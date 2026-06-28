using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Replaces a player's base model without removing/re-adding the entity (OpCode 35, SubOpCode 49).
/// Used for transformations and appearance changes that preserve camera position.
/// </summary>
public class PlayerUpdatePacketReplaceBaseModel : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 49;

    public ulong Guid;
    public int ModelId;

    public PlayerUpdatePacketReplaceBaseModel() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(ModelId);

        return writer.Buffer;
    }
}
