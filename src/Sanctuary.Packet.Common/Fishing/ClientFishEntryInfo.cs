using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientFishEntryInfo : ISerializableType
{
    public int Type;

    public int NameId;

    public int IconId;

    public bool Unknown4;
    public bool FishSpecial;

    public int FishLureRequirement;

    public string? Unknown7;

    public int Unknown8;

    public bool FishCatchable;

    public int Unknown10;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Type);
        writer.Write(NameId);
        writer.Write(IconId);

        writer.Write(Unknown4);
        writer.Write(FishSpecial);

        writer.Write(FishLureRequirement);

        writer.Write(Unknown7);

        writer.Write(Unknown8);

        writer.Write(FishCatchable);

        writer.Write(Unknown10);
    }
}