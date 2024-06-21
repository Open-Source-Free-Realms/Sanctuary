using Sanctuary.Core.IO;

namespace Sanctuary.Game.Resources.Definitions;

public class BaseItemStatDefinition : ISerializableType
{
    public int Id;

    /// <summary>
    /// 0 - Additive
    /// 1 - Multiplicative
    /// </summary>
    public int Type;

    public virtual void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(Type);
    }
}