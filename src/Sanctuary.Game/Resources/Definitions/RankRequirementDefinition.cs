namespace Sanctuary.Game.Resources.Definitions;

public class RankRequirementDefinition
{
    public int Id { get; set; }
    public ProfileType ProfileType { get; set; }
    public RequirementOperator Operator { get; set; }
    public int Rank { get; set; }
}
