using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sanctuary.DevClient;

public static class ClientInstaller
{
    private const string ClientCdnUrl = "https://opensourcefreerealms.com/client";
    private const string ClientManifestUrl = "https://opensourcefreerealms.com/clientmanifest.xml";
    private const string UserAgent = "Sanctuary client setup";
    private const string HtmlContentType = "text/html";
    private const string ClientFolderName = "client";
    private const string ClientExecutable = "FreeRealms.exe";
    private const int FirstNumberedClient = 2;
    private const string DownloadFolderName = ".client.download";
    private const int DownloadWorkers = 16;
    private const int DownloadChunkSize = 1024 * 1024;
    private const double BytesPerUnit = 1024;

    private static readonly string[] LauncherClientPath = ["OSFRLauncher", "Servers", "OSFR Public Server", "Client"];
    private static readonly string[] SizeUnits = ["B", "KiB", "MiB", "GiB", "TiB"];
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(30);

    public static async Task<IReadOnlyList<string>> GetClientFoldersAsync(string repositoryRoot, string? clientPath, int clientCount)
    {
        var firstClient = await GetFirstClientAsync(repositoryRoot, clientPath);
        var folders = new List<string> { firstClient };

        for (var number = FirstNumberedClient; number <= clientCount; number++)
        {
            var folder = Path.Combine(repositoryRoot, $"{ClientFolderName}-{number}");

            if (!HasClient(folder))
            {
                Console.WriteLine($"Copying a client for player {number}...");
                CopyDirectory(firstClient, folder);
            }

            folders.Add(folder);
        }

        return folders;
    }

    private static async Task<string> GetFirstClientAsync(string repositoryRoot, string? clientPath)
    {
        if (clientPath is not null)
            return HasClient(clientPath) ? Path.GetFullPath(clientPath) : throw new FileNotFoundException($"No {ClientExecutable} in {clientPath}");

        var clientFolder = Path.Combine(repositoryRoot, ClientFolderName);

        if (HasClient(clientFolder))
            return clientFolder;

        var launcherClient = Path.Combine([Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), .. LauncherClientPath]);

        if (HasClient(launcherClient))
            return launcherClient;

        if (Directory.Exists(clientFolder) && Directory.EnumerateFileSystemEntries(clientFolder).Any())
            throw new IOException($"{clientFolder} has files in it but no {ClientExecutable}");

        if (Directory.Exists(clientFolder))
            Directory.Delete(clientFolder);

        await DownloadClientAsync(repositoryRoot, clientFolder);

        return clientFolder;
    }

    private static bool HasClient(string folder) => File.Exists(Path.Combine(folder, ClientExecutable));

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));

        foreach (var directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private static async Task DownloadClientAsync(string repositoryRoot, string clientFolder)
    {
        using var httpClient = new HttpClient { Timeout = DownloadTimeout };

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

        var files = await ReadManifestAsync(httpClient);
        var totalSize = files.Sum(file => file.Size);
        var stagingFolder = Path.Combine(repositoryRoot, DownloadFolderName);
        var progress = new DownloadProgress(totalSize, files.Count);

        if (Directory.Exists(stagingFolder))
            Directory.Delete(stagingFolder, true);

        Directory.CreateDirectory(stagingFolder);

        try
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = DownloadWorkers };

            await Parallel.ForEachAsync(files, options, (file, cancellationToken) => DownloadFileAsync(httpClient, stagingFolder, file, progress, cancellationToken));

            Console.WriteLine();
            Directory.Move(stagingFolder, clientFolder);
        }
        catch
        {
            Console.Error.WriteLine();
            Directory.Delete(stagingFolder, true);
            throw;
        }
    }

    private static async Task<List<ManifestFile>> ReadManifestAsync(HttpClient httpClient)
    {
        var manifest = XDocument.Parse(await httpClient.GetStringAsync(ClientManifestUrl));
        var files = new List<ManifestFile>();

        VisitManifestElement(manifest.Root!, [], files);

        return files;
    }

    private static void VisitManifestElement(XElement element, string[] parent, List<ManifestFile> files)
    {
        foreach (var child in element.Elements())
        {
            var name = child.Attribute("name")?.Value;

            if (child.Name == "Folder")
            {
                VisitManifestElement(child, string.IsNullOrEmpty(name) ? parent : [.. parent, name], files);
            }
            else if (child.Name == "File" && name is not null)
            {
                if (Path.IsPathRooted(name) || name.Split('/', '\\').Contains(".."))
                    throw new InvalidDataException($"Unsafe path in client manifest: {name}");

                files.Add(new ManifestFile([.. parent, name], long.Parse(child.Attribute("size")!.Value)));
            }
        }
    }

    private static async ValueTask DownloadFileAsync(HttpClient httpClient, string stagingFolder, ManifestFile file, DownloadProgress progress, CancellationToken cancellationToken)
    {
        var destination = Path.Combine([stagingFolder, .. file.Path]);
        var url = $"{ClientCdnUrl}/{string.Join('/', file.Path.Select(Uri.EscapeDataString))}";

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(destination);

        var buffer = new byte[DownloadChunkSize];
        long fileSize = 0;
        int read;

        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            fileSize += read;
            progress.AddBytes(read);
        }

        var edgeInjectedHtml = response.Content.Headers.ContentType?.MediaType == HtmlContentType && fileSize >= file.Size;

        if (fileSize != file.Size && !edgeInjectedHtml)
            throw new IOException($"Size mismatch for {string.Join('/', file.Path)}: expected {file.Size} bytes, received {fileSize}");

        progress.CompleteFile();
    }

    private static string FormatSize(double size)
    {
        var unit = 0;

        while (size >= BytesPerUnit && unit < SizeUnits.Length - 1)
        {
            size /= BytesPerUnit;
            unit++;
        }

        return $"{size:F1} {SizeUnits[unit]}";
    }

    private sealed record ManifestFile(string[] Path, long Size);

    private sealed class DownloadProgress(long totalSize, int totalFiles)
    {
        private const double FullPercent = 100;

        private readonly Lock _lock = new();
        private long _downloaded;
        private int _completed;

        public void AddBytes(int count)
        {
            lock (_lock)
            {
                _downloaded += count;
                Report();
            }
        }

        public void CompleteFile()
        {
            lock (_lock)
            {
                _completed++;
                Report();
            }
        }

        private void Report()
        {
            var percent = totalSize > 0 ? _downloaded * FullPercent / totalSize : FullPercent;

            Console.Write($"\rDownloading client: {percent,6:F2}% ({FormatSize(_downloaded)} / {FormatSize(totalSize)}) [{_completed}/{totalFiles}]");
        }
    }
}
