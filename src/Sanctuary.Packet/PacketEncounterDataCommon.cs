using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// S2C opcode 62 — the combat RULESET. The client applies it in one shot:
//   CanUseAbilities/CanUseConsumables -> shows/hides the action bar
//   UseCombatCamera + CombatType      -> combat camera + controller
//   ShowHpBar                         -> NPC health bars (mode-wide)
//   ShowCombatUi                      -> combat player/party frames
//   CombatType                        -> BaseClient::InCombat() == true (blocks job changes etc.)
// The server must push combat rules ON at encounter start and push the DEFAULTS back at exit, or
// the client stays in combat forever.
public class PacketEncounterDataCommon : ISerializablePacket
{
    public const short OpCode = 62;

    public bool CombatStance;
    public bool CanTakeDamage;
    public bool CanUseAbilities;
    public bool CanUseConsumables;
    public bool CanMelee;
    public bool CanUseTraits;
    public bool CanChangeMaxMoveSpeed;
    public bool ShowHpBar;
    public bool ShowCombatUi;
    public bool UseCombatCamera;
    public bool CombatType;

    public int GameTimeOverride = -1;
    public float ObjectiveMultiplier = 1f;
    public int QuickCompletionTime = 3600;
    public int QuickCompletionValue;
    public int QuickCompletionLoss;
    public int NpcKOValue;
    public int KnockoutRewardValue;
    public int KnockoutPenalty;

    /// <summary>The combat-encounter ruleset: everything enabled, with the score values a live
    /// combat encounter used (300/NPC, 25000 per knockout remaining, 5000 penalty).</summary>
    public static PacketEncounterDataCommon CreateCombatRules() => new()
    {
        CombatStance = true,
        CanTakeDamage = true,
        CanUseAbilities = true,
        CanUseConsumables = true,
        CanMelee = true,
        CanUseTraits = true,
        CanChangeMaxMoveSpeed = true,
        ShowHpBar = true,
        ShowCombatUi = true,
        UseCombatCamera = true,
        CombatType = true,

        ObjectiveMultiplier = 500f,
        QuickCompletionTime = 1,
        QuickCompletionValue = 100_000,
        QuickCompletionLoss = 500,
        NpcKOValue = 300,
        KnockoutRewardValue = 25_000,
        KnockoutPenalty = 5_000,
    };

    /// <summary>The out-of-encounter defaults (matches the client constructor) — releases combat mode.</summary>
    public static PacketEncounterDataCommon CreateDefault() => new();

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(CombatStance);
        writer.Write(CanTakeDamage);
        writer.Write(CanUseAbilities);
        writer.Write(CanUseConsumables);
        writer.Write(CanMelee);
        writer.Write(CanUseTraits);
        writer.Write(CanChangeMaxMoveSpeed);
        writer.Write(ShowHpBar);
        writer.Write(ShowCombatUi);
        writer.Write(UseCombatCamera);
        writer.Write(CombatType);

        writer.Write(GameTimeOverride);
        writer.Write(ObjectiveMultiplier);
        writer.Write(QuickCompletionTime);
        writer.Write(QuickCompletionValue);
        writer.Write(QuickCompletionLoss);
        writer.Write(NpcKOValue);
        writer.Write(KnockoutRewardValue);
        writer.Write(KnockoutPenalty);

        return writer.Buffer;
    }
}
