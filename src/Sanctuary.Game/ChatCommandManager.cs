using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game;

public class ChatCommandManager : IChatCommandManager
{
    private readonly ILogger _logger;
    private readonly ILogger _auditLogger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, IChatCommand> _commands = [];

    public ChatCommandManager(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        _logger = loggerFactory.CreateLogger<ChatCommandManager>();
        _auditLogger = loggerFactory.CreateLogger("Audit");
        _serviceProvider = serviceProvider;
    }

    public string Prefix { get; } = "!";

    public ReadOnlyCollection<IChatCommandView> Commands => _commands.Values.Cast<IChatCommandView>().ToList().AsReadOnly();

    public bool Load()
    {
        var executingAssembly = Assembly.GetExecutingAssembly();

        foreach (var interactionType in executingAssembly.GetTypes())
        {
            if (interactionType.IsAbstract || interactionType.IsInterface)
                continue;

            if (!typeof(IChatCommand).IsAssignableFrom(interactionType))
                continue;

            var commandInstance = ActivatorUtilities.CreateInstance(_serviceProvider, interactionType) as IChatCommand;

            ArgumentNullException.ThrowIfNull(commandInstance);

            if (!_commands.TryAdd(commandInstance.KeyWord.ToLowerInvariant(), commandInstance))
            {
                _logger.LogWarning("Failed to add chat command: {prefix}{command}", Prefix, commandInstance.KeyWord);
                return false;
            }
        }

        _logger.LogInformation("Loaded {count} chat command(s).", _commands.Count);

        return true;
    }

    public bool TryHandle(Player invoker, string command)
    {
        var tokens = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 0)
            return false;

        var keyWord = tokens[0][Prefix.Length..];

        if (!_commands.TryGetValue(keyWord.ToLowerInvariant(), out var cmd))
            return false;

        if (invoker.ChatCommandRole < cmd.RequiredRole)
        {
            ChatHelper.SendSystemMessage(invoker, $"You do not have permission to use this command.");
            return true;
        }

        var args = tokens[1..];

        if (!cmd.Handle(invoker, args))
            ChatHelper.SendSystemMessage(invoker, $"Usage: {Prefix}{cmd.KeyWord} {cmd.Usage}");

        return true;
    }

    public void LogAction(IChatCommand command, Player invoker, string action, string? targetName, string? detail)
    {
        _auditLogger.LogInformation("{Command}|{Action}|Actor: \"{ActorName}\" ({ActorGuid}){Target}{Detail}",
            command.KeyWord,
            action,
            invoker.Name,
            invoker.Guid,
            targetName is null ? string.Empty : $", Target: \"{targetName}\"",
            detail is null ? string.Empty : $", {detail}"
        );
    }
}