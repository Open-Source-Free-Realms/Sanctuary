using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.MiniGames.CombatEncounters;

public abstract class CombatEncounter
{
    private const int EncounterZoningId = 0;
    private const byte EncounterZoningUnknown = 3;
    private const int EncounterZoningGeometryId = 96;

    public abstract MiniGameUiDefinition Definition { get; }

    public abstract int ZoneDefinitionId { get; }

    public abstract Vector4 EntryPosition { get; }

    public abstract Quaternion EntryRotation { get; }

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
