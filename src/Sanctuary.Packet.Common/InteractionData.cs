using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class InteractionData : ISerializableType
{
    public int EventId;
    public int IconId;
    public int ButtonText;
    public int Unknown4;
    public int Unknown5;
    public int Unknown6;
    public int DescString;
    public int TooltipId;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(EventId);
        writer.Write(IconId);
        writer.Write(ButtonText);
        writer.Write(Unknown4);
        writer.Write(Unknown5);
        writer.Write(Unknown6);
        writer.Write(TooltipId);
    }
}