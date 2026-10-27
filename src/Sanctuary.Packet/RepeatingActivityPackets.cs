using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Repeating activities (the client's BaseRepeatingActivityPacket family, opcode 0x8F with a byte sub-op):
/// daily things like the coin wheel, tracked by name with a remaining count.
/// </summary>
public static class BaseRepeatingActivityPacket
{
    public const short OpCode = 0x8F;
}

/// <summary>
/// Adds (or updates) an activity on the client. The daily wheel button only sends its start request while
/// the activity named "wheel" has a count above zero. Layout from the client's reader.
/// </summary>
public class RepeatingActivityAddPacket : ISerializablePacket
{
    public const byte SubOpCode = 1;

    public int ActivityId;
    public int CountRemaining;
    public int Unknown;
    /// <summary>
    /// Seconds from now, stored by the client as absolute times (resets).
    /// </summary>
    public int SecondsUntil1;
    public int SecondsUntil2;
    public string Name = string.Empty;
    public string Unknown2 = string.Empty;
    public int Unknown3;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(BaseRepeatingActivityPacket.OpCode);
        writer.Write(SubOpCode);

        writer.Write(ActivityId);
        writer.Write(CountRemaining);
        writer.Write(Unknown);
        writer.Write(SecondsUntil1);
        writer.Write(SecondsUntil2);
        writer.Write(Name);
        writer.Write(Unknown2);
        writer.Write(Unknown3);

        return writer.Buffer;
    }
}
