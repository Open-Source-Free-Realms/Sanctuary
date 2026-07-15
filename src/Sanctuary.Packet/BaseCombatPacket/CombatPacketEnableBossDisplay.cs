using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op32 sub9 "EnableBossDisplay" — [ulong Guid][bool Enable]. Enable=true registers the actor as a
// BOSS client-side: the overhead boss health display and boss name treatment. False removes it.
public class CombatPacketEnableBossDisplay : ISerializablePacket
{
    public const short OpCode = 32;
    public const short SubOpCode = 9;

    public ulong Guid;
    public bool Enable = true;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);
        writer.Write(SubOpCode);

        writer.Write(Guid);
        writer.Write(Enable);

        return writer.Buffer;
    }
}
