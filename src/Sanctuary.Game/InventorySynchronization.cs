namespace Sanctuary.Game;

// One reentrant mutation gate for the current single-gateway architecture.
// Hold from inventory/coin validation through durable write and in-memory reconciliation.
// Always acquire this before any trade session work; TradeManager uses this same gate.
public static class InventorySynchronization
{
    public static readonly object Gate = new();
}
