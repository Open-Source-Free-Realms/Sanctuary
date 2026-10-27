using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Tests ItemAbilities.json (potions, scrolls, buff foods) without needing the items.
/// </summary>
public class ItemAbilityChatCommand : IChatCommand
{
    private const int PageSize = 15;

    private readonly IResourceManager _resourceManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "itemability";
    public string Usage => "list [page] | use <abilityId> | buffs | clear";
    public string Description => "Lists and triggers item abilities (potions, scrolls, buff foods), shows your active buffs, or clears them.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public ItemAbilityChatCommand(IResourceManager resourceManager, IChatCommandManager chatCommandManager)
    {
        _resourceManager = resourceManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        switch (args[0].ToLowerInvariant())
        {
            case "list":
                return HandleList(invoker, args[1..]);

            case "use":
                return HandleUse(invoker, args[1..]);

            case "buffs":
                HandleBuffs(invoker);
                return true;

            case "clear":
                var groups = invoker.Effects.Select(effect => effect.Group).OfType<string>().Distinct().ToList();

                foreach (var group in groups)
                    invoker.RemoveEffectGroup(group);

                ChatHelper.SendSystemMessage(invoker, $"Cleared {groups.Count} item ability buff group(s).");
                return true;

            default:
                return false;
        }
    }

    private bool HandleList(Player invoker, string[] args)
    {
        var page = 1;

        if (args.Length > 0 && (!int.TryParse(args[0], out page) || page < 1))
            return false;

        var abilities = _resourceManager.ItemAbilities.Values.OrderBy(x => x.AbilityId).ToList();
        var pageCount = Math.Max(1, (abilities.Count + PageSize - 1) / PageSize);

        page = Math.Min(page, pageCount);

        ChatHelper.SendSystemMessage(invoker, $"Item abilities (page {page}/{pageCount}):");

        foreach (var ability in abilities.Skip((page - 1) * PageSize).Take(PageSize))
            ChatHelper.SendSystemMessage(invoker, $"{ability.AbilityId,-6} {ability.Group ?? "Cast"}, {ability.DurationMs / 1000}s");

        return true;
    }

    private bool HandleUse(Player invoker, string[] args)
    {
        if (args.Length != 1 || !int.TryParse(args[0], out var abilityId))
            return false;

        if (!_resourceManager.ItemAbilities.TryGetValue(abilityId, out var ability))
        {
            ChatHelper.SendSystemMessage(invoker, $"Unknown item ability {abilityId}. Use !{KeyWord} list.");
            return true;
        }

        // Borrow the name and icon of an item that grants it, for the buff bar.
        var item = _resourceManager.ClientItemDefinitions.Values.FirstOrDefault(x => x.ActivatableAbilityId == abilityId);

        ItemAbilityHelper.Apply(invoker, ability, item?.NameId ?? 0, item?.Icon.Id ?? 0);

        _chatCommandManager.LogAction(this, invoker, "Use", null, $"Ability: {abilityId}");
        ChatHelper.SendSystemMessage(invoker, $"Used item ability {abilityId}. Hitpoints {invoker.Hitpoints}/{invoker.MaxHitpoints}, energy {invoker.Energy}/{invoker.EffectiveMaxEnergy}.");

        return true;
    }

    private static void HandleBuffs(Player invoker)
    {
        var buffs = invoker.Effects.Where(effect => effect.Group is not null).ToList();

        if (buffs.Count == 0)
        {
            ChatHelper.SendSystemMessage(invoker, "No item ability buffs active.");
            return;
        }

        foreach (var buff in buffs)
        {
            var remaining = buff.ExpiresAt is { } expiresAt ? $"{Math.Max(0, (int)(expiresAt - DateTimeOffset.UtcNow).TotalSeconds)}s" : "until removed";
            var stats = buff.StatBonuses is null ? "" : " " + string.Join(", ", buff.StatBonuses.Select(stat => $"{stat.Key} {stat.Value:+0.##;-0.##}"));

            ChatHelper.SendSystemMessage(invoker, $"{buff.Group}: {remaining}{stats}");
        }
    }
}
