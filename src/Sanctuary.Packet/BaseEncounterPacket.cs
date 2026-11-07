using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseEncounterPacket
{
    public const short OpCode = 41;

    private short SubOpCode;

    public int MiniGameStateId;
    public int EncounterDefinitionId;

    public BaseEncounterPacket(short subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
        writer.Write(MiniGameStateId);
        writer.Write(EncounterDefinitionId);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out short opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out short subOpCode) || subOpCode != SubOpCode)
            return false;

        if (!reader.TryRead(out MiniGameStateId))
            return false;

        if (!reader.TryRead(out EncounterDefinitionId))
            return false;

        return true;
    }
}