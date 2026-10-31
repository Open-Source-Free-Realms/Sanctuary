namespace Sanctuary.Packet.Common;

public enum GroupKickResult : uint
{
    Success,
    NotInGroup,
    NotOwner,
    TargetNotInGroup
}
