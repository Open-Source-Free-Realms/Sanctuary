using System;

namespace Sanctuary.UdpLibrary.Packets;

public abstract class LogicalPacket
{
    public abstract Span<byte> GetDataPtr();
    public abstract int GetDataLen();
    public abstract void SetDataLen(int len);

    public virtual bool IsInternalPacket()
    {
        return false;
    }
}