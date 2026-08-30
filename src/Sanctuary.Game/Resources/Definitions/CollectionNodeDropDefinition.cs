using Sanctuary.Core.Collections;

namespace Sanctuary.Game.Resources.Definitions;

public sealed class CollectionNodeDropDefinition : IWeighted
{
    public int ItemDefinitionId { get; set; }
    public int Weight { get; set; } = 1;
}
