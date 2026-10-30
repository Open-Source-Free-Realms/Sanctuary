using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.MiniGames.CombatEncounters;

public class CombatEncounter
{
    private const int EncounterZoningId = 0;
    private const byte EncounterZoningUnknown = 3;
    private const int EncounterZoningGeometryId = 96;

    public MiniGameUiDefinition Definition { get; init; } = new();

    public int ZoneDefinitionId { get; init; }

    public Vector4 EntryPosition { get; init; }

    public Quaternion EntryRotation { get; init; }

    public virtual void OnGo(Player player, MiniGameUiSession miniGameUiSession, IZoneManager zoneManager)
    {
        if (player.Mount is not null)
            player.Dismount();

        if (!player.TeleportToZone(ZoneDefinitionId, player.Guid, EntryPosition, EntryRotation, EncounterZoningId, EncounterZoningUnknown, EncounterZoningGeometryId, out _))
            return;

        miniGameUiSession.State = MiniGameUiState.Entering;
    }

    public virtual void OnClose(Player player, MiniGameUiSession miniGameUiSession, IZoneManager zoneManager)
    {
        player.TeleportToZone(zoneManager.StartingZone, player.StartingZonePosition, player.StartingZoneRotation);
    }
}
