using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class Ability : ISerializableType
{
    // 0 - Empty
    // 1 - ?
    // 2 - Item
    // 3 - AbilityDefinition
    public int Type;

    // Same value as AbilityDefinitionId when the slot holds an ability.
    public int InstanceId;

    public int ManaCost;

    public int ItemDefinitionId;

    public int IconId;
    public int NameId;

    public int Unknown7;
    public float Range;
    public int Unknown9;

    public int AbilityDefinitionId;

    public int Unknown11;

    public bool ForceDismount;

    public static Ability Empty = new();

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Type);

        if (Type == 0)
            return;

        if (Type == 1 || Type == 3)
        {
            writer.Write(InstanceId);
            writer.Write(ManaCost);
        }
        else if (Type == 2)
        {
            writer.Write(ItemDefinitionId);
        }

        writer.Write(IconId);
        writer.Write(NameId);

        writer.Write(Unknown7);
        writer.Write(Range);
        writer.Write(Unknown9);

        writer.Write(AbilityDefinitionId);

        writer.Write(Unknown11);

        writer.Write(ForceDismount);
    }
}
