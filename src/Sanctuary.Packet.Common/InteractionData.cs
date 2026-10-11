using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class InteractionData : ISerializableType, IDeserializableType
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

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out Id))
            return false;

        if (!reader.TryRead(out IconId))
            return false;

        if (!reader.TryRead(out ButtonText))
            return false;

        if (!reader.TryRead(out Type))
            return false;

        if (!reader.TryRead(out Param1))
            return false;

        if (!reader.TryRead(out Param2))
            return false;

        if (!reader.TryRead(out TooltipId))
            return false;

        return true;
    }
}