using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class PacketInGamePurchasePlaceOrderResponse : PacketBaseInGamePurchase, ISerializablePacket
{
    public new const short OpCode = 4;

    public int OrderTrackingId;

    // 2, 4, 6, 7 - marketplace_error_order_failure
    // 5 - marketplace_error_balance_not_enough
    // 8 - marketplace_error_invalid_coupon
    // X - marketplace_error_request_unavailable
    public InGamePurchaseOrderResult Result;

    public string? OrderId;

    public int Discount;

    public int Total;

    public PacketInGamePurchasePlaceOrderResponse() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(OrderTrackingId);

        writer.Write((int)Result);

        writer.Write(OrderId);

        writer.Write(Discount);
        writer.Write(Total);

        return writer.Buffer;
    }
}