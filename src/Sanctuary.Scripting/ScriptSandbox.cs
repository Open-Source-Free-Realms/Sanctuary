using System;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Lua;

namespace Sanctuary.Scripting;

/// <summary>
/// A GM-authored script attached to one NPC or prop. It runs in its own environment with a whitelisted
/// standard library (see <see cref="ScriptRuntime.CreateSandboxEnv"/>), and every call is cut off after
/// <see cref="CallTimeout"/> so a runaway script can't stall the zone's runtime.
/// </summary>
/// <remarks>
/// Scripts define optional global hooks: onSpawn(), onSecond(), onInteract(player), onProximity(player)
/// and onChoice(player, hook). The NPC is the global <c>npc</c>.
/// </remarks>
public sealed class ScriptSandbox
{
    public const int MaxSourceLength = 16 * 1024;

    public static readonly TimeSpan CallTimeout = TimeSpan.FromMilliseconds(100);

    private readonly ScriptRuntime _runtime;
    private readonly LuaTable _environment;

    public string Source { get; }

    private ScriptSandbox(ScriptRuntime runtime, LuaTable environment, string source)
    {
        _runtime = runtime;
        _environment = environment;
        Source = source;
    }

    /// <summary>
    /// Compiles and runs the script's top level. Returns the sandbox, or the error to show the GM.
    /// </summary>
    public static async ValueTask<(ScriptSandbox? Sandbox, string? Error)> CompileAsync(IScriptableNpc npc, string source, string chunkName)
    {
        if (source.Length > MaxSourceLength)
            return (null, $"Scripts are at most {MaxSourceLength} characters.");

        var runtime = npc.ScriptRuntime;
        var environment = runtime.CreateSandboxEnv();

        environment["npc"] = new LuaValue(NpcUserData.GetOrCreate(npc));
        environment["print"] = CreatePrint(npc.Logger, chunkName);

        try
        {
            await runtime.ExecuteSourceAsync(source, chunkName, environment, CallTimeout);
        }
        catch (Exception ex) when (IsScriptError(ex))
        {
            return (null, Describe(ex));
        }

        return (new ScriptSandbox(runtime, environment, source), null);
    }

    public bool HasHook(string name) => _environment[name].Type == LuaValueType.Function;

    /// <summary>
    /// Calls a hook if the script defines it. Returns the error, or null if it ran (or isn't defined).
    /// </summary>
    public async ValueTask<string?> CallAsync(string hook, IScriptablePlayer? player = null, string? argument = null)
    {
        var function = _environment[hook];

        if (function.Type != LuaValueType.Function)
            return null;

        LuaValue[] args = (player, argument) switch
        {
            (null, null) => [],
            (not null, null) => [new LuaValue(new PlayerUserData(player))],
            (null, not null) => [argument],
            _ => [new LuaValue(new PlayerUserData(player!)), argument!]
        };

        try
        {
            await _runtime.CallAsync(function, args, CallTimeout);
            return null;
        }
        catch (Exception ex) when (IsScriptError(ex))
        {
            return Describe(ex);
        }
    }

    private static bool IsScriptError(Exception ex) => ex is LuaParseException or LuaCompileException
        or LuaRuntimeException or LuaAssertionException or LuaCanceledException or OperationCanceledException;

    private static string Describe(Exception ex) => ex is LuaCanceledException or OperationCanceledException
        ? $"Stopped after {CallTimeout.TotalMilliseconds} ms (endless loop?)."
        : ex.Message;

    private static LuaFunction CreatePrint(ILogger logger, string chunkName) => new("print", (context, cancellationToken) =>
    {
        var builder = new StringBuilder();

        for (var i = 0; i < context.ArgumentCount; i++)
        {
            if (i > 0)
                builder.Append('\t');

            builder.Append(context.Arguments[i].ToString());
        }

        logger.LogInformation("[Lua {Chunk}] {Message}", chunkName, builder.ToString());
        return new ValueTask<int>(0);
    });
}
