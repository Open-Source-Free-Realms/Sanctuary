using System;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Groups;

public sealed class GroupManager : IGroupManager
{
    private static readonly TimeSpan PendingInviteLifetime = TimeSpan.FromMinutes(1);

    private readonly object _lock = new();

    private readonly IZoneManager _zoneManager;

    public GroupManager(IZoneManager zoneManager)
    {
        _zoneManager = zoneManager;
    }

    public void Invite(Player inviter, string inviteeName)
    {
        _zoneManager.TryGetPlayer(inviteeName, out var invitee);

        Invite(inviter, invitee);
    }

    public void Invite(Player inviter, Player? invitee)
    {
        lock (_lock)
        {
            var result = TryInvite(inviter, invitee);

            inviter.SendTunneled(new GroupPacketGroupInviteReply
            {
                Result = result
            });
        }
    }

    public static bool CanInvite(Player inviter, Player invitee)
    {
        return invitee.Group is null
            && (inviter.Group is not { } group || group.Leader.Guid == inviter.Guid && !group.IsFull);
    }

    private static GroupInviteResult TryInvite(Player inviter, Player? invitee)
    {
        if (inviter.Group is { } group)
        {
            if (group.Leader.Guid != inviter.Guid)
                return GroupInviteResult.NotLeader;

            if (group.IsFull)
                return GroupInviteResult.GroupFull;
        }

        if (invitee is null
            || invitee.Guid == inviter.Guid
            || invitee.Ignores.Any(x => x.Guid == inviter.Guid))
            return GroupInviteResult.NotFound;

        if (invitee.Group is not null)
            return GroupInviteResult.AlreadyGrouped;

        if (invitee.PendingGroupInvite is { } pendingInvite && pendingInvite.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return pendingInvite.InviterGuid == inviter.Guid
                ? GroupInviteResult.AlreadyInvited
                : GroupInviteResult.AlreadyHasInvite;
        }

        invitee.PendingGroupInvite = new PendingGroupInvite(inviter.Guid, DateTimeOffset.UtcNow + PendingInviteLifetime);

        invitee.SendTunneled(new GroupPacketGroupInvite
        {
            Info = GetInviteInfo(inviter, invitee)
        });

        return GroupInviteResult.Sent;
    }

    public void Answer(Player invitee, ulong inviterGuid, bool accepted)
    {
        lock (_lock)
        {
            var result = TryAnswer(invitee, inviterGuid, accepted);

            if (result == GroupAcceptResult.Success)
                return;

            invitee.SendTunneled(new GroupPacketGroupAcceptReply
            {
                Result = result
            });
        }
    }

    private GroupAcceptResult TryAnswer(Player invitee, ulong inviterGuid, bool accepted)
    {
        if (invitee.PendingGroupInvite is not { } pendingInvite || pendingInvite.InviterGuid != inviterGuid)
            return GroupAcceptResult.NotInvited;

        invitee.PendingGroupInvite = null;

        if (!_zoneManager.TryGetPlayer(inviterGuid, out var inviter))
            return GroupAcceptResult.NoGroup;

        var acceptPacket = new GroupPacketGroupAccept
        {
            Info = GetInviteInfo(inviter, invitee),
            Accepted = accepted
        };

        if (!accepted)
        {
            inviter.SendTunneled(acceptPacket);

            return GroupAcceptResult.Success;
        }

        if (invitee.Group is not null)
            return GroupAcceptResult.NotInvited;

        var group = inviter.Group ?? new PlayerGroup(inviter);

        if (group.IsFull)
            return GroupAcceptResult.NoRoom;

        group.SendTunneled(acceptPacket);

        group.Add(invitee);

        inviter.Group = group;
        invitee.Group = group;

        group.SendTunneled(GetGroupUpdate(group));

        return GroupAcceptResult.Success;
    }

