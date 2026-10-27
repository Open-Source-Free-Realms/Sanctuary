using System;
using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Activity service (the client's BaseActivityServicePacket, opcode 0xA7, byte sub-op): the activities the
/// Games window (MinigameDetail) lists per category.
/// </summary>
public static class BaseActivityServicePacket
{
    public const int MinigameActivityType = 1;

    public const short OpCode = 0xA7;

    /// <summary>
    /// Sub-op of activity packets (the client's BaseActivityPacket), which carry their own byte sub-op.
    /// </summary>
    public const byte ActivitySubOpCode = 1;

    public const byte ListOfActivities = 1;
    public const byte JoinActivityRequest = 2;
    public const byte ActivityListRefreshRequest = 4;

    public static bool TryReadSubOps(ReadOnlySpan<byte> data, out byte serviceSubOpCode, out byte activitySubOpCode)
    {
        serviceSubOpCode = activitySubOpCode = 0;
        var reader = new PacketReader(data);

        return reader.TryRead(out short opCode) && opCode == OpCode
            && reader.TryRead(out serviceSubOpCode) && reader.TryRead(out activitySubOpCode);
    }
}

/// <summary>
/// One row of the client's Activities data source; field order from its reader.
/// </summary>
public class ActivityDefinition : ISerializableType
{
    public int Id;
    public int AppSystemId;
    /// <summary>
    /// ActivityCategories.txt id: the Games window tile the activity is listed under (17 is Wheels).
    /// </summary>
    public int Category;
    public int ImageSetId;
    public int ActivityPositionId;
    public int DisplayNameId;
    public int DisplayDescriptionId;
    public int NameId;
    public int DescriptionId;
    /// <summary>
    /// How PLAY launches it: 1 hands it to the client's minigame controller.
    /// </summary>
    public int Type;
    /// <summary>
    /// Whether PLAY is enabled; otherwise the window says the level is too low or it's members only.
    /// </summary>
    public bool CanPlayerJoin;
    public int Unknown12;
    /// <summary>
    /// A list the client reads into the activity (rewards, presumably); always sent empty.
    /// </summary>
    public int UnknownListCount;
    public int Unknown14;
    public bool MembersOnly;
    public string DetailImageFilename = string.Empty;
    public string ThumbnailImageFilename = string.Empty;
    public int Difficulty;
    public int Unknown19;
    public int Unknown20;
    public int MysteryChestIcon;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Id);
        writer.Write(AppSystemId);
        writer.Write(Category);
        writer.Write(ImageSetId);
        writer.Write(ActivityPositionId);
        writer.Write(DisplayNameId);
        writer.Write(DisplayDescriptionId);
        writer.Write(NameId);
        writer.Write(DescriptionId);
        writer.Write(Type);
        writer.Write(CanPlayerJoin);
        writer.Write(Unknown12);
        writer.Write(UnknownListCount);
        writer.Write(Unknown14);
        writer.Write(MembersOnly);
        writer.Write(DetailImageFilename);
        writer.Write(ThumbnailImageFilename);
        writer.Write(Difficulty);
        writer.Write(Unknown19);
        writer.Write(Unknown20);
        writer.Write(MysteryChestIcon);
    }
}

public class ActivityPacketListOfActivities : ISerializablePacket
{
    /// <summary>
    /// The type of activities in this list (see <see cref="ActivityDefinition.Type"/>); the client replaces
    /// its activities of that type with these.
    /// </summary>
    public int Type;

    public List<ActivityDefinition> Activities = new();

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(BaseActivityServicePacket.OpCode);
        writer.Write(BaseActivityServicePacket.ActivitySubOpCode);
        writer.Write(BaseActivityServicePacket.ListOfActivities);

        writer.Write(Type);
        writer.Write(Activities.Count);

        foreach (var activity in Activities)
            activity.Serialize(writer);

        return writer.Buffer;
    }
}
