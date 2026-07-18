using System.Threading;

using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Zones;

namespace Sanctuary.Game.Entities;

public sealed class CollectionNode : Npc
{
    private int _reserved;

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
        Dispose();

        if (Zone is BaseZone zone)
            zone.ScheduleCollectionNodePoolRefill(PoolDefinition.Key, SpawnDefinition.Id);
    }
}