    public void Leave(Player player)
    {
        lock (_lock)
        {
            player.PendingGroupInvite = null;

            if (player.Group is not { } group)
                return;

            var leavePacket = new GroupPacketGroupLeave
            {
                PlayerGuid = player.Guid,
                PlayerName = player.Name
            };

            group.SendTunneled(leavePacket);

            var wasLeader = group.Leader.Guid == player.Guid;

            RemoveMember(group, player);

            if (wasLeader && group.Members.Count > 1)
                group.SendTunneled(GetGroupUpdate(group));
        }
    }

    public void Kick(Player kicker, string targetName)
    {
        lock (_lock)
        {
            var result = TryKick(kicker, targetName);

            if (result == GroupKickResult.Success)
                return;

            kicker.SendTunneled(new GroupPacketGroupKickReply
            {
                Result = result
            });
        }
    }

    private GroupKickResult TryKick(Player kicker, string targetName)
    {
        if (kicker.Group is not { } group)
            return GroupKickResult.NotInGroup;

        if (group.Leader.Guid != kicker.Guid)
            return GroupKickResult.NotOwner;

        var target = group.Members.FirstOrDefault(member => member.Guid != kicker.Guid && member.Name.FullName == targetName);

        if (target is null)
            return GroupKickResult.TargetNotInGroup;

        var kickPacket = new GroupPacketGroupKick
        {
            KickerGuid = kicker.Guid,
            KickerName = kicker.Name,
            TargetGuid = target.Guid,
            TargetName = target.Name
        };

        group.SendTunneled(kickPacket);

        RemoveMember(group, target);

        return GroupKickResult.Success;
    }

    public void SetChangingJob(Player player, bool isChangingJob)
    {
        lock (_lock)
        {
            player.IsChangingJob = isChangingJob;

            if (player.Group is not { } group)
                return;

            var changingJobUpdatePacket = new GroupChangingJobUpdatePacket
            {
                PlayerGuid = player.Guid,
                IsChangingJob = isChangingJob
            };

            group.SendTunneled(changingJobUpdatePacket, player);
        }
    }

    public void SendMapPing(Player player, int x, int y)
    {
        if (player.Group is not { } group)
            return;

        var mapPingPacket = new GroupMapPingPacket
        {
            SenderGuid = player.Guid,
            X = x,
            Y = y
        };

        group.SendTunneled(mapPingPacket, player);
    }

    public void SendGroup(Player player)
    {
        lock (_lock)
        {
            if (player.Group is not { } group)
                return;

            player.SendTunneled(GetGroupUpdate(group));
        }
    }

    public void UpdateMember(Player player)
    {
        lock (_lock)
        {
            if (player.Group is not { } group)
                return;

            var memberUpdatePacket = new GroupPacketGroupMemberUpdate
            {
                Member = GetGroupMember(player)
            };

            group.SendTunneled(memberUpdatePacket);
        }
    }

    private static void RemoveMember(PlayerGroup group, Player player)
    {
        group.Remove(player);

        player.Group = null;

        if (group.Members.Count == 1)
            group.Leader.Group = null;
    }

    private static GroupPacketGroupUpdate GetGroupUpdate(PlayerGroup group)
    {
        return new GroupPacketGroupUpdate
        {
            Members = group.Members.Select(GetGroupMember).ToList()
        };
    }

    private static GroupMember GetGroupMember(Player player)
    {
        var profile = player.ActiveProfile;

        return new GroupMember
        {
            Guid = player.Guid,
            Name = player.Name,
            JobId = profile.Id,
            JobIconId = profile.Icon,
            JobBackgroundIconId = profile.BadgeImageSet,
            JobRank = profile.Rank,
            IsChangingJob = player.IsChangingJob
        };
    }

    private static GroupInviteInfo GetInviteInfo(Player inviter, Player invitee)
    {
        return new GroupInviteInfo
        {
            InviterGuid = inviter.Guid,
            InviterName = inviter.Name,
            InviteeGuid = invitee.Guid,
            InviteeName = invitee.Name
        };
    }
}
