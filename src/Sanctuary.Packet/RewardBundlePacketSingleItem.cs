using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Opcode 50 (RewardBundle), sub-type 2 = single item. Shows the yellow "You received: X x N"
/// notification at the bottom of the screen (client ClientRewardManager -> "ItemReceived" GUI event).
///
/// This is DISPLAY-ONLY: it does a read-only item-definition lookup for the name/icon/tint and fires
/// the notification. The item itself must still be granted separately (ClientUpdatePacketItemAdd).
/// Wire layout reverse-engineered from the client (sub_B8A640 / sub_B891F0) — see FISHING_RE_NOTES.md.
/// </summary>
public class RewardBundlePacketSingleItem : ISerializablePacket
{
    public const short OpCode = 50;
    private const byte RewardTypeSingleItem = 2;

    public int ItemDefinitionId;
    public int IconId; // 0 = use the item's default icon
    public int TintId; // 0 = use the item's default tint
    public int Count = 1;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);              // int16 = 50 (re-read by the outer dispatcher to route here)
        writer.Write(RewardTypeSingleItem);// uint8 = 2 (single-item sub-type)
        writer.Write(ItemDefinitionId);    // item definition id (drives the notification name/icon/tint)
        writer.Write(ItemDefinitionId);    // second id — only used to build a discarded string; mirror it
        writer.Write(IconId);              // IconData.m_nId  (0 = default)
        writer.Write(TintId);              // IconData.m_nTintId (0 = default)
        writer.Write(Count);               // quantity ("x N")
        writer.Write(0);                   // trailing dword — unused but must be present (27 bytes total)

        return writer.Buffer;
    }
}
