using System.Collections.Generic;
using System.Linq;

using Sanctuary.Game.Entities;

namespace Sanctuary.Game.Helpers;

/// <summary>
/// Overhead icons the game's own NPCs use (quest givers, Station Cash vendors, festive and minigame icons, ...),
/// for the GM panel's icon picker. Each is a (type, icon, state) as sent in NotificationInfo, labelled with
/// NPCs that show it.
/// </summary>
public static class NpcIconCatalog
{
    private const int EntriesPerLine = 12;

    private static List<string>? _entries;

    public static void Send(Player player, IResourceManager resourceManager)
    {
        var entries = _entries ??= Build(resourceManager);

        ChatHelper.SendGmPanelData(player, "icons", "begin");

        foreach (var chunk in entries.Chunk(EntriesPerLine))
            ChatHelper.SendGmPanelData(player, "icons", ["add", .. chunk]);

        ChatHelper.SendGmPanelData(player, "icons", "end");
    }

    // "type:icon:state:count:example names", most used first.
    private static List<string> Build(IResourceManager resourceManager)
    {
        var uses = new Dictionary<(int Type, int Icon, int State), List<string>>();

        foreach (var definition in resourceManager.Npcs.Values)
        {
            var notifications = definition.Notifications.AsEnumerable();

            if (definition.Notification is not null)
                notifications = notifications.Append(definition.Notification);

            foreach (var notification in notifications)
            {
                var key = (notification.NotificationType, notification.IconId, notification.IconState);

                if (!uses.TryGetValue(key, out var names))
                    uses[key] = names = [];

                if (!string.IsNullOrWhiteSpace(definition.Name))
                    names.Add(definition.Name.Trim());
            }
        }

        return uses
            .OrderByDescending(x => x.Value.Count)
            .Select(x =>
            {
                var examples = string.Join(", ", x.Value.Distinct().Take(3)).Replace(":", " ").Replace(";", " ");
                return $"{x.Key.Type}:{x.Key.Icon}:{x.Key.State}:{x.Value.Count}:{examples}";
            })
            .ToList();
    }
}
