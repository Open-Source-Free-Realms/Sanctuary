using System;
using System.IO;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketPortraitDataRequestHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketPortraitDataRequestHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PacketPortraitDataRequest.TryDeserialize(data, out var packetPortraitDataRequest))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketPortraitDataRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketPortraitDataRequest), packetPortraitDataRequest);

        var path = Path.Combine(PortraitStorage.GetCharacterDirectory(packetPortraitDataRequest.Guid), "headshot.png");

        if (!File.Exists(path))
            return true;

        var packetPlayerImageData = new PacketPlayerImageData
        {
            Guid = packetPortraitDataRequest.Guid,
            Provider = packetPortraitDataRequest.Provider,
            Portrait =
            {
                Unknown2 = 1,

                Guid = packetPortraitDataRequest.Guid,

                ModelId = connection.Player.Model,

                Attachments = connection.Player.GetAttachments(),

                Head = connection.Player.Head,
                Hair = connection.Player.Hair,
                SkinTone = connection.Player.SkinTone,
                FacePaint = connection.Player.FacePaint,
                ModelCustomization = connection.Player.ModelCustomization,

                HairColor = connection.Player.HairColor,
                EyeColor = connection.Player.EyeColor,
                HeadId = connection.Player.HeadId,
                HairId = connection.Player.HairId,
                SkinToneId = connection.Player.SkinToneId,
                FacePaintId = connection.Player.FacePaintId,

                Provider = packetPortraitDataRequest.Provider
            },
            PngPayload = File.ReadAllBytes(path)
        };

        connection.SendTunneled(packetPlayerImageData);

        return true;
    }
}