using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class BaseItemStatDefinition : ISerializableType
{
    public int Id { get; set; }

    /// <summary>
    /// 0 - Additive
    /// 1 - Multiplicative
    /// </summary>
    public int Type { get; set; }

    public virtual void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(Type);
    }
}