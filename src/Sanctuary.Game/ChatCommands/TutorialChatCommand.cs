using Microsoft.EntityFrameworkCore;

using Sanctuary.Database;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class TutorialChatCommand : TutorialSetChatCommand
{
    protected override TutorialSet Set => TutorialSet.MiniGameTutorials;

    public override string KeyWord => "tutorial";
    public override string Description => "Manages which minigame tutorials (GameTutorialData.txt) a player has seen.";

    public TutorialChatCommand(IZoneManager zoneManager, IResourceManager resourceManager, IChatCommandManager chatCommandManager, IDbContextFactory<DatabaseContext> dbContextFactory)
        : base(zoneManager, resourceManager, chatCommandManager, dbContextFactory)
    {
    }
}
