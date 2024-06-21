using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class CommandPacketInteractionList : BaseCommandPacket, ISerializablePacket
{
    public new const short OpCode = 9;

    public InteractionList List = new();

    public bool Unknown;

    public CommandPacketInteractionList() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        List.Serialize(writer);

        writer.Write(Unknown);

        return writer.Buffer;
    }
}