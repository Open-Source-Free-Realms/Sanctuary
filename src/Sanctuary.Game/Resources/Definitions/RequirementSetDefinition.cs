using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

public class RequirementSetDefinition
{
    public int Id { get; set; }
    public List<RequirementGroupDefinition> Groups { get; set; } = [];
}
