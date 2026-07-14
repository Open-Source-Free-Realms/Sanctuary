# MerchantSets.json — data provenance

`MerchantSets.json` maps a merchant **set id** to the item ids that set sells. It is read fresh by
the gateway on every shop-open and is editable via the WebAPI admin **Merchants** panel.

Which set a spawned merchant NPC uses is resolved **by NPC name** in
[`MerchantNpcSets.json`](MerchantNpcSets.json) (`npcName → setId`), because real FreeRealms
merchants were **one-job-one-tier** — each named vendor sold a specific tier of one job's gear.

## Provenance

| Sets | Contents | Provenance |
|---|---|---|
| **468** | Chef (captured) | **Capture-real** — the 25-item ware list recovered byte-for-byte from a live merchant packet (`p12.pcap`). Kept as the last-resort fallback set. |
| **500–568** | The 69 named job merchants (Archer, Blacksmith, Brawler, Chef, Medic, Miner, Ninja, Postman, Warrior, Wizard — one set per merchant/tier) | **Wiki-sourced.** Each set is a merchant's verbatim `==Sells==` inventory from the FreeRealms Fandom wiki (`Category:Merchant`, community-documented), with item **names resolved to ids** via `ClientItemDefinitions.Comment`. High confidence. |
| **480 / 481** | Kart / Demolition fallback | **Reconstruction (low confidence).** Kart Driver / Demo Derby jobs had **no** canonical coin-shop merchant (their gear was the Marketplace), so these are themed sets built from the matching `Kart Driver` / `Demo Derby` item families. |

**Prices** come from `ClientItemDefinitions.Cost` (authoritative retail price) via
`MerchantStore.CostFor`; `MerchantItems.json` mirrors them and stays admin-editable. Every id in
every set is a real, buyable item, so all shops function.

**Tiering:** items gate on **job level** via each definition's `MinProfileRank` (client-enforced at
equip time). A player can buy any tier but equips only what their job level allows — matching retail.

See [`fr-re/findings/merchant-ware-canon.md`](../../../fr-re/findings/merchant-ware-canon.md) for the
full sourcing method, resolution rate (~98% of real items), and the NPC→merchant name matches.

**Reviewing for upstream:** sets 500–568 are wiki-verified canon; 468 is capture-backed; 480/481 are
flagged reconstructions.
