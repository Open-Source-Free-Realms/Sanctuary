namespace Sanctuary.Core.IO;

public interface ISerializableType
{
    void Serialize(PacketWriter writer);
}