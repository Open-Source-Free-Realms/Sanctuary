using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientUpdatePacketUpdateProfileExperience : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 14;

    public int ProfileId;
    public int Rank;
    public int RankPercent;
    public int StarsAvailable;
    public int StarsEarned;
    public int Unknown6;

    public ClientUpdatePacketUpdateProfileExperience() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ProfileId);
        writer.Write(Rank);
        writer.Write(RankPercent);
        writer.Write(StarsAvailable);
        writer.Write(StarsEarned);
        writer.Write(Unknown6);

        return writer.Buffer;
    }
}
