using Sanctuary.Core.IO;

namespace Sanctuary.Game.Resources.Definitions;

public class ItemDefinition : BaseItemDefinition
{
    public int ActivatableRecastSeconds;

    public float Range;

    public int EquipRequirementId;

    public int Param1;
    public int Param2;
    public int Param3;
    public int Param4;
    public int Param5;
    public int Param6;
    public int Param7;
    public int Param8;

    public int ItemMaterialType;

    public int UseRequirementId;

    public int ContentId;

    public bool CombatOnly;

    public int PreferredItemBarSlot;

    public string? StringParam1;

    public class ItemAbilityEntry : ISerializableType
    {
        public int Slot;
        public int Id;
        public int Unknown;
        public int IconId;

        public void Serialize(PacketWriter writer)
        {
            writer.Write(Slot);
            writer.Write(Id);
            writer.Write(Unknown);
            writer.Write(IconId);
        }
    }
}