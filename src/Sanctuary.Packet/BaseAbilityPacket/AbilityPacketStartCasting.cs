using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// BaseAbilityPacket op36/sub3 starts an ability cast.
// The client uses this for cast progress, animation, and composite effect playback.
public class AbilityPacketStartCasting : BaseAbilityPacket, ISerializablePacket
{
    public new const short OpCode = 3;

    public ulong Unknown;            // m_llUnknown   (default = invalid GUID)
    public ulong Unknown2;           // m_llUnknown2  (default = invalid GUID)
    public int CompositeEffectId;    // m_nCompositeEffectId
    public int Animation = -1;       // m_nAnimation  (default -1)
    public int AbilityId;            // m_nAbilityId
    public float ActionTime;         // m_fActionTime (cast duration, seconds)
    public bool HasActionProgress;   // m_bHasActionProgress

    public AbilityPacketStartCasting() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);          // [BaseAbilityPacket.OpCode=36][SubOpCode=3]

        // Provisional field order; verify with the !cast command.
        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(CompositeEffectId);
        writer.Write(Animation);
        writer.Write(AbilityId);
        writer.Write(ActionTime);
        writer.Write(HasActionProgress);

        return writer.Buffer;
    }
}
