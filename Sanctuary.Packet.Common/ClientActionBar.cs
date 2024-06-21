using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientActionBar : ISerializableType
{
    public int Id;

    public Dictionary<int, ActionBarSlot> Slots = new();

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);

        writer.Write(Slots);
    }
}