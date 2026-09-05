namespace Sanctuary.Game.Resources.Definitions.Combat;

public sealed class AbilityDefinition
{
    public int Id { get; set; }

    public string Comment { get; set; } = string.Empty;

    public string EffectType { get; set; } = "SweepDamage";

    public int Damage { get; set; }
    public int HitCount { get; set; }
    public float AoeRadius { get; set; }
    public int EnergyCost { get; set; }

    public int AnimationId { get; set; }
    public int HitEffectId { get; set; }
    public int CastEffectId { get; set; }
    public int CasterEndEffectId { get; set; }
    public int EnemyExtraEffectId { get; set; }

    public int WeaponEffectId { get; set; }
    public int WeaponEffectDurationMs { get; set; } = 10000;

    public int TargetAnimationId { get; set; }
    public int TargetEffectDurationMs { get; set; }
    public int ContactEffectId { get; set; }

    public int NameId { get; set; }
    public int DescriptionId { get; set; }
    public int IconId { get; set; }
}
