using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class NameChangeInfo : ISerializableType
{
    public ulong Guid;

    public NameChangeType Type;

    public int Result;

    public string Name = null!;
    public string Status = null!;

    public virtual void Serialize(PacketWriter writer)
    {
        writer.Write(Type);

        writer.Write(Guid);

        writer.Write(Result);

        writer.Write(Name);
        writer.Write(Status);
    }
}