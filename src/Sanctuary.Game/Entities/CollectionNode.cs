using System.Collections.Generic;
using System.Threading;

using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.Entities;

public sealed class CollectionNode : Npc
{
    private int _reserved;
    private readonly List<Npc> _effectAnchors = [];

    public CollectionNodeTypeDefinition TypeDefinition { get; }
    public CollectionNodePoolDefinition PoolDefinition { get; }
    public CollectionNodeSpawnDefinition SpawnDefinition { get; }

    public CollectionNode(IZone zone, CollectionNodeTypeDefinition typeDefinition, CollectionNodePoolDefinition poolDefinition,
        CollectionNodeSpawnDefinition spawnDefinition)
        : base(zone)
    {
        TypeDefinition = typeDefinition;
        PoolDefinition = poolDefinition;
        SpawnDefinition = spawnDefinition;
    }

    public bool TryReserve()
    {
        return Interlocked.CompareExchange(ref _reserved, 1, 0) == 0;
    }

    public void Release()
    {
        Interlocked.Exchange(ref _reserved, 0);
    }

    public void CompleteCollection()
    {
        Zone.CompleteCollectionNode(this);
    }

    internal void AddEffectAnchor(Npc anchor)
    {
        _effectAnchors.Add(anchor);
    }

    internal void DisposeAfterCollection()
    {
        DisposeEffectAnchors();
        DisposeGracefully(
            animate: true,
            delay: 0,
            effectDelay: 0,
            compositeEffectId: 0,
            duration: 1000);
    }

    public override void Dispose()
    {
        DisposeEffectAnchors();
        base.Dispose();
    }

    private void DisposeEffectAnchors()
    {
        foreach (var anchor in _effectAnchors)
            anchor.Dispose();

        _effectAnchors.Clear();
    }
}
