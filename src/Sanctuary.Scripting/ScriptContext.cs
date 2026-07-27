using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Lua;

namespace Sanctuary.Scripting;

public class ScriptContext
{
    private readonly record struct EnvironmentKey(string ScriptFilePath, int Priority) : IComparable<EnvironmentKey>
    {
        public int CompareTo(EnvironmentKey other)
        {
            // Descending priority; tie-break on path so distinct scripts stay unique keys.
            var byPriority = other.Priority.CompareTo(Priority);
            return byPriority != 0 ? byPriority : string.CompareOrdinal(ScriptFilePath, other.ScriptFilePath);
        }
    }

    private readonly ScriptRuntime _runtime;
    private readonly ILogger _logger;
    private readonly LuaTable _rootEnvironment;
    private readonly ConcurrentDictionary<string, ScriptEvent> _events = [];

    /// <summary>
    /// Kept sorted by descending priority on insert, so handlers can be iterated in priority order.
    /// </summary>
    private ImmutableSortedDictionary<EnvironmentKey, LuaTable> _environments =
        ImmutableSortedDictionary.Create<EnvironmentKey, LuaTable>();

    internal ILuaUserData? UserData { get; }

    internal ScriptContext(ScriptRuntime runtime, ILogger logger, LuaTable environment, ILuaUserData? userData = null)
    {
        _runtime = runtime;
        _logger = logger;
        _rootEnvironment = environment;
        UserData = userData;

        // Override `print` to log to our logger instead of stdout.
        _rootEnvironment["print"] = new LuaFunction("print", (context, cancellationToken) =>
        {
            var arguments = context.Arguments;
            var builder = new StringBuilder();

            for (var i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                    builder.Append('\t');

                builder.Append(arguments[i].ToString());
            }

            _logger.LogInformation("[Lua] {Message}", builder.ToString());
            return new ValueTask<int>(0);
        });
    }

    public async ValueTask<bool> LoadScriptAsync(string scriptFilePath)
    {
        const string PriorityAnnotation = "---@priority ";

        var scriptEnv = new LuaTable
        {
            Metatable = new LuaTable()
        };

        // Inherit from the root environment so scripts can share globals and libraries
        scriptEnv.Metatable["__index"] = _rootEnvironment;

        try
        {
            await _runtime.ExecuteFileAsync(scriptFilePath, scriptEnv);

            var scriptPriority = 0;

            // Scan for script priority annotation
            using (var reader = new StreamReader(scriptFilePath))
            {
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();

                    if (line is null)
                        break;

                    if (line.StartsWith(PriorityAnnotation, StringComparison.OrdinalIgnoreCase))
                    {
                        var priorityString = line[PriorityAnnotation.Length..].Trim();

                        if (int.TryParse(priorityString, out var parsedPriority))
                        {
                            scriptPriority = parsedPriority;
                            break;
                        }
                        else
                        {
                            _logger.LogWarning("Invalid priority annotation in script {ScriptFilePath}: {Line}", scriptFilePath, line);
                        }
                    }
                }
            }

            var environmentKey = new EnvironmentKey(scriptFilePath, scriptPriority);
            ImmutableInterlocked.Update(ref _environments, environments => environments.SetItem(environmentKey, scriptEnv));

            // Events need to be rebuilt since the script introduces a new environment
            _events.Clear();
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load script {ScriptFilePath}", scriptFilePath);
            return false;
        }
    }

    public ScriptFunction? GetFunction(string functionName)
    {
        // Highest priority first; return the first script that currently defines the function.
        foreach (var environment in _environments.Values)
        {
            if (environment.TryGetValue(functionName, out var function) && function.Type == LuaValueType.Function)
            {
                return new ScriptFunction(_runtime, _logger, UserData, environment, functionName);
            }
        }

        return null;
    }

    public ScriptEvent GetEvent(string functionName)
    {
        return _events.GetOrAdd(functionName, BuildEvent);
    }

    private ScriptEvent BuildEvent(string functionName)
    {
        var environments = _environments;
        var functions = new ScriptFunction[environments.Count];

        var index = 0;
        foreach (var environment in environments.Values)
        {
            functions[index++] = new ScriptFunction(_runtime, _logger, UserData, environment, functionName);
        }

        return new ScriptEvent(functions);
    }
}