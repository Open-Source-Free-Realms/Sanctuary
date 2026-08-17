using System.Collections.Generic;

using Sanctuary.Core.Collections;

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

    private WeightedDropTable<CollectionNodeDropDefinition>? _table;
    public WeightedDropTable<CollectionNodeDropDefinition> Table => _table ??= new WeightedDropTable<CollectionNodeDropDefinition>(DropTable);
}
