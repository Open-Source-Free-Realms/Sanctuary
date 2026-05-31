using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class MatchmakingQueueDefinition : ISerializableType
{
    public int Id;

    public int NameId;

    public int MatchType;

    public int MinPlayers;
    public int MaxPlayers;

    public int MinTeams;
    public int MaxTeams;

    public int MaxGameStartDelay;

    public int Param1;
    public int Param2;
    public int Param3;
    public int Param4;
    public int Param5;
    public int Param6;
    public int Param7;

    public int EncounterDescriptionId;

    public int EncounterIcon;

    public int Unknown;
    public int Unknown2;

    public bool MemberOnly;

    public bool Unknown3;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);

        writer.Write(NameId);

        writer.Write(MatchType);

        writer.Write(MinPlayers);
        writer.Write(MaxPlayers);

        writer.Write(MinTeams);
        writer.Write(MaxTeams);

        writer.Write(MaxGameStartDelay);

        writer.Write(Param1);
        writer.Write(Param2);
        writer.Write(Param3);
        writer.Write(Param4);
        writer.Write(Param5);
        writer.Write(Param6);
        writer.Write(Param7);

        writer.Write(EncounterDescriptionId);

        writer.Write(EncounterIcon);

        writer.Write(Unknown);
        writer.Write(Unknown2);

        writer.Write(MemberOnly);

        writer.Write(Unknown3);
    }
}