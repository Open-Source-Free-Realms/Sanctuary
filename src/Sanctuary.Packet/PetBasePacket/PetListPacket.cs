using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class PetListPacket : PetBasePacket, ISerializablePacket
{
    public new const byte OpCode = 5;

    public List<PetClientInfo> Pets = new();

    public int ActivePetId;

    public ulong ActivePetGuid;

    public PetListPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Pets.Count);

        foreach (var petClientInfo in Pets)
        {
            writer.Write(petClientInfo.Id);
            petClientInfo.Serialize(writer);
        }

        writer.Write(ActivePetId);
        writer.Write(ActivePetGuid);

        return writer.Buffer;
    }
}
