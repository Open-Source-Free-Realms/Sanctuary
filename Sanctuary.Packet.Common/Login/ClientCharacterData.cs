using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientCharacterData
{
    public string ServerAddress = null!;
    public string ServerTicket = null!;
    public string CryptoKey = null!;

    public ulong Guid;

    public long Unknown;
    public string Unknown2 = null!;
    public string Unknown3 = null!;
    public string Unknown4 = null!;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(ServerAddress);
        writer.Write(ServerTicket);
        writer.Write(CryptoKey);

        writer.Write(Guid);

        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Unknown3);
        writer.Write(Unknown4);

        return writer.Buffer;
    }
}