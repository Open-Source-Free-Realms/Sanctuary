using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class EncounterDetailsInfo : ISerializableType, IDeserializableType
{
    public int GameId;

    public int Unknown2;

    public List<EncounterParticipantGroupInfo> ParticipantGroups { get; } = [];

    public List<EncounterTeamInfo> Teams { get; } = [];

    public int Type;

    public int TeleportEffectId;

    public bool Encounter;

    public bool Unknown3;

    public bool IsInNPXTutorial;

    public int DurationMs;

    public int RespawnTime;

    public MiniGameInfo MiniGame = new();

    public bool Unknown4;

    public bool Unknown5;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(GameId);

        writer.Write(Unknown2);

        writer.Write(ParticipantGroups);

        writer.Write(Teams);

        writer.Write(Type);

        writer.Write(TeleportEffectId);

        writer.Write(Encounter);

        writer.Write(Unknown3);

        writer.Write(IsInNPXTutorial);

        writer.Write(DurationMs);

        writer.Write(RespawnTime);

        MiniGame.Serialize(writer);

        writer.Write(Unknown4);

        writer.Write(Unknown5);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out GameId))
            return false;

        if (!reader.TryRead(out Unknown2))
            return false;

        if (!reader.TryRead(out int participantGroupsCount) || participantGroupsCount < 0 || participantGroupsCount > reader.RemainingLength / 4)
            return false;

        ParticipantGroups.Clear();

        for (var i = 0; i < participantGroupsCount; i++)
        {
            var encounterParticipantGroupInfo = new EncounterParticipantGroupInfo();

            if (!encounterParticipantGroupInfo.TryRead(ref reader))
                return false;

            ParticipantGroups.Add(encounterParticipantGroupInfo);
        }

        if (!reader.TryRead(out int teamsCount) || teamsCount < 0 || teamsCount > reader.RemainingLength / 24)
            return false;

        Teams.Clear();

        for (var i = 0; i < teamsCount; i++)
        {
            var encounterTeamInfo = new EncounterTeamInfo();

            if (!encounterTeamInfo.TryRead(ref reader))
                return false;

            Teams.Add(encounterTeamInfo);
        }

        if (!reader.TryRead(out Type))
            return false;

        if (!reader.TryRead(out TeleportEffectId))
            return false;

        if (!reader.TryRead(out Encounter))
            return false;

        if (!reader.TryRead(out Unknown3))
            return false;

        if (!reader.TryRead(out IsInNPXTutorial))
            return false;

        if (!reader.TryRead(out DurationMs))
            return false;

        if (!reader.TryRead(out RespawnTime))
            return false;

        if (!MiniGame.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out Unknown4))
            return false;

        if (!reader.TryRead(out Unknown5))
            return false;

        return true;
    }
}
