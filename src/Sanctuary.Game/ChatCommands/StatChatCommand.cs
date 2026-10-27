using System;
using System.Globalization;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.ChatCommands;

public class StatChatCommand : IChatCommand
{
    private readonly IZoneManager _zoneManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "stat";
    public string Usage => "list | <stat> [value] [player]";
    public string Description => "Shows or overrides a character stat (e.g. MaxMovementSpeed, JumpHeight, GlideEnabled) by name or id. Not saved; resets on relog.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public StatChatCommand(IZoneManager zoneManager, IChatCommandManager chatCommandManager)
    {
        _zoneManager = zoneManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        if (args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var (id, stat) in invoker.Stats)
                ChatHelper.SendSystemMessage(invoker, $"{(int)id,-4}{id,-32}{FormatValue(stat)}");

            return true;
        }

        if (!Enum.TryParse<CharacterStatId>(args[0], true, out var statId)
            || !Enum.IsDefined(statId) || statId == CharacterStatId.InvalidStat)
        {
            ChatHelper.SendSystemMessage(invoker, $"Unknown stat \"{args[0]}\". Use !{KeyWord} list.");
            return true;
        }

        var target = invoker;

        if (args.Length > 2)
        {
            var targetName = string.Join(' ', args[2..]);

            if (!_zoneManager.TryGetPlayer(targetName, out target))
            {
                ChatHelper.SendSystemMessage(invoker, $"No online player named \"{targetName}\" was found.");
                return true;
            }
        }

        target.Stats.TryGetValue(statId, out var current);

        if (args.Length == 1)
        {
            ChatHelper.SendSystemMessage(invoker, current is null
                ? $"{statId} is not set."
                : $"{statId} = {FormatValue(current)}");
            return true;
        }

        // Keep the stat's existing type; the client reads ints and floats differently.
        var isFloat = current?.IsFloat ?? args[1].Contains('.');

        CharacterStat newStat;

        if (isFloat && float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
            newStat = new CharacterStat(statId, floatValue);
        else if (!isFloat && int.TryParse(args[1], out var intValue))
            newStat = new CharacterStat(statId, intValue);
        else
        {
            ChatHelper.SendSystemMessage(invoker, $"{statId} expects {(isFloat ? "a decimal" : "a whole")} number.");
            return true;
        }

        target.UpdateCharacterStats(newStat);

        _chatCommandManager.LogAction(this, invoker, "Set", target.Name.FullName, $"{statId}: {FormatValue(newStat)}");
        ChatHelper.SendSystemMessage(invoker, $"{statId} set to {FormatValue(newStat)} on {target.Name.FullName}.");

        return true;
    }

    private static string FormatValue(CharacterStat stat)
    {
        return stat.IsFloat
            ? stat.Float.ToString("0.###", CultureInfo.InvariantCulture)
            : stat.Int.ToString(CultureInfo.InvariantCulture);
    }
}
