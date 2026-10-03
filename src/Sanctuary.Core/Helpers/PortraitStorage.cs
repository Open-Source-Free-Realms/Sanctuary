using System;
using System.IO;

namespace Sanctuary.Core.Helpers;

public static class PortraitStorage
{
    public static uint GetPortraitCrc(ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;

        foreach (var value in data)
        {
            crc ^= value;

            for (var bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
        }

        return ~crc;
    }

    // Set the same absolute directory for Gateway and WebAPI when hosted separately.
    public static string GetCharacterDirectory(ulong guid)
    {
        var root = Environment.GetEnvironmentVariable("SANCTUARY_PORTRAIT_IMAGE_ROOT");
        return Path.Combine(string.IsNullOrWhiteSpace(root) ? "Images" : root, guid.ToString());
    }
}
