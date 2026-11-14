using System.Diagnostics.CodeAnalysis;

using Sanctuary.Packet.Common;

namespace Sanctuary.Game.MiniGames;

public interface IMiniGameFactory
{
    bool TryCreate(ClientActivityDefinition activity, MiniGameUiDefinition definition, [MaybeNullWhen(false)] out MiniGameSession session);
}
