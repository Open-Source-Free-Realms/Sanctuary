namespace Sanctuary.Packet;

/// <summary>
/// Sent when the player presses Play on the daily wheel (Activity of the Day), if the client has a "wheel"
/// repeating activity with a count left.
/// </summary>
public static class CommandPacketInteractionStartWheel
{
    public const short OpCode = 0xB;
}
