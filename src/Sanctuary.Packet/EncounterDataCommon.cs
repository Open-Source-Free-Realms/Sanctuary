using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class EncounterDataCommon : ISerializablePacket, IDeserializable<EncounterDataCommon>
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

    public int GameTimeOverride;

    public float ObjectiveMultiplier;

    public int QuickCompletionTime;

    public int QuickCompletionValue;

    public int QuickCompletionLoss;

    public int NpcKOValue;

    public int KnockoutRewardValue;

    public int KnockoutPenalty;

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

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out EncounterDataCommon encounterDataCommon)
    {
        encounterDataCommon = new EncounterDataCommon();

        var reader = new PacketReader(data);

        if (!reader.TryRead(out short opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out encounterDataCommon.CombatStance))
            return false;

        if (!reader.TryRead(out encounterDataCommon.CanTakeDamage))
            return false;

        if (!reader.TryRead(out encounterDataCommon.CanUseAbilities))
            return false;

        if (!reader.TryRead(out encounterDataCommon.CanUseConsumables))
            return false;

        if (!reader.TryRead(out encounterDataCommon.CanMelee))
            return false;

        if (!reader.TryRead(out encounterDataCommon.CanUseTraits))
            return false;

        if (!reader.TryRead(out encounterDataCommon.CanChangeMaxMoveSpeed))
            return false;

        if (!reader.TryRead(out encounterDataCommon.ShowHpBar))
            return false;

        if (!reader.TryRead(out encounterDataCommon.ShowCombatUi))
            return false;

        if (!reader.TryRead(out encounterDataCommon.UseCombatCamera))
            return false;

        if (!reader.TryRead(out encounterDataCommon.CombatType))
            return false;

        if (!reader.TryRead(out encounterDataCommon.GameTimeOverride))
            return false;

        if (!reader.TryRead(out encounterDataCommon.ObjectiveMultiplier))
            return false;

        if (!reader.TryRead(out encounterDataCommon.QuickCompletionTime))
            return false;

        if (!reader.TryRead(out encounterDataCommon.QuickCompletionValue))
            return false;

        if (!reader.TryRead(out encounterDataCommon.QuickCompletionLoss))
            return false;

        if (!reader.TryRead(out encounterDataCommon.NpcKOValue))
            return false;

        if (!reader.TryRead(out encounterDataCommon.KnockoutRewardValue))
            return false;

        if (!reader.TryRead(out encounterDataCommon.KnockoutPenalty))
            return false;

        return reader.RemainingLength == 0;
    }
}
