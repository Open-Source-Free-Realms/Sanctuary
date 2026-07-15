using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Minigame goal state — opcode 45, header [int16 45][byte subOpCode]. A MiniGameState must exist
// client-side (created by the launch details packet) and goals must be DEFINED inline in that
// packet's ObjectiveData[]: the client drops op45 packets for goal ids it doesn't already know.
// sub1 activates a goal, sub2 ticks progress, sub3 completes it.
public static class ObjectivePacketWriter
{
    public const short OpCode = 45;
}

/// <summary>Sub 1 — (re)activate a goal: sets its Total and fires the "New Objective" announce.</summary>
public class ObjectiveActivatePacket : ISerializablePacket
{
    public const byte SubOpCode = 1;

    public int ObjectiveId;
    public int Total;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(ObjectivePacketWriter.OpCode);
        writer.Write(SubOpCode);
        writer.Write(ObjectiveId);
        writer.Write(Total);

        return writer.Buffer;
    }
}

/// <summary>Sub 3 — complete a goal: fires the green-check "Goal Complete!" announce for the id.</summary>
public class ObjectiveCompletePacket : ISerializablePacket
{
    public const byte SubOpCode = 3;

    public int ObjectiveId;
    public int Unknown;
    public int Unknown2 = 5000;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(ObjectivePacketWriter.OpCode);
        writer.Write(SubOpCode);
        writer.Write(ObjectiveId);
        writer.Write(Unknown);
        writer.Write(Unknown2);

        return writer.Buffer;
    }
}

/// <summary>Sub 2 — progress tick: sets the goal's current Count.</summary>
public class ObjectiveUpdatePacket : ISerializablePacket
{
    public const byte SubOpCode = 2;

    public int ObjectiveId;
    public int Count;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(ObjectivePacketWriter.OpCode);
        writer.Write(SubOpCode);
        writer.Write(ObjectiveId);
        writer.Write(Count);

        return writer.Buffer;
    }
}
