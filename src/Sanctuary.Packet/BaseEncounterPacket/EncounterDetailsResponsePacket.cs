using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// One inline objective inside MiniGameInfo.ObjectiveData[]. Goals must be DEFINED here, inline in
// the launch details packet, then ACTIVATED by id with op45 sub1 — the client's op45 dispatch
// requires the goal id to already exist in the MiniGameState, so goals that aren't defined inline
// can never be activated.
public sealed class EncounterObjective
{
    public int ObjectiveId;
    public int NameId;          // goal text (string id)
    public int DescriptionId;
    public int Status;          // 0 inline; ObjectiveActivate flips it to 2 (announce)
    public int Count;
    public int Total;           // 0 inline; the follow-up ObjectiveActivate sets the real total
    public int Unknown8;
    public bool MemberOnly;
    public int Unknown10;
}

// op41 sub114 — the S2C encounter details packet: the adventure OFFER POPUP (title / difficulty /
// description / prizes + GO! button) and, with the trailing Launch flag set, the LAUNCH that creates
// the client's MiniGameState.
//
// Layout: BaseEncounter header, then EncounterDetailsCommon { ints, two empty collections,
// ZoneContext, TeleportEffectId, flag bytes, RespawnTime, MiniGameInfo, two tail bytes }, then the
// packet's own Launch flag, an int, and an empty StoreBundleId set.
// MiniGameInfo: NameId · IconId · DescriptionId · Difficulty · ProfileType · Type · MembersOnly ·
// RewardBundle ×3 (reward / member / preview) · ObjectiveData[] · byte ×5 · string · int · byte ·
// PreselectedGameId · byte ×4 · ActivityId.
public class EncounterDetailsResponsePacket : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 114;

    // --- the visible popup content (MiniGameInfo) ---
    public int NameId;            // title (string id)
    public int IconId = -1;       // dungeon emblem icon (-1 = none/default)
    public int DescriptionId;     // description (string id)
    public int Difficulty;
    public int ProfileType;
    public int MiniGameType;
    public bool MembersOnly;

    public int TeleportEffectId;
    public int RespawnTime;
    public bool Tutorial;

    // Zone-context selector: 6 sets the client's ARENA flag — while set, every AddNpc apply forces
    // the character's disposition to hostile before the nameplate color resolver runs, so encounter
    // mobs get the RED name at spawn. 8 = hub. Send BEFORE the NPC adds.
    public int ZoneContext;

    // The trailing packet flag picks the client path: false = OFFER popup, true = LAUNCH (creates
    // the MiniGameState from this packet's MiniGameInfo). The MiniGameState is the master gate for
    // the whole minigame UI — every op45 objective packet is dropped while none exists — so the
    // encounter entry flow must send this packet AGAIN with Launch=true at GO!.
    public bool Launch;

    // Objectives DEFINED inline (see EncounterObjective). Empty = count-0 (offer popup).
    public List<EncounterObjective> Objectives = [];

    // The offer popup's prize list + the victory loot-wheel slices — serialized into the PREVIEW
    // reward bundle. The prize set should match the player's active job; ProfileType names the job
    // CATEGORY the set is for.
    public List<RewardEntry> PreviewRewards = [];

    // Coins/XP for the extra loot-wheel slices (bundle coins/XP columns).
    public int PreviewCoins;
    public int PreviewXp;

    // MiniGameInfo tail int = the ClientActivityDefinitions ACTIVITY ID for this encounter.
    public int ActivityId;

    public EncounterDetailsResponsePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        // ===== EncounterDetailsCommon =====
        writer.Write(0);                 // Unknown
        writer.Write(0);                 // Unknown2
        writer.Write(0);                 // collection count = 0
        writer.Write(0);                 // EncounterTeamData list count = 0
        writer.Write(ZoneContext);
        writer.Write(TeleportEffectId);
        writer.Write(true);              // Unknown5 (byte) — client ctor default 1
        writer.Write(false);             // Unknown6 (byte)
        writer.Write(Tutorial);          // (byte)
        writer.Write(0);                 // Unknown8
        writer.Write(RespawnTime);

        // ----- MiniGameInfo -----
        writer.Write(NameId);
        writer.Write(IconId);
        writer.Write(DescriptionId);
        writer.Write(Difficulty);
        writer.Write(ProfileType);
        writer.Write(MiniGameType);
        writer.Write(MembersOnly);       // (byte)
        WriteEmptyRewardBundle(writer);  // reward bundle
        WriteEmptyRewardBundle(writer);  // member reward bundle
        WriteRewardBundle(writer, PreviewRewards, PreviewCoins, PreviewXp); // preview (popup prizes + loot wheel)
        writer.Write(Objectives.Count);
        foreach (var obj in Objectives)
            WriteObjective(writer, obj);
        writer.Write(true);              // U8  (client ctor default 1)
        writer.Write(true);              // U9
        writer.Write(true);              // U10
        writer.Write(true);              // U11
        writer.Write(true);              // U12
        writer.Write((string?)null);     // U13 string
        writer.Write(1);                 // U14 (client ctor default 1)
        writer.Write(true);              // U15 (client ctor default 1)
        writer.Write(0);                 // PreselectedGameId
        writer.Write(false);             // U16
        writer.Write(false);             // U17
        writer.Write(false);             // U18
        writer.Write(false);             // U19
        writer.Write(ActivityId);
        // ----- end MiniGameInfo -----

        writer.Write(false);             // UNK0 (byte)
        writer.Write(true);              // UNK1 (byte) — REQUIRED: the client gates the whole popup on this
        // ===== end EncounterDetailsCommon =====

        writer.Write(Launch);            // (byte): false = offer popup, true = launch
        writer.Write(0);                 // packet Unknown
        writer.Write(0);                 // StoreBundleId set count = 0

        return writer.Buffer;
    }

    // One ObjectiveData record — byte-identical to the op45 ObjectiveData layout so an
    // inline-defined goal can be activated by id.
    private static void WriteObjective(PacketWriter writer, EncounterObjective obj)
    {
        writer.Write(obj.ObjectiveId);
        writer.Write(obj.NameId);
        writer.Write(obj.DescriptionId);
        writer.Write(false);              // byte Unknown4
        WriteEmptyRewardBundle(writer);
        writer.Write(obj.Status);
        writer.Write(obj.Count);
        writer.Write(obj.Total);
        writer.Write(obj.Unknown8);
        writer.Write(obj.MemberOnly);     // (byte)
        writer.Write(obj.Unknown10);
    }

    // RewardBundleBase with no entries — 69 fixed bytes; all-zero is a valid empty bundle.
    private static void WriteEmptyRewardBundle(PacketWriter writer)
    {
        writer.Write(false);
        for (var i = 0; i < 9; i++) writer.Write(0);
        writer.Write(0); writer.Write(0);
        writer.Write(0); writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);                                  // entryCount = 0
        writer.Write(0);
    }

    private static void WriteRewardBundle(PacketWriter writer, List<RewardEntry> entries, int coins, int xp)
    {
        if (entries.Count == 0)
        {
            WriteEmptyRewardBundle(writer);
            return;
        }

        RewardBundle.Write(writer, entries, coins, xp, entries[0].IconId, entries[0].NameId);
    }
}
