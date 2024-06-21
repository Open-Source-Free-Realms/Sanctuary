using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientUpdatePacketDoneSendingPreloadCharacters : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 26;

    public bool UnloadAllPlayers;

    public ClientUpdatePacketDoneSendingPreloadCharacters() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(UnloadAllPlayers);

        return writer.Buffer;
    }
}