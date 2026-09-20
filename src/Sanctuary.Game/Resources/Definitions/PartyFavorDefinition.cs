namespace Sanctuary.Game.Resources.Definitions;

public class PartyFavorDefinition
{
    public int ItemId { get; set; }
    public int EffectId { get; set; }
    public int AnimationId { get; set; } = 3351; // emo_spraycan
    public float GestureSeconds { get; set; } = 1.5f;
    public float EffectSeconds { get; set; } = 20f;
    public float Range { get; set; } = 12f;
    public int CooldownMs { get; set; } = 3000;
}
