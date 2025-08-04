using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ItemDefinition : BaseItemDefinition
{
    public int ActivatableRecastSeconds { get; set; }

    public float Range { get; set; }

    public int EquipRequirementId { get; set; }

    public int Param1 { get; set; }
    public int Param2 { get; set; }
    public int Param3 { get; set; }
    public int Param4 { get; set; }
    public int Param5 { get; set; }
    public int Param6 { get; set; }
    public int Param7 { get; set; }
    public int Param8 { get; set; }

    public int ItemMaterialType { get; set; }

    public int UseRequirementId { get; set; }

    public int ContentId { get; set; }

    public bool CombatOnly { get; set; }

    public int PreferredItemBarSlot { get; set; }

    public string? StringParam1 { get; set; }

    public class ItemAbilityEntry : ISerializableType
    {
        public int Slot { get; set; }
        public int Id { get; set; }
        public int Unknown { get; set; }
        public int IconId { get; set; }

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Slot);
            writer.Write(Id);
            writer.Write(Unknown);
            writer.Write(IconId);
        }
    }
}