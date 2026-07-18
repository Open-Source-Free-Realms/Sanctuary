using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

// op36 sub5 — populates a profile's ability toolbar (the on-screen 1/2/3/4 keys = slots 0-3).
// Wire: [int16 36][int16 5][int32 ProfileId][int32 SlotCount][Slot × SlotCount].
// Slots are POSITIONAL (list index = key position) and the client renders them as a contiguous run
// from slot 0 — an empty slot hides everything after it. ProfileId must be the player's current
// active profile or the packet is ignored.
public class AbilityPacketSetDefinition : BaseAbilityPacket, ISerializablePacket
{
    public new const short OpCode = 5;

    public int ProfileId;

    public AbilitySet AbilitySet = new();

    public AbilityPacketSetDefinition() : base(OpCode)
    {
    }

    /// <summary>An all-empty toolbar (8 Type-0 slots).</summary>
    public static AbilityPacketSetDefinition CreateEmpty(int profileId)
    {
        return new AbilityPacketSetDefinition
        {
            ProfileId = profileId
        };
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ProfileId);

        AbilitySet.Serialize(writer);

        return writer.Buffer;
    }
}