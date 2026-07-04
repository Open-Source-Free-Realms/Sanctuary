using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// Sub-opcode 14: ulong Guid + int + bool + int + int + string + int + int + int
///              + int + int + int + int + string + string + int + int + bool + int
public class FishingPacketFishingResult : BaseFishingPacket, ISerializablePacket
{
    public new const short OpCode = 14;

    public ulong Guid;         // @16 target player (must be local player guid)
    public int ResultType;     // @24
    public bool Caught;        // @28 "special/no-fish" flag — false to show size+weight
    public int FishId;         // @32 fish NAME string-table id (localized), NOT the model
    public int Unknown1;       // @36 scoring[1]
    public string? FishName;   // @40 (unused by catch banner)
    public float Unknown2;     // @56 WEIGHT (banner "%2.2f")
    public int Unknown3;       // @60 size selector 1=small 2=med 3=large 4=xlarge
    public float Unknown4;     // @64 scoring[6] (weight/score)
    public int Unknown5;       // @68 scoring[2]
    public int Unknown6;       // @72 scoring[3]
    public int Unknown7;       // @76 scoring[5]
    public int Unknown8;       // @80 show-off held-fish MODEL id — MUST be > 0 or no banner
    public string? UnknownStr1;// @84 held-fish tint alias
    public string? UnknownStr2;// @100 held-fish texture alias
    public int Unknown9;       // @116 show-off sparkle composite-effect id
    public int Unknown10;      // @124
    public bool Unknown11;     // @128
    public int Unknown12;      // @120 show-off size class 1..4 (serialized last)

    public FishingPacketFishingResult() : base(OpCode) { }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();
        Write(writer);
        writer.Write(Guid);
        writer.Write(ResultType);
        writer.Write(Caught);
        writer.Write(FishId);
        writer.Write(Unknown1);
        writer.Write(FishName ?? "");
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(Unknown5);
        writer.Write(Unknown6);
        writer.Write(Unknown7);
        writer.Write(Unknown8);
        writer.Write(UnknownStr1 ?? "");
        writer.Write(UnknownStr2 ?? "");
        writer.Write(Unknown9);
        writer.Write(Unknown10);
        writer.Write(Unknown11);
        writer.Write(Unknown12);
        return writer.Buffer;
    }
}
