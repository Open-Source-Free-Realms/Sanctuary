using System;

namespace Sanctuary.UdpLibrary.Packets;

public class SimpleLogicalPacket : LogicalPacket
{
    private byte[] Data;
    private int DataLen;

    public SimpleLogicalPacket(ReadOnlySpan<byte> data, int dataLen)
    {
        DataLen = dataLen;
        Data = new byte[dataLen];

        if (!data.IsEmpty)
            data.Slice(0, DataLen).CopyTo(Data);
    }

    public override int GetDataLen()
    {
        return DataLen;
    }

    public override Span<byte> GetDataPtr()
    {
        return Data;
    }

    public override void SetDataLen(int len)
    {
        DataLen = len;
    }
}