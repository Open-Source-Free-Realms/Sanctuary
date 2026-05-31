using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

public class MiniGameInfo : ISerializableType
{
    public int NameId;
    public int IconId;
    public int DescriptionId;
    public int Difficulty;
    public int ProfileType;

    public RewardBundleBase RewardBundleBase = new();
    public RewardBundleBase RewardBundleBase_Member = new();
    public RewardBundleBase RewardBundleBase_Preview = new();

    // public List<ObjectiveData> Objectives;

    /// <summary>
    ///   -1 - None
    ///    0 - Tutorial
    ///    1 - Sample
    ///    2 - Scavenger Hunt
    ///    3 - Client Flash
    ///    4 - Combat
    ///    5 - Hidden Object
    ///    6 - Chess
    ///    7 - Checkers
    ///    8 - Match Three
    ///    9 - Mimic
    ///   10 - Racing
    ///   11 - Demo Derby
    ///   12 - Soccer
    ///   13 - Pirates
    ///   14 - Tower Defense
    ///   15 - Tile Matching
    ///   16 - Trading Card Game
    ///   17 - Platformer
    ///   18 - Pro Vehicle Race
    ///   19 - Battle Mages
    ///   21 - Fishing
    ///   22 - Wheel
    ///   23 - Simple Card Game
    /// 1001 - Starfighter
    /// 1002 - 3D Tower Defense
    /// 1003 - SpeederBike
    /// 1004 - Saber Strike
    /// 1005 - Lightsaber Duel
    /// 1006 - Star Destroyer
    /// 1007 - Droid Programming
    /// 1008 - Infiltration
    /// 1009 - Force Perception
    /// 1010 - Star Typer
    /// 1011 - Stunt Gungan
    /// 1012 - Crystal Attunement
    /// 1013 - Blaster Training
    /// 1014 - R2 Rocket Rescue
    /// 1015 - Jedi Jump
    /// 1016 - Gunship
    /// 1017 - Holocron
    /// 1018 - Spin
    /// 1019 - Quiz
    /// 1020 - Mine Buster
    /// 1022 - Demolition Droid
    /// </summary>
    public int Type;

    public bool MembersOnly;

    public int Unknown14;

    public int PreselectedGameId;

    public int Unknown20;

    public bool ShowStarCounter;
    public bool ShowStatusIcon;
    public bool ShowActionBar;

    public bool Unknown11;

    public bool ShowEndDialog;

    public bool Unknown15;
    public bool Unknown16;
    public bool Unknown17;
    public bool Unknown18;
    public bool Unknown19;

    public string? Unknown13;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(NameId);
        writer.Write(IconId);
        writer.Write(DescriptionId);
        writer.Write(Difficulty);
        writer.Write(ProfileType);
        writer.Write(Type);

        writer.Write(MembersOnly);

        RewardBundleBase.Serialize(writer);
        RewardBundleBase_Member.Serialize(writer);
        RewardBundleBase_Preview.Serialize(writer);

        // TODO: Objectives
        writer.Write(0);

        writer.Write(ShowStarCounter);
        writer.Write(ShowStatusIcon);
        writer.Write(ShowActionBar);

        writer.Write(Unknown11);

        writer.Write(ShowEndDialog);

        writer.Write(Unknown13);

        writer.Write(Unknown14);

        writer.Write(Unknown15);

        writer.Write(PreselectedGameId);

        writer.Write(Unknown16);
        writer.Write(Unknown17);
        writer.Write(Unknown18);
        writer.Write(Unknown19);

        writer.Write(Unknown20);
    }
}