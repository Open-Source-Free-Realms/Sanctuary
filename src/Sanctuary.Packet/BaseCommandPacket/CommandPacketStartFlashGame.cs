using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class CommandPacketStartFlashGame : BaseCommandPacket, ISerializablePacket
{
    public new const short OpCode = 12;

    public string? LuaClass;
    public string? Swf;

    public bool Unknown;

    public CommandPacketStartFlashGame() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(LuaClass);
        writer.Write(Swf);
        writer.Write(Unknown);

        return writer.Buffer;
    }
}