using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketDeclineInvite : BaseHousingPacket, IDeserializable<ClientHousingPacketDeclineInvite>
{
    public new const short OpCode = 16;

    public ulong Guid;

    public ClientHousingPacketDeclineInvite() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketDeclineInvite value)
    {
        value = new ClientHousingPacketDeclineInvite();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.Guid))
            return false;

        return reader.RemainingLength == 0;
    }
}