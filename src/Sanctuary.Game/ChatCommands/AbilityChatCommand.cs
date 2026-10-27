using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Previews combat abilities from CombatAbilities.json on the toolbar without editing CombatJobs.json.
/// Combat abilities are not executed server-side yet, so this only changes what the toolbar shows.
/// </summary>
public class AbilityChatCommand : IChatCommand
{
    private const int PageSize = 15;

    private readonly IResourceManager _resourceManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "ability";
    public string Usage => "list [page] | basic|special <abilityId> | clear";
    public string Description => "Overrides your basic/special toolbar ability with any combat ability for testing. Needs a weapon equipped.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public AbilityChatCommand(IResourceManager resourceManager, IChatCommandManager chatCommandManager)
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

            case "clear":
                invoker.BasicAbilityOverride = null;
                invoker.SpecialAbilityOverride = null;
                invoker.SendToolbar();

                ChatHelper.SendSystemMessage(invoker, "Ability overrides cleared.");
                return true;

            case "basic":
            case "special":
                break;

            default:
                return false;
        }

        if (args.Length != 2 || !int.TryParse(args[1], out var abilityId))
            return false;

        if (!_resourceManager.CombatAbilities.ContainsKey(abilityId))
        {
            ChatHelper.SendSystemMessage(invoker, $"Unknown ability {abilityId}. Use !{KeyWord} list.");
            return true;
        }

        var basic = args[0].Equals("basic", StringComparison.OrdinalIgnoreCase);

        if (basic)
            invoker.BasicAbilityOverride = abilityId;
        else
            invoker.SpecialAbilityOverride = abilityId;

        if (!invoker.SendToolbar())
        {
            ChatHelper.SendSystemMessage(invoker, "Your active profile has no combat job kit, so the override is stored but not shown.");
            return true;
        }

        _chatCommandManager.LogAction(this, invoker, basic ? "Basic" : "Special", null, $"Ability: {abilityId}");
        ChatHelper.SendSystemMessage(invoker, $"{(basic ? "Basic" : "Special")} ability set to {abilityId}.");

        return true;
    }

    private bool HandleList(Player invoker, string[] args)
    {
        var page = 1;

        if (args.Length > 0 && (!int.TryParse(args[0], out page) || page < 1))
            return false;

        var abilities = _resourceManager.CombatAbilities.Values.OrderBy(x => x.Id).ToList();
        var pageCount = Math.Max(1, (abilities.Count + PageSize - 1) / PageSize);

        ChatHelper.SendSystemMessage(invoker, $"Combat abilities (page {Math.Min(page, pageCount)}/{pageCount}):");

        foreach (var ability in abilities.Skip((page - 1) * PageSize).Take(PageSize))
            ChatHelper.SendSystemMessage(invoker, $"{ability.Id,-6} {ability.EffectType}, {ability.Damage} dmg, {ability.EnergyCost} energy {ability.Comment}");

        return true;
    }
}
