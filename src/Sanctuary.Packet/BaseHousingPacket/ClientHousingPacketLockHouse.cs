using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketLockHouse : BaseHousingPacket, IDeserializable<ClientHousingPacketLockHouse>
{
    public new const short OpCode = 22;

    public ulong HouseGuid;
    public bool IsLocked;

    public ClientHousingPacketLockHouse() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketLockHouse value)
    {
        value = new ClientHousingPacketLockHouse();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.HouseGuid))
            return false;

        if (!reader.TryRead(out value.IsLocked))
            return false;

        return reader.RemainingLength == 0;
    }
}