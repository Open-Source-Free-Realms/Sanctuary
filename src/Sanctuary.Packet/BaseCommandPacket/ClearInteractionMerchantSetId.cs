namespace Sanctuary.Packet;

// ClearInteractionMerchantSetId (base command 26 / sub 43). Sent by the client when the
// merchant/shop window closes — a 4-byte packet (base+sub only, no fields; wire bytes
// 1A002B00), usually sent twice. It is purely a client→server notification: the reference
// SOE server sends nothing back in reply. We consume it so it stops being logged as an
// unhandled packet (twice, at both dispatch layers).
public class ClearInteractionMerchantSetId : BaseCommandPacket
{
    public new const short OpCode = 43;

    public ClearInteractionMerchantSetId() : base(OpCode)
    {
    }
}
