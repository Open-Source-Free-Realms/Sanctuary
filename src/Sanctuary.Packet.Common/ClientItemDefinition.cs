using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientItemDefinition : BaseItemDefinition
{
    public int ResellValue { get; set; }

    // ModelId
    // Player Customization Equipment Slot (sub_BD5980)
    public int Param1 { get; set; }

    /// <summary>
    /// Used in PlayerCustomization (sub_BD5980)
    /// </summary>
    public int Param2 { get; set; }

    public int Unknown5 { get; set; }

    public Dictionary<int, ClientItemStatDefinition> Stats { get; set; } = new();

    public List<ItemDefinition.ItemAbilityEntry> Abilities { get; set; } = new();

    public ClientItemDefinition()
    {
    }

    public override void Serialize(PacketWriter writer)
    {
        base.Serialize(writer);

        writer.Write(ResellValue);

        writer.Write(Param1);
        writer.Write(Param2);
        writer.Write(Unknown5);

        writer.Write(Stats);

        writer.Write(Abilities);
    }

    public ClientItemDefinition Clone() => (ClientItemDefinition)MemberwiseClone();

    public int GetMemberPurchasePrice()
    {
        if (Cost > 0 && (MemberDiscount - 1) <= 99)
        {
            var discountFactor = MemberDiscount * 0.01f;
            var discountMultiplier = 1.0f - discountFactor;
            var discountedCost = discountMultiplier * Cost;

            return (int)discountedCost;
        }

        return Cost;
    }
}