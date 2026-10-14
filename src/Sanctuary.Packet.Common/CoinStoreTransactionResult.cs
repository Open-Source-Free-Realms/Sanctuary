namespace Sanctuary.Packet.Common;

public enum CoinStoreTransactionResult
{
    Success = 1,
    InvalidItem = 3,
    InvalidQuantity = 4,
    NotEnoughCoins = 7,
    Error = 8
}
