using System;
using System.Collections.Generic;
using System.Linq;

namespace Sanctuary.Game.Resources.Definitions;

public sealed class CollectionNodeTypeDefinition
{
    public required string Key { get; set; }
    public required string Name { get; set; }
    public int ModelId { get; set; }
    public float Scale { get; set; } = 1f;
    public int CompositeEffectId { get; set; }
    public int InteractRange { get; set; } = 12;
    public byte CursorId { get; set; } = 18;
    public float PlacementYOffset { get; set; }
    public List<CollectionNodeDropDefinition> DropTable { get; set; } = [];

    public int TotalDropWeight => DropTable.Sum(drop => drop.Weight);

    public CollectionNodeDropDefinition SelectDrop(int roll)
    {
        if (roll < 0 || roll >= TotalDropWeight)
            throw new ArgumentOutOfRangeException(nameof(roll));

        foreach (var drop in DropTable)
        {
            if (roll < drop.Weight)
                return drop;

            roll -= drop.Weight;
        }

        throw new InvalidOperationException("The collection node drop table is invalid.");
    }
}
