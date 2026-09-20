using System;
using System.Globalization;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class ScaleChatCommand : IChatCommand
{
    private const float MinScale = 0.25f;
    private const float MaxScale = 5f;

    public string KeyWord => "scale";
    public string Usage => "<multiplier> [seconds] | reset";
    public string Description => "Resizes you - 2 is twice your normal size, 0.5 is half. Lasts forever unless you pass a duration. 'reset' puts you back to normal.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length is 0 or > 2)
            return false;

        if (args[0].Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            invoker.RemoveTemporaryScale();
            invoker.SetScale(Player.DefaultScale);

            ChatHelper.SendSystemMessage(invoker, "Size reset to normal.");

            return true;
        }

        if (!float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
            return false;

        if (!float.IsFinite(scale) || scale < MinScale || scale > MaxScale)
        {
            ChatHelper.SendSystemMessage(invoker, $"Scale must be between {MinScale} and {MaxScale}.");
            return false;
        }

        var durationMs = 0;

        if (args.Length == 2)
        {
            if (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0)
                return false;

            durationMs = seconds * 1000;
        }

        if (durationMs > 0)
            invoker.ApplyTemporaryScale(scale, durationMs);
        else
        {
            invoker.RemoveTemporaryScale();
            invoker.SetScale(scale);
        }

        ChatHelper.SendSystemMessage(invoker, durationMs > 0
            ? $"Scaled to {scale}x for {durationMs / 1000}s."
            : $"Scaled to {scale}x. Use !scale reset to go back to normal.");

        return true;
    }
}
