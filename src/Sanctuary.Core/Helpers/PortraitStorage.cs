using System;
using System.IO;

namespace Sanctuary.Core.Helpers;

public static class PortraitStorage
{
    // Set the same absolute directory for Gateway and WebAPI when hosted separately.
    public static string GetCharacterDirectory(ulong guid)
    {
        var root = Environment.GetEnvironmentVariable("SANCTUARY_PORTRAIT_IMAGE_ROOT");
        return Path.Combine(string.IsNullOrWhiteSpace(root) ? "Images" : root, guid.ToString());
    }
}
