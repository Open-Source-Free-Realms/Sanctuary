using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class RewardBundlePacket : BaseRewardPacket, ISerializablePacket
{
    public new const byte OpCode = 1;

    // The coin shop's item-received flow provided the behavioral reference for this packet.
    // Keeping it generic allows collections, combat, quests, and other reward sources to reuse it.
    // Defaults reproduce the runtime-validated single-item reward notification.
    public bool Success = true;
    public int Unknown1;
    public int RewardKind;
    public int Unknown2;
    public int Unknown3 = 3;
    public int Unknown4;
    public int Unknown5;
    public float Multiplier = 1.0f;
    public int Unknown6;
    public int Unknown7;

    public ulong SourceGuid;
    public ulong PlayerGuid;

    public int IconId;
    public int NameId;
    public int Quantity = 1;
    public int Unknown8 = 1;

    public byte EntryType;
    public int EntryIconId;
    public int EntryUnknown;
    public int EntryNameId;
    public int EntryQuantity = 1;
    public int ItemDefinitionId;
    public int Tint;
    public int EntryUnknown2;
    public int EntryUnknown3;
    public byte EntryUnknown4;
    public int ItemGuid;
    public int EntryUnknown5;

    public RewardBundlePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Success);
        writer.Write(Unknown1);
        writer.Write(RewardKind);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(Unknown5);
        writer.Write(Multiplier);
        writer.Write(Unknown6);
        writer.Write(Unknown7);
        writer.Write(SourceGuid);
        writer.Write(PlayerGuid);
        writer.Write(IconId);
        writer.Write(NameId);
        writer.Write(Quantity);
        writer.Write(Unknown8);
        writer.Write(EntryType);
        writer.Write(EntryIconId);
        writer.Write(EntryUnknown);
        writer.Write(EntryNameId);
        writer.Write(EntryQuantity);
        writer.Write(ItemDefinitionId);
        writer.Write(Tint);
        writer.Write(EntryUnknown2);
        writer.Write(EntryUnknown3);
        writer.Write(EntryUnknown4);
        writer.Write(ItemGuid);
        writer.Write(EntryUnknown5);

        return writer.Buffer;
    }
}
