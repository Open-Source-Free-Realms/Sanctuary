using System;
using System.Collections.Generic;
using System.Numerics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Gateway.Fishing;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class FishingPacketRegisterPlayerRequestHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(FishingPacketRegisterPlayerRequestHandler));

        // Give the fishing sessions a logger so their server-driven bite timeline is visible.
        Fishing.FishingSessions.Logger = loggerFactory.CreateLogger("FishingSession");

        // Give the fishing sessions DB access so caught items are persisted (survive logout).
        Fishing.FishingSessions.DbContextFactory =
            serviceProvider.GetRequiredService<IDbContextFactory<Sanctuary.Database.DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        _logger.LogTrace("Received fishing register player request. Payload: {payload}", Convert.ToHexString(data));

        var player = connection.Player;

        // NOTE: a self-AddPc (proxied character of ourselves) was tried here to satisfy the client's
        // bobber/line rendering, but the client ignores a proxied character keyed to the current player
        // (IsCurrentPlayer path) — no proxied character is created. The self bobber/line must come from
        // the local ControllerFishing instead.

        // Use the zone the player actually entered (recorded at minigame start); fall back to
        // their current position if we have no recorded zone.
        var session = FishingSessions.GetOrCreate(player);

        var spawnPos = player.Position;
        string? zoneName = session.ZoneConfig.ZoneName;
        if (zoneName is not null)
            spawnPos = session.ZoneConfig.SpawnPosition;

        // Water-surface Y baseline the client uses for bobber/fish placement.
        var waterY = spawnPos.Y;

        // Cast distance comes from the equipped rod's tier (short/greater/deepest). The client validates
        // a cast only when the water-raycast distance is within [min, max].
        var (minCast, maxCast) = session.GetCastDistance();

        connection.SendTunneled(new FishingPacketRegisterPlayerResponse
        {
            FishingPlayerConfig = new FishingPlayerConfig
            {
                Unknown = 4,
                Unknown2 = minCast,   // MinCastDistance (float) — from the equipped rod's tier
                Unknown3 = maxCast,   // MaxCastDistance (float) — from the equipped rod's tier
                Unknown4 = 1,
                Unknown5 = 5,
                Unknown6 = 100,
                Unknown7 = 21,
                Unknown8 = 50,
                Unknown9 = 1,
                // Camera block (client ControllerFishing::sub_AF85E0): distance/pitch/heading/target.
                Unknown10 = 6.0f,
                Unknown11 = 0.444f,
                Unknown12 = 1.85f,
                Unknown13 = 0.2f,
                Unknown14 = 1,
                Unknown15 = 1,
                Unknown16 = 1
            },
            // Preload ALL fishing-scene models (bobber, lure, underwater fish, catch fish), not just
            // the 3 underwater fish. The client instantiates each (sub_B68600 -> m_ActorIds), so the
            // bobber/lure are ready immediately at cast time instead of streaming in late.
            FishModelIds = [.. FishingSession.PreloadModelIds],
            FishingZoneConfig = new Sanctuary.Packet.Common.FishingZoneConfig
            {
                Unknown = zoneName,
                // Fish-arena X: MUST be the zone's Underwater_Bed X (the client hardcodes the
                // arena Y=-8/Z=485 to sit inside that bed). Using the overworld pond X puts the
                // fish above the wrong water — the "fish in the sky" bug.
                Unknown3 = session.ZoneConfig.UnderwaterBedX,
                Unknown6 = waterY      // overworld water-surface Y (bobber height where you cast)
            }
        });

        connection.SendTunneled(new FishingPacketUpdateData
        {
            Guid = player.Guid,
            Position = player.Position
        });

        // Populate the Fish Finder with this hole's real fish (name/icon per the wiki), matching what
        // the session can actually roll as a catch. Built from the per-zone fish table.
        connection.SendTunneled(new FishingPacketFishInfoUpdate
        {
            ClientFishEntries = session.BuildFishFinderEntries()
        });

        // NOTE: SpawnProxiedFishingSchool packets are intentionally NOT sent. The client places every
        // fish in a school at the SAME point (sub_CD12A0), so a school renders as a clump of fish
        // stacked on one spot — the "multiple fish stacked in the center" the player saw. The lively
        // underwater fish come from SpawnFishRun (sent on cast) instead.

        _logger.LogInformation("Player {guid} registered for fishing (zone {zone}) at {pos}", player.Guid, zoneName, spawnPos);
        return true;
    }
}
