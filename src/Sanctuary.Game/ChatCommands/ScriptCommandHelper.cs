using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Sanctuary.Game.Authoring;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Zones;
using Sanctuary.Scripting;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// "!npc script ..." and "!prop script ...": edits the GM script on the selected world NPC or prop.
/// Source arrives from the GM panel in escaped chunks (see <see cref="PanelTextHelper"/>) and is
/// compiled on commit; results go back as script|ok and script|error panel lines.
/// </summary>
public static class ScriptCommandHelper
{
    public const string Usage = "script show | append <escaped chunk> | commit | cancel | clear | test | use <library name> [key=value ...]";

    /// <summary>
    /// Panel lines carry at most this much escaped source each.
    /// </summary>
    private const int ChunkLength = 200;

    /// <param name="rawArgs">The text after "script", exactly as typed.</param>
    public static bool Handle(IChatCommand command, IChatCommandManager chatCommandManager, IScriptLibrary scriptLibrary, Player invoker, Npc target, string kind, string[] args, string rawArgs)
    {
        if (invoker.ChatCommandRole < ChatCommandRole.Admin)
        {
            ChatHelper.SendSystemMessage(invoker, "Only admins can edit scripts.");
            return true;
        }

        if (args.Length == 0)
            return false;

        if (target.Zone is not WorldZone worldZone || !(worldZone.IsWorldNpc(target.Guid) || worldZone.IsWorldFixture(target.Guid)))
        {
            ChatHelper.SendSystemMessage(invoker, "Only NPCs and props saved in a world zone can have scripts.");
            return true;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "show":
                SendSource(invoker, target, kind, worldZone.GetSavedScript(target.Guid) ?? string.Empty);
                return true;

            case "append":
                if (args.Length < 2)
                    return false;

                // Raw, so indentation and runs of spaces in the source survive.
                var chunk = ChatCommandText.Remainder(rawArgs, 1);

                invoker.PendingScript ??= new StringBuilder();

                if (invoker.PendingScript.Length + chunk.Length > ScriptSandbox.MaxSourceLength * 2)
                {
                    invoker.PendingScript = null;
                    SendResult(invoker, false, $"Script too long; at most {ScriptSandbox.MaxSourceLength} characters.");
                    return true;
                }

                invoker.PendingScript.Append(chunk);
                return true;

            case "cancel":
                invoker.PendingScript = null;
                return true;

            case "commit":
                if (invoker.PendingScript is null)
                {
                    SendResult(invoker, false, "Nothing to commit; append the script first.");
                    return true;
                }

                var source = PanelTextHelper.Unescape(invoker.PendingScript.ToString());
                invoker.PendingScript = null;

                // A library template loaded into the editor still has its {{placeholders}}; fill them with the header defaults.
                if (!ScriptTemplate.Parse(source, null, null, false).TryRender(new Dictionary<string, string>(), out var filled, out var fillError))
                {
                    SendResult(invoker, false, fillError!);
                    return true;
                }

                _ = CommitAsync(command, chatCommandManager, invoker, worldZone, target, filled);
                return true;

            case "clear":
                _ = CommitAsync(command, chatCommandManager, invoker, worldZone, target, null);
                return true;

            case "test":
                _ = TestAsync(invoker, target);
                return true;

            case "use":
                if (args.Length < 2)
                    return false;

                if (!TryRenderLibraryScript(scriptLibrary, args[1], args[2..], out var rendered, out var renderError))
                {
                    SendResult(invoker, false, renderError!);
                    return true;
                }

                _ = CommitAsync(command, chatCommandManager, invoker, worldZone, target, rendered);
                return true;

            default:
                return false;
        }
    }

    private static async Task CommitAsync(IChatCommand command, IChatCommandManager chatCommandManager, Player invoker, WorldZone worldZone, Npc target, string? source)
    {
        var error = await target.Zone.NpcContent.SetScriptAsync(target, source);

        // A compile error leaves the old script in place; an onSpawn error still saves it.
        var compiled = source is null ? target.GmScript is null : target.GmScript?.Source == source;

        if (compiled)
        {
            worldZone.TrySaveScript(target, source);
            chatCommandManager.LogAction(command, invoker, source is null ? "ScriptClear" : "ScriptSave", null,
                $"Guid: {target.Guid}, Length: {source?.Length ?? 0}");
        }

        if (error is not null)
            SendResult(invoker, false, error);
        else
            SendResult(invoker, true, source is null ? "Script removed." : "Script saved and running.");
    }

    private static async Task TestAsync(Player invoker, Npc target)
    {
        var error = await target.Zone.NpcContent.TestScriptAsync(target, invoker);

        SendResult(invoker, error is null, error ?? "Ran onSpawn and onInteract without errors.");
    }

    /// <summary>
    /// Renders a library script with "key=value" arguments, whose values are escaped (see <see cref="PanelTextHelper"/>).
    /// </summary>
    private static bool TryRenderLibraryScript(IScriptLibrary scriptLibrary, string name, string[] arguments, out string source, out string? error)
    {
        source = string.Empty;

        if (!scriptLibrary.TryGet(name, out var entry))
        {
            error = $"No library script named {name}. Use !scriptlib list.";
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var argument in arguments)
        {
            var separator = argument.IndexOf('=');

            if (separator <= 0)
            {
                error = $"Expected key=value, got '{argument}'.";
                return false;
            }

            values[argument[..separator]] = PanelTextHelper.Unescape(argument[(separator + 1)..]);
        }

        return entry.TryRender(values, out source, out error);
    }

    private static void SendSource(Player invoker, Npc target, string kind, string source)
        => SendSource(invoker, kind, target.Guid, source);

    /// <summary>
    /// Sends source to the panel's script editor as script|begin, script|chunk... and script|end.
    /// </summary>
    public static void SendSource(Player invoker, string kind, object id, string source)
    {
        var escaped = PanelTextHelper.Escape(source);

        ChatHelper.SendGmPanelData(invoker, "script", "begin", kind, id);

        for (var i = 0; i < escaped.Length; i += ChunkLength)
            ChatHelper.SendGmPanelData(invoker, "script", "chunk", escaped.Substring(i, Math.Min(ChunkLength, escaped.Length - i)));

        ChatHelper.SendGmPanelData(invoker, "script", "end");
    }

    public static void SendResult(Player invoker, bool ok, string message)
    {
        ChatHelper.SendGmPanelData(invoker, "script", ok ? "ok" : "error", PanelTextHelper.Escape(message));
        ChatHelper.SendSystemMessage(invoker, ok ? message : $"Script error: {message}");
    }
}
