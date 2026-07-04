using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PlayerUpdatePacketUpdateCharacterState : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 20;

    public ulong Guid;

    public int State;

    public PlayerUpdatePacketUpdateCharacterState() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);

        writer.Write(State);

        return writer.Buffer;
    }
}