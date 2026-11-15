namespace Sanctuary.Game.MiniGames;

public sealed class UnavailableMiniGameSession : MiniGameSession
{
    public UnavailableMiniGameSession(MiniGameUiDefinition definition)
        : base(definition, WorldMiniGameHost.Instance)
    {
    }

    public override bool Playable => false;
}
