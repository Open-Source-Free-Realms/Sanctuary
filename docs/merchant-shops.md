# Vendor merchant shops — protocol & design

How the in-world merchant NPCs work: click a vendor, a shop window opens bound to that NPC, and you
buy (and sell back) items for coins. This document is the reference for the packets and data the
feature uses.

## Reading the opcode notation

Application packets in this protocol have a **two-level opcode**: a *base* opcode that selects a
packet family, then a *sub* opcode that selects the specific message within it. Throughout the code
and this doc they're written **`base/sub`**. The families this feature touches:

| Base | Family | Class |
|---|---|---|
| **26** | Command packets (targeting, interaction menus) | `BaseCommandPacket` |
| **165** | Coin-store packets (buy / sell / merchant window) | `BaseCoinStorePacket` |
| **35** | Player-update packets (spawns, notifications) | `BasePlayerUpdatePacket` |

So, for example, `165/10` = base **165** (coin store) / sub **10** — the merchant-window packet.

## Opening a shop (the click → menu → window flow)

Clicking an NPC is a two-message gesture, then the menu selection is a third. None of the menu
packets are new — merchants **reuse the same interaction packets the game already uses for
player-to-player menus**, so there's no guessed wire format on the menu path.

```
  Player clicks a merchant NPC
        |
   [C→S] CommandPacketSelectPlayer  (26/19)   "I selected this entity"  → server stores the guid
        |                                                                  on GatewayConnection.SelectedGuid
   [C→S] FreeInteractionNpc         (26/20)   "interact with my selection" (empty packet, no guid)
        |
        v  server resolves SelectedGuid → the NPC → Npc.OnInteract(player)
   [S→C] CommandPacketInteractionList (26/9)  the interaction menu — for a merchant, one button:
        |                                     "Merchant" (interaction Type 17)
        |
   Player clicks the "Merchant" button
        |
   [C→S] CommandPacketInteractionSelect (26/10)  "I picked interaction N"  → ShopInteraction runs
        |
   [S→C] PlayerUpdatePacketItemDefinitions (35/…)  the wares' name/icon/price definitions
   [S→C] CoinStoreMerchantListPacket       (165/10) opens the shop window bound to this NPC
        |
   [C→S] ClearInteractionMerchantSetId    (26/43)  sent when the window is closed
```

Two small correctness notes baked into the handlers:
- After a menu choice, the server sends an **empty** `CommandPacketInteractionList` (26/9) to dismiss
  the on-screen menu. Otherwise the "Merchant" button lingers on top of the window's close (X), so
  closing re-clicks the button and reopens the shop.
- `SelectedGuid` is cleared once the interaction resolves. The client folds a periodic "interact with
  selection" poll into its idle traffic; if the selection still pointed at the merchant, that poll
  would re-open the menu on every tick.

## Making items buyable

The client only shows a working **Buy** control for items it knows as coin-store entries. So at login
the server registers every merchant ware into the coin-store catalog as a dynamic item
(`StartingZone.SendCoinStoreItemList`). Without this the window still renders (name/icon/price come
from the pushed item definitions), but the quantity stepper stays blank and Buy does nothing.

- **Buy** flows through the existing coin-store *sell-to-client* handler. It now also accepts **recipe
  items** (item `Type 5`, e.g. cooking recipes) in addition to equipment and consumables — job
  merchants sell recipes, which were previously rejected.
- **Sell back** (re-selling a purchased item to the merchant) is `CoinStoreBuyBackRequestPacket`
  (`165/12`), answered with `CoinStoreBuyBackResponsePacket` (`165/13`).

## The "coin" marker over vendors

Each vendor floats a coin marker so players can tell it's a shop without clicking. That marker is a
`NotificationInfo` sent by the existing `PlayerUpdatePacketAddNotifications` (`35/10`) path when the
NPC becomes visible. The specific field values (`NotificationType 1`, `IconId 1`, `IconState 12`,
`ReferenceId 3227` = the "Merchant" label) were recovered from live packet captures, identical across
several captures and vendors.

## Data model

Which NPCs are merchants, what they sell, and for how much:

- **`BaseZone.TryCreateNpc`** tags an NPC as a merchant when its model name contains `merchant`
  (e.g. `human_m_merchant_blacksmith_african.agr`), assigns it a *merchant set id* from the subtype,
  gives it the `ShopInteraction`, and sets the coin marker.
- **`Resources/MerchantSets.json`** — `setId → [item ids]`: what each vendor subtype sells.
- **`Resources/MerchantItems.json`** — `[{ id, cost }]`: the flat coin cost of each ware. Seeded on
  first boot from each item's base cost; both files are **read fresh on every shop-open**, so cost
  edits take effect without a restart.

> **Data provenance:** only set **468 (chef)** is a real capture — its 25-item ware list was recovered
> byte-for-byte from a live merchant packet. The other subtype sets (blacksmith, miner, …) are
> **illustrative samples** seeded from thematically-matching items so every vendor has plausible stock;
> they are placeholders to be curated, not verified retail data. See `Resources/MerchantSets.README.md`.
