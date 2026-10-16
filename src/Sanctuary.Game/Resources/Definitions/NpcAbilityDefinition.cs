using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Resources.Definitions;

public class NpcAbilityDefinition
{
    public int BuffIconId { get; set; }
    public int BuffNameId { get; set; }
    public int HitAnimationId { get; set; }
    public int RecoveryAnimationId { get; set; }
    public int RecoveryDelayMs { get; set; }
    public int HitReportTimeoutMs { get; set; }
    public int Slot { get; set; }
    public int DurationMs { get; set; }
    public int PickupAbilityId { get; set; }
    public int PickupEffectId { get; set; }
    public int PickupAnimationId { get; set; }
    public Ability Ability { get; set; } = new();
    public ClientAbilityDefinition Definition { get; set; } = new();
    public ProjectileParameters ProjectileParameters { get; set; } = new();
}
