using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Sanctuary.WebAPI.Endpoints;

public static class PortraitEndpoints
{
    private static ILogger _logger = null!;
    private readonly static ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = [];

    public static void MapPortraitEndpoints(this WebApplication app)
    {
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();

        _logger = loggerFactory.CreateLogger(nameof(AuthEndpoints));

        app.MapPost("/image", ImageHandlerAsync).DisableAntiforgery();
    }

    private static async Task<IResult> ImageHandlerAsync(
        HttpContext context,
        [FromForm] string imageType,
        [FromForm] ulong characterId,
        CancellationToken cancellationToken,
        [FromForm] IFormFileCollection files)
    {
        // ContentType and Boundary are hardcoded in the client.
        if (context.Request.ContentType != "multipart/form-data; boundary=AaBb432101234bBaA")
        {
            _logger.LogWarning("Invalid Content-Type: {ContentType}", context.Request.ContentType);

            return Results.BadRequest("Invalid Content-Type");
        }

        // ImageType is hardcoded in the client.
        if (imageType != "portrait")
        {
            _logger.LogWarning("Invalid imageType: {ImageType}", imageType);

            return Results.BadRequest("Invalid imageType.");
        }

        if (characterId == 0)
        {
            _logger.LogWarning("Invalid characterId: {CharacterId}", characterId);

            return Results.BadRequest("Invalid characterId.");
        }

        var saveDirectory = Path.Combine("Images", characterId.ToString());

        if (!Directory.Exists(saveDirectory))
            Directory.CreateDirectory(saveDirectory);

        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                _logger.LogWarning("Invalid file name: {FileName}", file.FileName);

                return Results.BadRequest("Invalid file name.");
            }

            if (file.ContentType != "image/png")
            {
                _logger.LogWarning("Invalid file name: {FileName}", file.FileName);

                return Results.BadRequest("Invalid file name.");
            }

            // Names are hardcoded in the client.
            if (file.Name != "thumbnailFile" && file.Name != "imageFile")
            {
                _logger.LogWarning("Invalid name: {Name}", file.Name);

                return Results.BadRequest("Invalid name.");
            }

            var fileName = Path.GetFileName(file.FileName);

            // File names are hardcoded in the client.
            if (fileName != "headshot.png" && fileName != "portrait.png")
            {
                _logger.LogWarning("Invalid file name: {FileName}", file.FileName);

                return Results.BadRequest("Invalid file name.");
            }

            var savePath = Path.Combine(saveDirectory, fileName);

            var fileLock = _fileLocks.GetOrAdd(savePath, new SemaphoreSlim(1, 1));

            await fileLock.WaitAsync();

            try
            {
                using var stream = file.OpenReadStream();

                using var image = await Image.LoadAsync<Rgba32>(stream, cancellationToken);

                if (image.Metadata.DecodedImageFormat is not PngFormat)
                {
                    _logger.LogWarning("Invalid image format: {Format}", image.Metadata.DecodedImageFormat?.Name);

                    return Results.BadRequest("Invalid image format.");
                }

                if (file.Name == "thumbnailFile" && image.Width != 70 && image.Height != 70)
                {
                    _logger.LogWarning("Invalid thumbnailFile size: {Width}x{Height}", image.Width, image.Height);

                    return Results.BadRequest("Invalid thumbnailFile size.");
                }

                if (file.Name == "imageFile" && image.Width != 180 && image.Height != 330)
                {
                    _logger.LogWarning("Invalid imageFile size: {Width}x{Height}", image.Width, image.Height);

                    return Results.BadRequest("Invalid imageFile size.");
                }

                await image.SaveAsPngAsync(savePath, cancellationToken);

                _logger.LogDebug("Successfully uploaded {Name} for character {Character}.", file.Name, characterId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving uploaded file for character {CharacterId}", characterId);

                return Results.StatusCode(500);
            }
            finally
            {
                fileLock.Release();
            }
        }

        return Results.Ok();
    }
}