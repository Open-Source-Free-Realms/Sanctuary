using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GroupChangingJobUpdatePacket : BaseGroupPacket, ISerializablePacket
{
    public new const short OpCode = 15;

    public ulong PlayerGuid;

    public bool IsChangingJob;

    public GroupChangingJobUpdatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(PlayerGuid);

        writer.Write(IsChangingJob);

        return writer.Buffer;
    }
}
