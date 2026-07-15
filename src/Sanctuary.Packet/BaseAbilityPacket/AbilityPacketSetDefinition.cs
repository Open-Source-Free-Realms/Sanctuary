using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op36 sub5 — populates a profile's ability toolbar (the on-screen 1/2/3/4 keys = slots 0-3).
// Wire: [int16 36][int16 5][int32 ProfileId][int32 SlotCount][Slot × SlotCount].
// Slots are POSITIONAL (list index = key position) and the client renders them as a contiguous run
// from slot 0 — an empty slot hides everything after it. ProfileId must be the player's current
// active profile or the packet is ignored.
public class AbilityPacketSetDefinition : BaseAbilityPacket, ISerializablePacket
{
    public new const short OpCode = 5;

    public class Slot
    {
        public int Type = 3;          // 0 empty, 1/3 ability (AbilityDefinition), 2 item
        public int Unknown2;
        public int ManaCost;          // the client greys the slot out while current energy is below this
        public int ItemDefinitionId;  // Type 2 only
        public int IconId;
        public int NameId;
        public int Unknown7 = 4;
        public float Unknown8;
        public int Unknown9 = 1;
        public int AbilityDefinitionId;
        public int Unknown11;
        public bool Unknown12 = true;

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Type);

            if (Type == 0)
                return;

            if (Type == 1 || Type == 3)
            {
                writer.Write(Unknown2);
                writer.Write(ManaCost);
            }
            else if (Type == 2)
            {
                writer.Write(ItemDefinitionId);
            }

            writer.Write(IconId);
            writer.Write(NameId);
            writer.Write(Unknown7);
            writer.Write(Unknown8);
            writer.Write(Unknown9);
            writer.Write(AbilityDefinitionId);
            writer.Write(Unknown11);
            writer.Write(Unknown12);
        }
    }

    public int ProfileId;
    public int SlotCount = 8;
    public List<Slot?> Slots = new();

    public AbilityPacketSetDefinition() : base(OpCode)
    {
    }

    /// <summary>An all-empty toolbar (8 Type-0 slots).</summary>
    public static AbilityPacketSetDefinition CreateEmpty(int profileId)
    {
        return new AbilityPacketSetDefinition { ProfileId = profileId, SlotCount = 8 };
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        writer.Write(ProfileId);
        writer.Write(SlotCount);

        for (var i = 0; i < SlotCount; i++)
        {
            if (i < Slots.Count && Slots[i] is { } slot)
                slot.Serialize(writer);
            else
                writer.Write(0); // empty slot (Type = 0)
        }

        return writer.Buffer;
    }
}
