using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class WallOfDataUIEventPacket : WallOfDataBasePacket, IDeserializable<WallOfDataUIEventPacket>
{
    public new const byte OpCode = 4;

    public string? TableName;
    public string? Callback;
    public string? Param;

    public WallOfDataUIEventPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out WallOfDataUIEventPacket value)
    {
        value = new WallOfDataUIEventPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.TableName))
            return false;

        if (!reader.TryRead(out value.Callback))
            return false;

        if (!reader.TryRead(out value.Param))
            return false;

        return reader.RemainingLength == 0;
    }

    public override string ToString()
    {
        return $"TableName: {TableName}, Callback: {Callback}, Param: {Param}";
    }
}