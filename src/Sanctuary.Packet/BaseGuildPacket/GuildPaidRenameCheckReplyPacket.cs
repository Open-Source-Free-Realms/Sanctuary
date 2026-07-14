using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class GuildPaidRenameCheckReplyPacket : BaseGuildPacket, ISerializablePacket
{
    public new const byte OpCode = 30;

    public ulong Guid;
    public string? Name;

    // 1 - NotLeader
    // 2 - Invalid
    // 3 - Taken
    // 4 - AlreadyPendingRequest
    // 5 - Success
    public int Result;

    public GuildPaidRenameCheckReplyPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Name);

        writer.Write(Result);

        return writer.Buffer;
    }
}