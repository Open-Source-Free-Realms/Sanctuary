using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Interactions;

namespace Sanctuary.Game;

public class InteractionManager : IInteractionManager
{
    private ILogger _logger;
    private IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<int, IInteraction> _interactions = [];

    public InteractionManager(ILogger<ResourceManager> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public bool Load()
    {
        var executingAssembly = Assembly.GetExecutingAssembly();

        foreach (var interactionType in executingAssembly.GetTypes())
        {
            if (!interactionType.GetInterfaces().Any(i => i.IsAssignableFrom(typeof(IInteraction))))
                continue;

            var interactionInstance = ActivatorUtilities.CreateInstance(_serviceProvider, interactionType) as IInteraction;

            ArgumentNullException.ThrowIfNull(interactionInstance);

            if (!_interactions.TryAdd(interactionInstance.Id, interactionInstance))
            {
                _logger.LogWarning("Failed to add interaction. {interaction}", interactionInstance.Id);
                return false;
            }
        }

        _logger.LogInformation("Loaded {count} interactions.", _interactions.Count);

        return true;
    }

    public bool TryGet(int id, [MaybeNullWhen(false)] out IInteraction interaction)
    {
        return _interactions.TryGetValue(id, out interaction);
    }
}