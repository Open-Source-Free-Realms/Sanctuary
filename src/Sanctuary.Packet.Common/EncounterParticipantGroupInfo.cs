using System.Collections.Generic;
using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public sealed class EncounterParticipantGroupInfo : ISerializableType, IDeserializableType
{
    public List<KeyValuePair<int, EncounterParticipantInfo>> Participants { get; } = [];

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Participants.Count);

        foreach (var encounterParticipantInfo in Participants)
        {
            writer.Write(encounterParticipantInfo.Key);
            encounterParticipantInfo.Value.Serialize(writer);
        }
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out int participantsCount) || participantsCount < 0 || participantsCount > reader.RemainingLength / 60)
            return false;

        Participants.Clear();

        for (var i = 0; i < participantsCount; i++)
        {
            if (!reader.TryRead(out int key))
                return false;

            var encounterParticipantInfo = new EncounterParticipantInfo();

            if (!encounterParticipantInfo.TryRead(ref reader))
                return false;

            Participants.Add(new KeyValuePair<int, EncounterParticipantInfo>(key, encounterParticipantInfo));
        }

        return true;
    }
}
