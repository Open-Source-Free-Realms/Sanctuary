using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ExecuteScriptPacket : BaseUiPacket, ISerializablePacket
{
    public new const byte OpCode = 7;

    public string Script = string.Empty;

    public List<int> Params = new();

    public ExecuteScriptPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Script);

        writer.Write(Params);

        return writer.Buffer;
    }
}