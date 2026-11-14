using System.Collections.Generic;

using Sanctuary.Game.Entities;

namespace Sanctuary.Game.MiniGames;

public sealed class MiniGameUiSession
{
    public int EncounterId { get; }
    public MiniGameUiDefinition Definition { get; }
    public Player Player { get; }
    public MiniGameUiState State { get; set; }
    public Dictionary<int, int> ObjectiveProgress { get; } = [];
    public int KnockOuts { get; set; }
    public int MaxKnockOuts { get; set; }

    public MiniGameUiSession(int encounterId, MiniGameUiDefinition definition, Player player)
    {
        EncounterId = encounterId;
        Definition = definition;
        Player = player;
    }
}
