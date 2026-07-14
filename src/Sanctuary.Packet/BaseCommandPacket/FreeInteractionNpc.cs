namespace Sanctuary.Packet;

// FreeInteractionNpc (base command 26 / sub 20). Sent by the client when the player
// clicks/uses an already-selected entity — a 4-byte packet (base+sub only, no fields).
// It means "interact with my current selection"; the target guid comes from the preceding
// CommandPacketSelectPlayer (26/19), stored on GatewayConnection.SelectedGuid.
public class FreeInteractionNpc : BaseCommandPacket
{
    public new const short OpCode = 20;

    public FreeInteractionNpc() : base(OpCode)
    {
    }
}
