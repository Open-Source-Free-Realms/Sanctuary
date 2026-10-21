using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

public class RequirementGroupDefinition
{
    public int Id { get; set; }
    public List<RankRequirementDefinition> Requirements { get; set; } = [];
}
