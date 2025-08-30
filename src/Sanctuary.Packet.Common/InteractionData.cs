using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class InteractionData : ISerializableType
{
    public int Id;

    public int IconId;
    public int ButtonText;
    public int Type;
    public int Param1;
    public int Param2;
    public int DescString;
    public int TooltipId;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(IconId);
        writer.Write(ButtonText);
        writer.Write(Type);
        writer.Write(Param1);
        writer.Write(Param2);
        writer.Write(TooltipId);
    }
}