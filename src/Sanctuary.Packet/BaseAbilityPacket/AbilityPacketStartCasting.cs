using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// op36 sub3 (S2C) — begins an ability cast on a character: plays the animation and composite effect,
// and locks the pressed action-bar slot for ActionTime seconds (with an optional progress bar).
// The proven way to play a cast/attack animation on the player.
public class AbilityPacketStartCasting : BaseAbilityPacket, ISerializablePacket
{
    public new const short OpCode = 3;

    public ulong Unknown;            // caster guid
    public ulong Unknown2;           // target guid
    public int CompositeEffectId;    // FX on the caster during the cast
    public int Animation = -1;       // animation group id (-1 = none)
    public int AbilityId;
    public float ActionTime;         // cast duration, seconds — the client's slot-lock window
    public bool HasActionProgress;   // true = render a progress (cast) bar

    public AbilityPacketStartCasting() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

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
