using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// Updates the client with new profile XP (OpCode 38, SubOpCode 14).
public class ClientUpdatePacketUpdateProfileExperience : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 14;

    public int ProfileId;
    public int XpGained;
    public int TotalXpInLevel; // 0-100 percent
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
