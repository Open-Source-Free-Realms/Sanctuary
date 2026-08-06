using System;

using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class GuildMemberLocationRequestHandler
{
    private static IZoneManager _zoneManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
    }

    public static bool HandlePacket(GatewayConnection connection)
    {
        if (connection.Player.GuildData is null)
            return true;

        var guildMemberLocationUpdatePacket = new GuildMemberLocationUpdatePacket
        {
            GuildGuid = connection.Player.GuildData.Guid
        };

        foreach (var guildMemberGuid in connection.Player.GuildData.Members.Keys)
        {
            if (guildMemberGuid == connection.Player.Guid)
                continue;

            if (!_zoneManager.StartingZone.TryGetPlayer(guildMemberGuid, out var guildMember))
                continue;

            if (!guildMember.Visible)
                continue;

            guildMemberLocationUpdatePacket.Entries.Add(new GuildMemberLocationUpdatePacket.Entry
            {
                Guid = guildMember.Guid,

                LocationX = guildMember.Position.X,
                LocationZ = guildMember.Position.Z
            });
        }

        if (guildMemberLocationUpdatePacket.Entries.Count > 0)
            connection.SendTunneled(guildMemberLocationUpdatePacket);

        return true;
    }
}