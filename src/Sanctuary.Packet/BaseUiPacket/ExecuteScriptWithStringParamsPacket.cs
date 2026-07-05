using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ExecuteScriptWithStringParamsPacket : BaseUiPacket, ISerializablePacket
{
    public new const byte OpCode = 8;

    public string Script = string.Empty;

    public List<string> Params = new();

    public ExecuteScriptWithStringParamsPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Script);

        writer.Write(Params.Count);
        foreach (var param in Params)
            writer.Write(param);

        return writer.Buffer;
    }
}
