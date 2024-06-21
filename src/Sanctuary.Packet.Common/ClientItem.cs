using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientItem : ItemInstance
{
    public bool ActivateEnabled;

    public override void Serialize(PacketWriter writer)
    {
        base.Serialize(writer);

        writer.Write(ActivateEnabled);
    }
}