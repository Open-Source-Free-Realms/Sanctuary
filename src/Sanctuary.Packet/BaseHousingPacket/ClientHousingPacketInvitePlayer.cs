using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientHousingPacketInvitePlayer : BaseHousingPacket, IDeserializable<ClientHousingPacketInvitePlayer>
{
    public new const short OpCode = 15;

    public ulong InviteeGuid;
    public ulong Unknown;

    public ClientHousingPacketInvitePlayer() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ClientHousingPacketInvitePlayer value)
    {
        value = new ClientHousingPacketInvitePlayer();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.InviteeGuid))
            return false;

        if (!reader.TryRead(out value.Unknown))
            return false;

        return reader.RemainingLength == 0;
    }
}