namespace Sanctuary.Packet.Common;

public enum GroupInviteResult : uint
{
    Sent,
    NotFound,
    Silent,
    AlreadyGrouped,
    AlreadyHasInvite,
    NotLeader,
    AlreadyInvited,
    GroupFull
}
