using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientUpdatePacketDoneSendingPreloadCharacters : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 26;

    public bool Preload;

    public ClientUpdatePacketDoneSendingPreloadCharacters() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Preload);

        return writer.Buffer;
    }
}