using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;

namespace Sanctuary.Game.ChatCommands;

public class FlagsChatCommand : IChatCommand
{
    // Matches what character creation stores for members (CharacterCreateRequestHandler).
    private const int MemberStatus = 2;

    private readonly IZoneManager _zoneManager;
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    public string KeyWord => "flags";
    public string Usage => "[player] | member on|off [player]";
    public string Description => "Shows a player's account/character flags, or toggles membership on the character and account.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public FlagsChatCommand(IZoneManager zoneManager, IChatCommandManager chatCommandManager, IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _zoneManager = zoneManager;
        _chatCommandManager = chatCommandManager;
        _dbContextFactory = dbContextFactory;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length > 0 && args[0].Equals("member", StringComparison.OrdinalIgnoreCase))
            return HandleMember(invoker, args[1..]);

        if (!TryGetTarget(invoker, args, out var target))
            return true;

        ChatHelper.SendSystemMessage(invoker,
            $"{target.Name.FullName}: Member={target.MembershipStatus != 0} (status {target.MembershipStatus}), " +
            $"Mod={target.IsMod}, Admin={target.IsAdmin}, Muted={target.IsMuted()}, " +
            $"Frozen={target.FrozenMovementSpeed is not null}, PacketLog={target.PacketLogger is not null}");

        return true;
    }

    private bool HandleMember(Player invoker, string[] args)
    {
        if (args.Length == 0)
            return false;

        bool enable;

        if (args[0].Equals("on", StringComparison.OrdinalIgnoreCase))
            enable = true;
        else if (args[0].Equals("off", StringComparison.OrdinalIgnoreCase))
            enable = false;
        else
            return false;

        if (!TryGetTarget(invoker, args[1..], out var target))
            return true;

        var characterId = GuidHelper.GetPlayerId(target.Guid);

        using var dbContext = _dbContextFactory.CreateDbContext();

        var dbCharacter = dbContext.Characters
            .Include(x => x.User)
            .SingleOrDefault(x => x.Id == characterId);

        if (dbCharacter is null)
            return true;

        dbCharacter.MembershipStatus = enable ? MemberStatus : 0;
        dbCharacter.User.IsMember = enable;
        dbContext.SaveChanges();

        target.MembershipStatus = dbCharacter.MembershipStatus;

        target.SendTunneled(new PacketMembershipSubscriptionInfo
        {
            IsMember = enable,
            IsReferee = target.IsMod || target.IsAdmin
        });

        _chatCommandManager.LogAction(this, invoker, enable ? "MemberOn" : "MemberOff", target.Name.FullName, null);
        ChatHelper.SendSystemMessage(invoker, $"Membership {(enable ? "enabled" : "disabled")} for {target.Name.FullName}. Some member UI only updates after relog.");

        return true;
    }

    private bool TryGetTarget(Player invoker, string[] nameArgs, out Player target)
    {
        target = invoker;

        if (nameArgs.Length == 0)
            return true;

        var targetName = string.Join(' ', nameArgs);

        if (_zoneManager.TryGetPlayer(targetName, out var found))
        {
            target = found;
            return true;
        }

        ChatHelper.SendSystemMessage(invoker, $"No online player named \"{targetName}\" was found.");
        return false;
    }
}
