using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class ClientUpdatePacketUpdateProfileExperience : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 14;

    public int ProfileId;
    public int XpGained;
    public int TotalXpInLevel;
    public int CurrentLevel;

    public ClientUpdatePacketUpdateProfileExperience() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ProfileId);
        writer.Write(XpGained);
        writer.Write(TotalXpInLevel);
        writer.Write(CurrentLevel);

        return writer.Buffer;
    }
}
