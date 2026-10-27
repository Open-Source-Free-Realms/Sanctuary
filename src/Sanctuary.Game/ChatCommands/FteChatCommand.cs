using Microsoft.EntityFrameworkCore;

using Sanctuary.Database;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class FteChatCommand : TutorialSetChatCommand
{
    protected override TutorialSet Set => TutorialSet.FirstTimeEvents;

    public override string KeyWord => "fte";
    public override string Description => "Manages which first-time events (UI tips, FirstTimeEvents.txt) a player has seen. Names work in place of ids.";

    public FteChatCommand(IZoneManager zoneManager, IResourceManager resourceManager, IChatCommandManager chatCommandManager, IDbContextFactory<DatabaseContext> dbContextFactory)
        : base(zoneManager, resourceManager, chatCommandManager, dbContextFactory)
    {
    }
}
