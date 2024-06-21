using System;

namespace Sanctuary.UdpLibrary.Packets;

public class FixedLogicalPacket : LogicalPacket
{
    private byte[] Data;
    private int DataLen;

    public FixedLogicalPacket(int quickSize, ReadOnlySpan<byte> data, int dataLen)
    {
        Data = new byte[quickSize];
        DataLen = dataLen;

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