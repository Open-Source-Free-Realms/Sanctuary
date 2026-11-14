using System.Diagnostics.CodeAnalysis;

using Sanctuary.Packet.Common;

namespace Sanctuary.Game.MiniGames.CombatEncounters;

public sealed class CombatEncounterFactory : IMiniGameFactory
{
    private readonly IResourceManager _resourceManager;
    private readonly IZoneManager _zoneManager;

    public CombatEncounterFactory(IResourceManager resourceManager, IZoneManager zoneManager)
    {
        _resourceManager = resourceManager;
        _zoneManager = zoneManager;
    }

    public bool TryCreate(ClientActivityDefinition activity, MiniGameUiDefinition definition, [MaybeNullWhen(false)] out MiniGameSession session)
    {
        session = null;

        if (!_resourceManager.CombatEncounters.TryGetValue(definition.GameId, out var combatEncounter))
            return false;

        var host = new ZoneMiniGameHost(_zoneManager, combatEncounter.ZoneDefinitionId, combatEncounter.EntryPosition, combatEncounter.EntryRotation);

        session = new CombatEncounterSession(definition, combatEncounter, host);

        return true;
    }
}
