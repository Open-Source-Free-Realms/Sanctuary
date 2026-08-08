using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op41 sub106 — encounter state machine step. The server walks State through the entry flow (same
// EncounterId/InstanceId as the details packet header): 2 offer shown → 3 → 4 ready → 5 after GO! →
// 6 running.
public class EncounterStatePacket : ISerializablePacket
{
    public const short OpCode = 41;
    public const short SubOpCode = 106;

    public int EncounterId;
    public int InstanceId;
    public int State;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);
        writer.Write(SubOpCode);

        writer.Write(EncounterId);
        writer.Write(InstanceId);
        writer.Write(State);

        return writer.Buffer;
    }
}
