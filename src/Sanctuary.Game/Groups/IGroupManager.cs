using Sanctuary.Game.Entities;

namespace Sanctuary.Game.Groups;

public interface IGroupManager
{
    void Invite(Player inviter, string inviteeName);
    void Invite(Player inviter, Player? invitee);
    void Answer(Player invitee, ulong inviterGuid, bool accepted);
    void Leave(Player player);
    void Kick(Player kicker, string targetName);

    void SetChangingJob(Player player, bool isChangingJob);
    void SendMapPing(Player player, int x, int y);

    void SendGroup(Player player);
    void UpdateMember(Player player);
}
