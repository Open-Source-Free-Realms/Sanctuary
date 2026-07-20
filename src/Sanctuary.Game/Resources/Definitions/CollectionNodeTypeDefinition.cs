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

    /// <summary>
    /// Selects the drop whose weighted range contains the supplied roll.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The roll is outside the drop table's weighted range.</exception>
    /// <exception cref="InvalidOperationException">The drop table does not contain a matching entry.</exception>
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
