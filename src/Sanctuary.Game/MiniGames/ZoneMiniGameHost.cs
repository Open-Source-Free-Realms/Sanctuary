using System.Diagnostics.CodeAnalysis;
using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.MiniGames;

public sealed class ZoneMiniGameHost : IMiniGameHost
{
    private const int ZoningId = 0;
    private const int ZoningGeometryId = 96;

    private readonly IZoneManager _zoneManager;

    public ZoneMiniGameHost(IZoneManager zoneManager, int zoneDefinitionId, Vector4 entryPosition, Quaternion entryRotation)
    {
        _zoneManager = zoneManager;
        ZoneDefinitionId = zoneDefinitionId;
        EntryPosition = entryPosition;
        EntryRotation = entryRotation;
    }

    public int ZoneDefinitionId { get; }
    public Vector4 EntryPosition { get; }
    public Quaternion EntryRotation { get; }

    public bool LoadsZone => true;

    public bool TryEnter(Player player, [MaybeNullWhen(false)] out IZone zone)
    {
        if (player.Mount is not null)
            player.Dismount();

        return player.TeleportToZone(ZoneDefinitionId, player.Guid, EntryPosition, EntryRotation, ZoningId, ZoningGeometryId, out zone);
    }

    public void Leave(Player player)
    {
        // Leaving a world zone records where the player was; return them there.
        player.TeleportToZone(_zoneManager.StartingZone, player.StartingZonePosition, player.StartingZoneRotation);
    }
}
