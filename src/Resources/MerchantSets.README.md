# MerchantSets.json — data provenance

`MerchantSets.json` maps a merchant **set id** (a vendor subtype) to the item ids that subtype
sells. It is read fresh by the gateway on every shop-open and is editable via the WebAPI admin
**Merchants** panel.

## What is real vs. sample

| Set | Subtype | Provenance |
|---|---|---|
| **468** | Chef | **Capture-real** — the exact 25-item ware list recovered byte-for-byte from a live merchant packet (`p12.pcap`). Trustworthy. |
| 469–479 | Blacksmith, Miner, Medic, Archer, Wizard, Warrior, Brawler, Ninja, Postman, Kart, Demolition | **Illustrative samples.** Retail per-subtype ware lists were never captured. These are seeded from thematically-matching item classes so each vendor has plausible, buyable stock — **not** verified retail data. |

Every id in every set is a real, existing item in `ClientItemDefinitions.json` and is buyable, so the
shops all function. The **non-chef sets are placeholders meant to be curated** — either by hand via
the admin panel, or replaced with wiki-sourced canon data (in progress).

**If you are reviewing this for upstream:** treat 469–479 as example seed data, not authoritative
content. Only set 468 is capture-backed.
