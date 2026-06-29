using Sanctuary.Core.IO;

using System;
namespace Sanctuary.Packet;

public class PlayerUpdatePacketUpdateTemporaryAppearance : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 14;

    public ulong Guid;

    public int TemporaryAppearance;

    public PlayerUpdatePacketUpdateTemporaryAppearance() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(TemporaryAppearance);

        writer.Write(Guid);

        return writer.Buffer;
    }
}
