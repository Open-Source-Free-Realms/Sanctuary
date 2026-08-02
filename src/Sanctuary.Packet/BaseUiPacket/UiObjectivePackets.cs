using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// ★ THE REAL GOALS-WINDOW FEED (RE'd 2026-07-03). The in-game top-right "Goals" tracker is
// Main.wndObjectives (minigame.lua ObjectiveWindow), bound to the C++ data source
// "BaseClient.ObjectiveHelper" (ObjectiveHelperDataSource, created in the UIProcessor ctor
// @0xA98020). It is NOT fed by op45 (minigame goal state) or the MiniGameGoals data source —
// those drive the LOBBY/ready panes (wndMinigameStatusObjPane) and the goal-complete announces.
//
// The ObjectiveHelper rows come exclusively from this BaseUiPacket (op47) family, dispatched in
// BaseClient::OnTunneledClientPacket2 case 47 -> UIProcessor::sub_A91BF0:
//   sub 1 = ADD/UPSERT row  -> sub_CB81E0: row key = ObjectiveId, text = StringProvider(NameId)
//           (server-known string id caveat), then fires the DS row-changed event -> Lua
//           BaseClient_ObjectiveHelper_OnDataChanged -> ObjectiveWindow:AddOrUpdateObjective ->
//           the window SHOWS ITSELF on its first row. MembersOnly + non-member client swaps the
//           text to string 9195 and icon state 4 (locked). The row's own Total field (this
//           packet's last wire int, live-verified 2026-07-26) gates whether ANY live count ever
//           renders at all - the client's status-text builder (FUN_00A8B9A0) only formats a
//           "Count/Total" string when Total>1, else it always shows a generic status label with
//           no digits (this is genuinely why a Total=1 single-target objective never shows a count
//           in retail either, not a display bug - confirmed structurally).
//           IsBonus (2026-07-26, found via the DS's own column-name debug table, FUN_00cb7710,
//           index 3 = "Is Bonus") stores into the row's +0x1c byte - LIVE-TESTED 2026-07-26, does
//           NOT produce the "Bonus:" prefix (confirmed: native code never reads +0x1c in the
//           decompiled Text-column render path, FUN_00A8B9A0's 6 args). Kept as dead-but-harmless
//           data since the client does store it.
//           CategoryPrefixId (2026-07-26, the REAL mechanism - found by fully decompiling the wire
//           deserializer FUN_00A8D1F0): the wire has a genuinely separate int32 field, positioned
//           right after IsBonus/MembersOnly, that the deserializer writes into the row's +0x18 -
//           the SAME offset the DS's own "Category Prefix" column (index 6, FUN_00cb7710) reads via
//           a dedicated getter (FUN_00c45c30) AND the Text-column builder (FUN_00A8B9A0) passes as
//           its category arg to the GATE function FUN_00A872F0. LIVE-TESTED 2026-07-26 with a T4
//           text directory id (116192, the real "Bonus: " string) - did NOT work, because this field
//           is NOT a text id at all: FUN_00A872F0 does `switch(param_2 + 1)` over a tiny closed set
//           and resolves a NAMED template key, not a T4 hash: -1/1->"ObjectiveCategoryPrefixPrimary",
//           2->"...Secondary", 3->"...Job", 4->"...Bonus", 5->"...Elite" (0 hits no case -> the
//           no-prefix path, which is why leaving this field at its 0 default always produced no
//           prefix). Use value 4 for "Bonus:".
//   sub 2 = UPDATE goal count -> sub_CB7E50 (previously undocumented, found 2026-07-26): sets the
//           row's live Count AND fires the same DS row-changed notify sub 1 does - REQUIRED for a
//           count to actually paint on screen after the row already exists; writing the row's
//           underlying fields directly (proven via a live memory patch) does nothing without this
//           notify also firing.
//   sub 3 = COMPLETE/REMOVE row by id -> sub_CB7F20.
//   sub 5 = CLEAR all rows -> sub_A89B60 (no payload; Lua ObjectiveWindow:Clear via OnDataUpdate).
//
// GROUND TRUTH (2014-04-01 capture): entry burst idx 28049/28069 =
//   2F00 01 [62310000=12642] [00000000] [F0960100=104176] [00] [00] [00000000] [00] [01000000]
// and completion idx 37165 = 2F00 03 [62310000]. No minigame state is required — this window
// works standalone (the Lua only gates on tutorial/pirates/disabled).
public class UiObjectiveAddPacket : BaseUiPacket, ISerializablePacket
{
    public new const byte OpCode = 1;

    public int ObjectiveId;
    public int Unknown2;
    public int NameId;          // row text (server-known string id!)
    public bool IsBonus;        // DS column 3 "Is Bonus" (row+0x1c) - dead data, native code never reads it
    public bool MembersOnly;    // non-member client: text -> string 9195, icon state 4 (locked)
    // DS column 6 "Category Prefix" (row+0x18) - a small enum, NOT a text id (see the note above):
    // 0 = none, 1/-1 = Primary, 2 = Secondary, 3 = Job, 4 = Bonus, 5 = Elite.
    public int CategoryPrefixId;
    public bool Unknown7;
    // Live-verified 2026-07-26 (frida memory patch on the real client): this is the row's GOAL TOTAL —
    // the field the client's own status-text builder (FUN_00a8b9a0) checks as "Total>1" before it'll ever
    // render a live "N/M" count at all. The earlier "real capture always sends 1" note was just every
    // captured example happening to be a single-target objective, not this field being hardcoded to 1.
    public int Total = 1;

    public UiObjectiveAddPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ObjectiveId);
        writer.Write(Unknown2);
        writer.Write(NameId);
        writer.Write(IsBonus);
        writer.Write(MembersOnly);
        writer.Write(CategoryPrefixId);
        writer.Write(Unknown7);
        writer.Write(Total);

        return writer.Buffer;
    }
}

// Sub 2 — live-verified 2026-07-26 (frida): updates an EXISTING row's Goal Count and fires the client's
// own "_OnDataChanged" redraw notify (FUN_010cb770) — the missing piece that makes a row already showing
// a real Total (see UiObjectiveAddPacket.Total) actually paint a live "N/M" count. Never implemented
// before this; op47/sub2 was previously undocumented.
public class UiObjectiveUpdateCountPacket : BaseUiPacket, ISerializablePacket
{
    public new const byte OpCode = 2;

    public int ObjectiveId;
    public int Count;

    public UiObjectiveUpdateCountPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ObjectiveId);
        writer.Write(Count);

        return writer.Buffer;
    }
}

// Sub 3 — complete/remove a Goals-window row by objective id.
public class UiObjectiveCompletePacket : BaseUiPacket, ISerializablePacket
{
    public new const byte OpCode = 3;

    public int ObjectiveId;

    public UiObjectiveCompletePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ObjectiveId);

        return writer.Buffer;
    }
}

// Sub 5 — clear every Goals-window row (no payload).
public class UiObjectiveClearPacket : BaseUiPacket, ISerializablePacket
{
    public new const byte OpCode = 5;

    public UiObjectiveClearPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        return writer.Buffer;
    }
}
