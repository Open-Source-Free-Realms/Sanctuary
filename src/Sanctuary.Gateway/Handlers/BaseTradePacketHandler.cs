using System;
using Microsoft.Extensions.DependencyInjection;
using Sanctuary.Core.IO;
using Sanctuary.Game;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseTradePacketHandler
{
    private static TradeManager _trades = null!;

    public static void ConfigureServices(IServiceProvider services)
    {
        _trades = services.GetRequiredService<TradeManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        if (connection.Player is null || !reader.TryRead(out byte subtype) || !reader.TryRead(out int sessionId))
            return false;

        switch (subtype)
        {
            case 3:
                if (!reader.TryRead(out byte accepted) || accepted > 1 || reader.RemainingLength != 0)
                    return false;
                _trades.Reply(connection.Player, sessionId, accepted == 1);
                return true;
            case 7:
                if (reader.RemainingLength != 0)
                    return false;
                _trades.Cancel(connection.Player, sessionId);
                return true;
            case 10:
                if (!reader.TryRead(out int coins) || reader.RemainingLength != 0)
                    return false;
                _trades.OfferCoins(connection.Player, sessionId, coins);
                return true;
            case 6:
                if (!reader.TryRead(out int acceptedRevision) || reader.RemainingLength != 0)
                    return false;
                _trades.Accept(connection.Player, sessionId, acceptedRevision);
                return true;
            case 8:
                if (!reader.TryRead(out int lockedRevision) || !reader.TryRead(out byte locked) || locked > 1 || reader.RemainingLength != 0)
                    return false;
                _trades.SetLocked(connection.Player, sessionId, lockedRevision, locked == 1);
                return true;
            case 9:
                if (!reader.TryRead(out int itemId) || !reader.TryRead(out int offeredTotal) || reader.RemainingLength != 0)
                    return false;
                _trades.OfferItem(connection.Player, sessionId, itemId, offeredTotal);
                return true;
            default:
                return false;
        }
    }
}
