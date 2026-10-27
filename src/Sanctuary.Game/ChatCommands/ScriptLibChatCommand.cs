using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sanctuary.Game.Authoring;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Scripting;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// The script library: shipped templates plus GM-saved scripts, applied to an NPC or prop with
/// "!npc script use" / "!prop script use". Lists go to the GM panel as scriptlib|... lines.
/// </summary>
public class ScriptLibChatCommand : IRawChatCommand
{
    private readonly IScriptLibrary _scriptLibrary;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "scriptlib";
    public string Usage => "list [search] | show <name> | append <escaped chunk> | commit <name> [description] | cancel | delete <name>";
    public string Description => "Browses and saves reusable scripts. Apply one with !npc script use <name> [key=value ...].";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public ScriptLibChatCommand(IScriptLibrary scriptLibrary, IChatCommandManager chatCommandManager)
    {
        _scriptLibrary = scriptLibrary;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args) => Handle(invoker, args, string.Join(' ', args));

    public bool Handle(Player invoker, string[] args, string rawArgs)
    {
        if (args.Length == 0)
            return false;

        switch (args[0].ToLowerInvariant())
        {
            case "list":
                SendList(invoker, args.Length > 1 ? ChatCommandText.Remainder(rawArgs, 1) : null);
                return true;

            case "show":
                if (args.Length != 2)
                    return false;

                if (!_scriptLibrary.TryGet(args[1], out var shown))
                {
                    ScriptCommandHelper.SendResult(invoker, false, $"No library script named {args[1]}.");
                    return true;
                }

                ScriptCommandHelper.SendSource(invoker, "lib", shown.Name, shown.Source);
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
                    ScriptCommandHelper.SendResult(invoker, false, $"Script too long; at most {ScriptSandbox.MaxSourceLength} characters.");
                    return true;
                }

                invoker.PendingScript.Append(chunk);
                return true;

            case "cancel":
                invoker.PendingScript = null;
                return true;

            case "commit":
                if (args.Length < 2)
                    return false;

                if (invoker.PendingScript is null)
                {
                    ScriptCommandHelper.SendResult(invoker, false, "Nothing to commit; append the script first.");
                    return true;
                }

                var source = PanelTextHelper.Unescape(invoker.PendingScript.ToString());
                invoker.PendingScript = null;

                var description = args.Length > 2 ? PanelTextHelper.Unescape(ChatCommandText.Remainder(rawArgs, 2)) : null;

                _ = CommitAsync(invoker, args[1], source, description);
                return true;

            case "delete":
                if (args.Length != 2)
                    return false;

                if (!_scriptLibrary.TryDelete(args[1], out var deleteError))
                {
                    ScriptCommandHelper.SendResult(invoker, false, deleteError!);
                    return true;
                }

                _chatCommandManager.LogAction(this, invoker, "Delete", null, $"Name: {args[1]}");
                ScriptCommandHelper.SendResult(invoker, true, $"Deleted {args[1]}.");
                SendList(invoker, null);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Saves only if the script compiles with its parameter defaults filled in.
    /// </summary>
    private async Task CommitAsync(Player invoker, string name, string source, string? description)
    {
        var entry = ScriptTemplate.Parse(source, name, description, false);

        if (!entry.TryRender(new System.Collections.Generic.Dictionary<string, string>(), out var rendered, out var renderError))
        {
            ScriptCommandHelper.SendResult(invoker, false, $"Parameter defaults don't fit their types: {renderError}");
            return;
        }

        // Unregistered stand-in, so the check can't affect anything in the zone.
        var (_, compileError) = await ScriptSandbox.CompileAsync(new Npc(invoker.Zone), rendered, $"lib {name}");

        if (compileError is not null)
        {
            ScriptCommandHelper.SendResult(invoker, false, compileError);
            return;
        }

        if (!_scriptLibrary.TrySave(name, source, description ?? entry.Description, invoker.CharacterId, out var saveError))
        {
            ScriptCommandHelper.SendResult(invoker, false, saveError!);
            return;
        }

        _chatCommandManager.LogAction(this, invoker, "Save", null, $"Name: {name}, Length: {source.Length}");
        ScriptCommandHelper.SendResult(invoker, true, $"Saved {name} to the library.");
        SendList(invoker, null);
    }

    private void SendList(Player invoker, string? search)
    {
        ChatHelper.SendGmPanelData(invoker, "scriptlib", "begin");

        foreach (var entry in _scriptLibrary.GetAll(search))
        {
            // entry: name, template flag, description, then one "key:type:default" field per parameter.
            ChatHelper.SendGmPanelData(invoker, "scriptlib",
            [
                "entry",
                entry.Name,
                entry.IsTemplate ? 1 : 0,
                PanelTextHelper.Escape(entry.Description),
                .. entry.Params.Select(param => $"{param.Key}:{param.Type}:{PanelTextHelper.Escape(param.Default)}")
            ]);
        }

        ChatHelper.SendGmPanelData(invoker, "scriptlib", "end");
    }
}
