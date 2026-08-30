using Sanctuary.Core.Collections;

namespace Sanctuary.Game.Resources.Definitions;

public sealed class TintDropDefinition : IWeighted
{
    public int TintId { get; set; }
    public int Weight { get; set; } = 1;
}
