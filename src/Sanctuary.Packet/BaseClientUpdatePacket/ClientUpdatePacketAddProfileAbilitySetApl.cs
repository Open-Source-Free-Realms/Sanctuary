using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

// BaseClientUpdatePacket op38/sub15 gives a profile its ability set.
// Payload is an AbilityExperienceSet terminated by an entry whose Id is 0.
// Wire format from AbilityExperienceSet::SerializeForClient:
//   repeat { int Id(!=0); bool IsActivateable; int NameId; int DescriptionId; int IconId;
//            int Experience; int Rank; int RankExperience; int RankMaxExperience; int RequiredLevel }
//   then int 0.
// The client is expected to request each ability full definition after this update.
public class ClientUpdatePacketAddProfileAbilitySetApl : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 15;

    public List<AbilityExperience> AbilityExperiences = new();

    public ClientUpdatePacketAddProfileAbilitySetApl() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer); // [BaseClientUpdatePacket.OpCode=38][SubOpCode=15]

        foreach (var abilityExperience in AbilityExperiences)
        {
            abilityExperience.Serialize(writer);

            if (abilityExperience.Unknown == 0)
                return writer.Buffer; // entry was the terminator
        }

        // explicit terminator (Id == 0)
        new AbilityExperience { Unknown = 0 }.Serialize(writer);

        return writer.Buffer;
    }
}
