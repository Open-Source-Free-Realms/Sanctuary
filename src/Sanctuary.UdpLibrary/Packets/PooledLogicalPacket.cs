using System;

namespace Sanctuary.UdpLibrary.Packets;

public class PooledLogicalPacket : LogicalPacket
{
    private byte[] Data;
    private int DataLen;
    private int MaxDataLen;

    public PooledLogicalPacket(int len)
    {
        MaxDataLen = len;
        Data = new byte[MaxDataLen];
        DataLen = 0;
    }

    public override Span<byte> GetDataPtr()
    {
        return Data;
    }

    public override int GetDataLen()
    {
        return DataLen;
    }

    public override void SetDataLen(int len)
    {
        DataLen = len;
    }

    public void SetData(Span<byte> data, int dataLen, Span<byte> data2, int dataLen2)
    {
        DataLen = dataLen + dataLen2;

        var dataPtr = Data.AsSpan();

        if (!data.IsEmpty)
            data.Slice(0, dataLen).CopyTo(dataPtr);

        if (!data2.IsEmpty)
            data2.Slice(0, dataLen2).CopyTo(dataPtr.Slice(dataLen));
    }
}