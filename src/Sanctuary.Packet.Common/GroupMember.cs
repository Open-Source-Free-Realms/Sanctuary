using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class GroupMember : ISerializableType
{
    public ulong Guid;

    public NameData Name = new();

    public int Unknown;

    public int JobId;

    public int Unknown2;

    public int JobIconId = -1;
    public int JobBackgroundIconId = -1;
    public int JobRank = 1;

    public int Unknown3;

    public bool IsChangingJob;
    public bool InMinigame;

    public int MiniGameId;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Guid);

        Name.Serialize(writer);

        writer.Write(Unknown);

        writer.Write(JobId);

        writer.Write(Unknown2);

        writer.Write(JobIconId);
        writer.Write(JobBackgroundIconId);
        writer.Write(JobRank);

        writer.Write(Unknown3);

        writer.Write(IsChangingJob);
        writer.Write(InMinigame);

        writer.Write(MiniGameId);
    }
}
