# Free Realms Fishing — Wiki reference (freerealms.fandom.com)

Captured from the Fisherman job page and every page in `Category:Fishing` (the 6 fishing holes),
plus the fisherman NPCs and bait/lure/rod data. This is the authoritative **data model** for a 1:1
fishing pass. The moment-to-moment mini-game mechanics (bobber/bite/reel timing, camera) are NOT on
the wiki — those are reverse-engineered from the client in `FISHING_RE_NOTES.md`.

Source pages: Fisherman, Sacred Grove Shallows, Rainbow Lake, Darklit Lagoon, Brambleback's Bayou,
Wintery Basin, Frostbitten Banks, Chip Numbwing, Bait Bucket. (`Category:Fishing` = those 6 holes only.)

## The job
"Ever wonder what lurks within the waters of Sacred Grove? Fishermen know all the secret spots to cast
their lines, and reel in everything from exotic fish to treasure and coin!"
- Unlock: `<Sports Club>` Message Board in Sanctuary.
- Trainer: **Reed Stillwater** in Stillwater Crossing.
- NPCs: **Jonah Relicreel** (Rainbow Lake, "Itty Bitty Bait" quest); **Chip Numbwing** (pixie merchant/
  fisherman in Snowhill/Seaside).
- Quests: Becoming a Better Fisherman (Sanctuary); Testing the Waters (Reed Stillwater); Itty Bitty Bait.

## Fishing holes (all "Difficulty 1") — fish by unlock level
Each hole is its own fishing mini-game instance in a zone. **Sacred Grove Shallows is our test spot**
(activity 563 / sg_fishing_medpond).

### Sacred Grove Shallows  (zone: Stillwater Crossing)
Collections: Extra Large Catch of the Day, Wilds Stream Fish, Wilds Pond Fish
| Fish | Min level |
|------|-----------|
| Slugmud Skipper | 1 |
| Tickled Trout | 1 |
| Flutterfish | 1 |
| Butter Flyfish | 5 |
| Cheery Salmon | 10 |
| Chipsen Fish | 14 |
| Feral Catfish | 17 |
| Baconfish | 20 |
| Tangletooth Shark | 20 |

### Rainbow Lake  (zone: Lakeshore)
Collections: Extra Large Catch of the Day, Wilds Stream Fish, Wilds Pond Fish
Flutterfish (1), Calico Catfish (1), Tickled Trout (1), Toothy Tetra (5), Peachy Perch (10),
Finless Fish (14), Lady Tetra (17), Tangletooth Shark (20)

### Brambleback's Bayou  (zone: Bristlewood)
Collections: Briarwood Pond Fish, Briarwood Stream Fish, Extra Large and in Charge
Creeping Cod (1), Bitter Betta (1), Globfish (1), Blind Swurglefish (5), Changed Salmon (10),
Fanged Grouper (14), Briar Nibbler (17), Bitter Betta (20)

### Darklit Lagoon  (zone: Bristlewood)
Collections: Briarwood Pond Fish, Briarwood Stream Fish, Extra Large and in Charge
Ink Cod (1), Creeping Cod (1), Golden Scaled Nettler (1), Thorny Trout (5), Old Sole (10),
Purplenosed Shark (14), Roach Loach (17)

### Wintery Basin  (zone: Snowhill)
Collections: Snowhill Pond Fish, Snowhill Stream Fish, Extra Large Catch of the Day
Frozen Char (1), Winter Piranha (1), Frostgill Smelt (5), Spineless Stickleback (10),
Blubracuda (14), Coach Loach (17), Ferocious Fangler (20)

### Frostbitten Banks  (zone: Snowhill)
Collections: Snowhill Pond Fish, Snowhill Stream Fish, Extra Large Catch of the Day
Chilly Grouper (1), Winter Piranha (1), Frostgill Smelt (5), Pacu Pacu (5), Blue Thornfin (10),
Spineless Stickleback (10), Goofy Grouper (14), Talking Bass (17), Ferocious Fangler (20),
Plattypus (20)

## Rods (→ cast distance; maps to FishingPlayerConfig Min/MaxCastDistance)
- Simple Bamboo Fishing Rod — "cast a short distance"
- Golden Reel Fishing Rod — "cast a short distance"
- Metal Fishing Rod — "cast a greater distance"
- Red Scoped Fishing Rod — "cast a greater distance"
- Golden Scoped Fishing Rod — "fish in the deepest of waters!"
Tools come from the Coin Shop, quests, or reeled-in treasure chests.

