using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sanctuary.WebAPI.Options;

namespace Sanctuary.WebAPI.Endpoints;

/// <summary>
/// Client asset delivery (the client's <c>AssetDelivery:IndirectServerAddress</c>).
/// </summary>
/// <remarks>
/// The client downloads <c>manifest.crc</c> and <c>manifest.txt.z</c>, and fetches any asset whose
/// manifest crc doesn't match its local packs from <c>&lt;bucket&gt;/&lt;name&gt;</c>.
/// Assets are served from a local mirror of the asset server, falling back to the upstream server.
/// Files in the client mods folder replace the asset of the same name and are patched into the
/// served manifest, so clients pick them up without any local changes.
/// </remarks>
public static class AssetEndpoints
{
    private const string HttpClientName = "AssetUpstream";

    private const string ManifestCrcFileName = "manifest.crc";
    private const string ManifestFileName = "manifest.txt.z";

    /// <summary>
    /// Header of Forgelight ".z" files, followed by the big-endian uncompressed size and zlib data.
    /// </summary>
    private const uint CompressedMagic = 0xA1B2C3D4;

    private static ILogger _logger = null!;

    private static readonly SemaphoreSlim _manifestLock = new(1, 1);
    private static PatchedManifest? _manifest;

    private sealed record PatchedManifest(string Key, string Crc, byte[] Compressed);

    public static void AddAssetEndpointServices(this IServiceCollection services)
    {
        services.AddHttpClient(HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
    }

    public static void MapAssetEndpoints(this WebApplication app)
    {
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();

        _logger = loggerFactory.CreateLogger(nameof(AssetEndpoints));

        app.MapGet("/assets/" + ManifestCrcFileName, ManifestCrcHandlerAsync);
        app.MapGet("/assets/" + ManifestFileName, ManifestHandlerAsync);
        app.MapGet("/assets/{bucket}/{fileName}", AssetHandlerAsync);
    }

    private static async Task<IResult> ManifestCrcHandlerAsync(
        IWebHostEnvironment environment,
        IOptionsSnapshot<WebAPIOptions> webAPIOptions,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        var manifest = await GetManifestAsync(environment, webAPIOptions.Value, httpClientFactory, cancellationToken);

        return manifest is null
            ? Results.StatusCode(StatusCodes.Status502BadGateway)
            : Results.Text(manifest.Crc);
    }

    private static async Task<IResult> ManifestHandlerAsync(
        IWebHostEnvironment environment,
        IOptionsSnapshot<WebAPIOptions> webAPIOptions,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        var manifest = await GetManifestAsync(environment, webAPIOptions.Value, httpClientFactory, cancellationToken);

        return manifest is null
            ? Results.StatusCode(StatusCodes.Status502BadGateway)
            : Results.Bytes(manifest.Compressed, "application/octet-stream");
    }

    private static async Task<IResult> AssetHandlerAsync(
        string bucket,
        string fileName,
        HttpContext context,
        IWebHostEnvironment environment,
        IOptionsSnapshot<WebAPIOptions> webAPIOptions,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        // Only plain "<bucket>/<asset>" paths are valid.
        if (fileName != Path.GetFileName(fileName) || bucket != Path.GetFileName(bucket))
            return Results.BadRequest();

        var options = webAPIOptions.Value;

        var clientModPath = Path.Combine(GetClientModsDirectory(environment, options), fileName);

        if (File.Exists(clientModPath))
        {
            _logger.LogInformation("Serving client mod {fileName}.", fileName);

            return Results.File(clientModPath, "application/octet-stream");
        }

        if (!string.IsNullOrEmpty(options.AssetsDirectory))
        {
            var assetPath = Path.Combine(options.AssetsDirectory, bucket, fileName);

            if (File.Exists(assetPath))
                return Results.File(assetPath, "application/octet-stream");
        }

        var data = await GetUpstreamAsync(options, httpClientFactory, $"{Uri.EscapeDataString(bucket)}/{Uri.EscapeDataString(fileName)}{context.Request.QueryString}", cancellationToken);

        return data is null
            ? Results.NotFound()
            : Results.Bytes(data, "application/octet-stream");
    }

    private static string GetClientModsDirectory(IWebHostEnvironment environment, WebAPIOptions options)
    {
        return Path.Combine(environment.ContentRootPath, options.ClientModsDirectory ?? "ClientMods");
    }

    private static async Task<byte[]?> GetUpstreamAsync(WebAPIOptions options, IHttpClientFactory httpClientFactory, string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(options.AssetUpstreamAddress))
            return null;

        var url = $"{options.AssetUpstreamAddress.TrimEnd('/')}/{path}";

        try
        {
            var httpClient = httpClientFactory.CreateClient(HttpClientName);

            using var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Failed to fetch asset {url}.", url);
            return null;
        }
    }

