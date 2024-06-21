using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientGameServerData : ISerializableType
{
    public GameServerData Data = new();

    public bool AllowedAccess;

    public void Serialize(PacketWriter writer)
    {
        Data.Serialize(writer);

        writer.Write(AllowedAccess);
    }
}