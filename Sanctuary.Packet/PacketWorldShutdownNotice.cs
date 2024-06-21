using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class PacketWorldShutdownNotice : ISerializablePacket
{
    public const short OpCode = 92;

    /// <summary>
    /// Time remaining in seconds.
    /// </summary>
    public int TimeRemaining;

    public string Reason = null!;
    public int ReasonId;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);

        writer.Write(TimeRemaining);

        writer.Write(Reason);
        writer.Write(ReasonId);

        return writer.Buffer;
    }
}