using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class AbilityPacketExecuteClientLua : BaseAbilityPacket, ISerializablePacket
{
    public new const short OpCode = 17;

    public string Script = string.Empty;
    public float Param1;
    public float Param2;
    public float Param3;

    public AbilityPacketExecuteClientLua() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Script);
        writer.Write(Param1);
        writer.Write(Param2);
        writer.Write(Param3);

        return writer.Buffer;
    }
}