## Lures → +10% catch chance for 3 specific fish each
Client system: `FishingProcessor.m_FishLureRequirementList` + `FishingLureDataSource`; each
ClientFishEntryInfo carries a lure-requirement id (see FISHING_RE_NOTES.md). A matching equipped lure
raises that fish's odds ~10%.

| Lure | +10% chance for |
|------|-----------------|
| 16oz Steak | Winter Piranha, Toothy Tetra, Blind Swurglefish |
| Flyfisher 3000 | Chilly Grouper, Butter Flyfish, Briar Nibbler |
| French Fry | Blubbercuda, Baconfish, Changed Salmon |
| Frostflies | Frostgill Smelt, Flutterfish, Ink Cod |
| Mega Slider | Goofy Grouper, Finless Fish, Fanged Grouper |
| Nightcrawlers | Plattypus, Lady Tetra, Roach Loach |
| Perch Pinpointer | Pacu Pacu, Peachy Perch, Globfish |
| Shiny Crankbait | Coach Loach, Feral Catfish, Creeping Cod |
| Skipper Seeker | Frozen Char, Slugmud Skipper, Old Sole |
| Sleek Clicker | Talking Bass, Cheery Salmon, Bitter Betta |
| Thorn Jig | Blue Thornfin, Chipsen Fish, Thorny Trout |
| Tiny Rib | Spineless Stickleback, Calico Catfish, Purplenosed Shark |
| Treasure Magnet | Treasure (sg_fishing_treasure_chest_bbe, model 1624) |

## Master fish list (min level; holes)
Baconfish(20; SG) · Bitter Betta(1/20; Bayou) · Blind Swurglefish(5; Bayou) · Blubracuda(14; Wintery) ·
Blue Thornfin(10; Frostbitten) · Briar Nibbler(17; Bayou) · Butter Flyfish(5; SG) · Calico Catfish(1;
Rainbow) · Changed Salmon(10; Bayou) · Cheery Salmon(10; SG) · Chilly Grouper(1; Frostbitten) ·
Chipsen Fish(14; SG) · Coach Loach(17; Wintery) · Creeping Cod(1; Bayou/Darklit) · Fanged Grouper(14;
Bayou) · Feral Catfish(17; SG) · Ferocious Fangler(20; Wintery/Frostbitten) · Finless Fish(14; Rainbow)
· Flutterfish(1; SG/Rainbow) · Frostgill Smelt(5; Wintery/Frostbitten) · Frozen Char(1; Wintery) ·
Globfish(1; Bayou) · Goofy Grouper(14; Frostbitten) · Golden Scaled Nettler(1; Darklit) · Ink Cod(1;
Darklit) · Lady Tetra(17; Rainbow) · Old Sole(10; Darklit) · Pacu Pacu(5; Frostbitten) · Peachy Perch(10;
Rainbow) · Plattypus(20; Frostbitten) · Purplenosed Shark(14; Darklit) · Roach Loach(17; Darklit) ·
Slugmud Skipper(1; SG) · Spineless Stickleback(10; Wintery/Frostbitten) · Talking Bass(17; Frostbitten)
· Tangletooth Shark(20; SG/Rainbow) · Thorny Trout(5; Darklit) · Tickled Trout(1; SG/Rainbow) · Toothy
Tetra(5; Rainbow) · Winter Piranha(1; Wintery/Frostbitten)

## Notes for the 1:1 pass
- **Our current FishTable is wrong for Sacred Grove.** We roll "Swurgle Fish / Calico Catfish / Globfish"
  — but those belong to Brambleback's Bayou (Globfish, Blind Swurglefish) and Rainbow Lake (Calico
  Catfish). Sacred Grove Shallows' real fish are Slugmud Skipper, Tickled Trout, Flutterfish, Butter
  Flyfish, Cheery Salmon, Chipsen Fish, Feral Catfish, Baconfish, Tangletooth Shark.
- A proper pass builds a **per-activity fish table** keyed by the zone's Underwater_Bed, each fish with
  its ClientItemDefinitions item/name/icon id, min level, size, and lure requirement, then rolls by
  player level + rarity (and applies the +10% lure bonus).
- Higher fish require higher fisherman level (1/5/10/14/17/20 tiers). All holes are "Difficulty 1".
- Treasure (chest, model 1624) is a possible catch, boosted by the Treasure Magnet lure.
