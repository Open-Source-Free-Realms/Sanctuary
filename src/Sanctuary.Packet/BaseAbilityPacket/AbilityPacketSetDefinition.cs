using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// BaseAbilityPacket op36/sub5 populates a profile ability toolbar.
// Wire format from AbilitySet::SerializeForClient and Ability::sub_8E6760:
//   [op36][sub5][int ProfileId][int Count][Slot * Count]
// Slot (Ability::sub_8E6760): int Type; if Type!=0: (Type 1/3 -> int Unknown2, int ManaCost; Type 2 ->
//   int ItemDefinitionId) then int IconId, int NameId, int Unknown7, float Unknown8, int Unknown9,
//   int AbilityDefinitionId, int Unknown11, bool Unknown12. Type 0 = empty slot.
// The captured Ninja set uses 8 slots: 2 populated slots followed by 6 empty slots.
public class AbilityPacketSetDefinition : BaseAbilityPacket, ISerializablePacket
{
    public new const short OpCode = 5;

    public class Slot
    {
        public int Type = 3;          // 0 empty, 1/3 ability (AbilityDefinition), 2 item
        public int Unknown2;          // capture: == AbilityDefinitionId
        public int ManaCost;
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

    // Slot 0 is common melee; slot 1 is the equipped weapon special.
    // SlotCount stays 8 to match the client-accepted packet shape.
    public static AbilityPacketSetDefinition CreateEmpty(int profileId)
    {
        return new AbilityPacketSetDefinition { ProfileId = profileId, SlotCount = 8 };
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer); // [op36][sub5]

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
