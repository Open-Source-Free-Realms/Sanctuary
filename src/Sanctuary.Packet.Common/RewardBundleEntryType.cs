namespace Sanctuary.Packet.Common;

public enum RewardBundleEntryType
{
    Item = 0x1,
    Experience = 0x3,
    QuestAdd = 0x6,
    ProfileAdd = 0x7,
    AbilityLine = 0x8,
    CollectionAdd = 0xA,
    CollectionEntryAdd = 0xB,
    Token = 0xC,
    PetTrickExperience = 0xD,
    Recipe = 0xE,
    ZoneFlag = 0xF,
    CharacterFlag = 0x11,
    WheelSpin = 0x12,
    Trophy = 0x13,
    ClientExitUrl = 0x14,
    FactoryExperience = 0x16,
    Chest = 0x17
}
