using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

// The in-game top-right "Goals" window is fed exclusively by this op47 family (the op45 minigame
// goal state drives the lobby panes and announces instead). sub1 adds/updates a row, sub3
// completes/removes it, sub5 clears the window. No minigame state is required — the window works
// standalone and shows itself on its first row.
public class UiObjectiveAddPacket : BaseUiPacket, ISerializablePacket
{
    public new const byte OpCode = 1;

    public int ObjectiveId;
    public int Unknown2;
    public int NameId;          // row text (string id)
    public bool Unknown4;
    public bool MembersOnly;    // non-member client: text swaps to the members-only string, icon locked
    public int Unknown6;
    public bool Unknown7;
    public int Unknown8 = 1;

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
        writer.Write(Unknown4);
        writer.Write(MembersOnly);
        writer.Write(Unknown6);
        writer.Write(Unknown7);
        writer.Write(Unknown8);

        return writer.Buffer;
    }
}

/// <summary>Sub 3 — complete/remove a Goals-window row by objective id.</summary>
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

/// <summary>Sub 5 — clear every Goals-window row (no payload).</summary>
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
