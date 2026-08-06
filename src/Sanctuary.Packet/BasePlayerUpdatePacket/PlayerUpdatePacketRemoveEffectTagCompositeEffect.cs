using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op35 sub42 "RemoveEffectTagCompositeEffect" — stop a looping composite effect attached via sub41.
// GROUND TRUTH (proto reference sample): [short 35][short 42][ulong Guid][int TagId] (16B). The TagId
// must match the one the AddEffectTagCompositeEffect used.
public class PlayerUpdatePacketRemoveEffectTagCompositeEffect : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 42;

    public ulong Guid;
    public int TagId;

    public PlayerUpdatePacketRemoveEffectTagCompositeEffect() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(TagId);

        return writer.Buffer;
    }
}