    private static async Task<PatchedManifest?> GetManifestAsync(IWebHostEnvironment environment, WebAPIOptions options,
        IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var clientModsDirectory = GetClientModsDirectory(environment, options);

        var clientMods = Directory.Exists(clientModsDirectory)
            ? new DirectoryInfo(clientModsDirectory).GetFiles().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList()
            : [];

        var localManifest = string.IsNullOrEmpty(options.AssetsDirectory)
            ? null
            : new FileInfo(Path.Combine(options.AssetsDirectory, ManifestFileName));

        // Rebuild whenever the source manifest or any mod changes.
        var key = string.Join('|', clientMods.Select(x => $"{x.Name}:{x.Length}:{x.LastWriteTimeUtc.Ticks}"))
            + $"|{localManifest?.FullName}:{(localManifest?.Exists == true ? localManifest.LastWriteTimeUtc.Ticks : 0)}";

        if (_manifest?.Key == key)
            return _manifest;

        await _manifestLock.WaitAsync(cancellationToken);

        try
        {
            if (_manifest?.Key == key)
                return _manifest;

            var source = localManifest?.Exists == true
                ? await File.ReadAllBytesAsync(localManifest.FullName, cancellationToken)
                : await GetUpstreamAsync(options, httpClientFactory, ManifestFileName, cancellationToken);

            if (source is null)
            {
                _logger.LogError("No asset manifest available locally or upstream.");
                return null;
            }

            var lines = Encoding.UTF8.GetString(Decompress(source))
                .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            foreach (var clientMod in clientMods)
            {
                var isCompressed = clientMod.Name.EndsWith(".z", StringComparison.OrdinalIgnoreCase);
                var assetName = isCompressed ? clientMod.Name[..^2] : clientMod.Name;

                // Replace the original entry whether it was listed compressed or not.
                lines.RemoveAll(line =>
                {
                    var name = line.Split(',')[0];
                    return name.Equals(assetName, StringComparison.OrdinalIgnoreCase)
                        || name.Equals(assetName + ".z", StringComparison.OrdinalIgnoreCase);
                });

                var data = await File.ReadAllBytesAsync(clientMod.FullName, cancellationToken);

                // ".z" entries list the crc of the uncompressed asset and the size of the download.
                var crc = isCompressed ? Crc32(Decompress(data)) : Crc32(data);

                lines.Add($"{clientMod.Name},{crc},{data.Length}");
            }

            var manifest = Encoding.UTF8.GetBytes(string.Join("\r\n", lines) + "\r\n");

            _manifest = new PatchedManifest(key, $"{Crc32(manifest)},{manifest.Length}", Compress(manifest));

            _logger.LogInformation("Built asset manifest with {count} entries and {mods} client mod(s).", lines.Count, clientMods.Count);

            return _manifest;
        }
        finally
        {
            _manifestLock.Release();
        }
    }

    private static byte[] Decompress(byte[] data)
    {
        if (data.Length < 8 || BinaryPrimitives.ReadUInt32BigEndian(data) != CompressedMagic)
            throw new InvalidDataException("Not a compressed asset.");

        var size = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4));

        using var input = new ZLibStream(new MemoryStream(data, 8, data.Length - 8), CompressionMode.Decompress);
        using var output = new MemoryStream(size);

        input.CopyTo(output);

        return output.ToArray();
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();

        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(header, CompressedMagic);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], data.Length);
        output.Write(header);

        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(data);

        return output.ToArray();
    }

    private static readonly uint[] Crc32Table = Enumerable.Range(0, 256).Select(i =>
    {
        var crc = (uint)i;

        for (var bit = 0; bit < 8; bit++)
            crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;

        return crc;
    }).ToArray();

    /// <summary>
    /// Standard CRC-32, the checksum the client's manifest uses.
    /// </summary>
    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFF;

        foreach (var b in data)
            crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);

        return ~crc;
    }
}
