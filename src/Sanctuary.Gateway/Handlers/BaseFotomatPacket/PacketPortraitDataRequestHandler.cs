using System;
using System.IO;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketPortraitDataRequestHandler
{
    private static ILogger _logger = null!;
    private static IZoneManager _zoneManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketPortraitDataRequestHandler));
        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
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

        if (!_zoneManager.TryGetPlayer(packetPortraitDataRequest.Guid, out var portraitPlayer))
            return true;

        var packetPlayerImageData = new PacketPlayerImageData
        {
            Guid = packetPortraitDataRequest.Guid,
            Provider = packetPortraitDataRequest.Provider,
            Portrait =
            {
                Unknown2 = 1,

                Guid = packetPortraitDataRequest.Guid,

                ModelId = portraitPlayer.Model,

                Attachments = portraitPlayer.GetAttachments(),

                Head = portraitPlayer.Head,
                Hair = portraitPlayer.Hair,
                SkinTone = portraitPlayer.SkinTone,
                FacePaint = portraitPlayer.FacePaint,
                ModelCustomization = portraitPlayer.ModelCustomization,

                HairColor = portraitPlayer.HairColor,
                EyeColor = portraitPlayer.EyeColor,
                HeadId = portraitPlayer.HeadId,
                HairId = portraitPlayer.HairId,
                SkinToneId = portraitPlayer.SkinToneId,
                FacePaintId = portraitPlayer.FacePaintId,

                Provider = packetPortraitDataRequest.Provider
            },
            PngPayload = File.ReadAllBytes(path)
        };

        connection.SendTunneled(packetPlayerImageData);

        return true;
    }
}