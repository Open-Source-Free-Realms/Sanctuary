using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Core.IO;
using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class ActivityPacketJoinActivityRequestHandler
{
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(ActivityPacketJoinActivityRequestHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data, int serverType)
    {
        if (!ActivityPacketJoinActivityRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(ActivityPacketJoinActivityRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(ActivityPacketJoinActivityRequest), packet);

        if (!_resourceManager.ClientActivityDefinitions.TryGetValue(packet.ActivityId, out var clientActivityDefinition))
            return true;

        // Free Realms Trading Card Game Lobby
        if (packet.ActivityId == 7)
        {
            var miniGameInfo = new MiniGameInfo()
            {
                NameId = clientActivityDefinition.DisplayNameId,
                IconId = clientActivityDefinition.ImageSetId,
                DescriptionId = clientActivityDefinition.DisplayDescriptionId,
                Difficulty = clientActivityDefinition.Difficulty,
                ProfileType = 0,
                Type = 16, // Trading Card Game
                PreselectedGameId = 7
            };

            var miniGameGroupInfo = new MiniGameGroupInfo()
            {
                Id = 69,
                NameId = clientActivityDefinition.DisplayNameId,
                DescriptionId = clientActivityDefinition.DisplayDescriptionId,
                IconId = clientActivityDefinition.ImageSetId
            };

            using var writer = new PacketWriter();

            miniGameInfo.Serialize(writer);

            writer.Write(0); // Unused
            writer.Write(0); // Unused

            writer.Write(miniGameGroupInfo.Serialize());

            var clientActivityLaunchPacketInviteDetails = new ClientActivityLaunchPacketInviteDetails(packet.ActivityId, 0)
            {
                Guid = connection.Player.Guid,
                Inviter = "Test",
                Members =
                {
                    new()
                    {
                        Id = 1,
                        Guid = connection.Player.Guid,
                        InviteStatus = 2,
                        IsFoundingMember = true
                    }
                },
                Request =
                {
                    RequestorGuid = connection.Player.Guid,
                    SysHashkey = JenkinsHelper.OneAtATimeHash("Minigame"),
                    ReqId = 69420,
                    MinMembers = 1,
                    MaxMembers = 1,
                    ImageSetId = clientActivityDefinition.ImageSetId,
                    NameStringId = clientActivityDefinition.DisplayNameId,
                    DescStringId = clientActivityDefinition.DisplayDescriptionId,
                    SysSpecificData = writer.Buffer
                }
            };

            connection.SendTunneled(clientActivityLaunchPacketInviteDetails);

            var clientActivityLaunchPacketActivityLaunched = new ClientActivityLaunchPacketActivityLaunched(packet.ActivityId, 0);

            clientActivityLaunchPacketActivityLaunched.Guids.Add(connection.Player.Guid);

            connection.SendTunneled(clientActivityLaunchPacketActivityLaunched);

            var miniGameInfoPacket = new MiniGameInfoPacket(packet.ActivityId, -1, -1)
            {
                Info = miniGameInfo
            };

            connection.SendTunneled(miniGameInfoPacket);
        }
        // Mining Practice
        else if (packet.ActivityId == 1113)
        {
            var miniGameInfo = new MiniGameInfo()
            {
                NameId = clientActivityDefinition.DisplayNameId,
                IconId = clientActivityDefinition.ImageSetId,
                DescriptionId = clientActivityDefinition.DisplayDescriptionId,
                Difficulty = clientActivityDefinition.Difficulty,
                ProfileType = 1,
                Type = 3, // Client Flash
                PreselectedGameId = 1113,
                Unknown11 = true,
                Unknown13 = "game_hidden.gfx"
            };

            var miniGameGroupInfo = new MiniGameGroupInfo()
            {
                Id = 69,
                NameId = clientActivityDefinition.DisplayNameId,
                DescriptionId = clientActivityDefinition.DisplayDescriptionId,
                IconId = clientActivityDefinition.ImageSetId,
                BackgroundSwf = "game_hidden.gfx"
            };

            using var writer = new PacketWriter();

            miniGameInfo.Serialize(writer);

            writer.Write(1);
            writer.Write(2);

            writer.Write(miniGameGroupInfo.Serialize());

            var clientActivityLaunchPacketInviteDetails = new ClientActivityLaunchPacketInviteDetails(packet.ActivityId, 0)
            {
                Guid = connection.Player.Guid,
                Inviter = "Test",
                Members =
                {
                    new()
                    {
                        Id = 1,
                        Guid = connection.Player.Guid,
                        InviteStatus = 2,
                        IsFoundingMember = true
                    }
                },
                Request =
                {
                    RequestorGuid = connection.Player.Guid,
                    SysHashkey = JenkinsHelper.OneAtATimeHash("Minigame"),
                    ReqId = 69420,
                    MinMembers = 1,
                    MaxMembers = 1,
                    ImageSetId = clientActivityDefinition.ImageSetId,
                    NameStringId = clientActivityDefinition.DisplayNameId,
                    DescStringId = clientActivityDefinition.DisplayDescriptionId,
                    SysSpecificData = writer.Buffer
                }
            };

            connection.SendTunneled(clientActivityLaunchPacketInviteDetails);

            var clientActivityLaunchPacketActivityLaunched = new ClientActivityLaunchPacketActivityLaunched(packet.ActivityId, 0);

            clientActivityLaunchPacketActivityLaunched.Guids.Add(connection.Player.Guid);

            connection.SendTunneled(clientActivityLaunchPacketActivityLaunched);

            var miniGameInfoPacket = new MiniGameInfoPacket(packet.ActivityId, -1, -1)
            {
                Info = miniGameInfo
            };

            connection.SendTunneled(miniGameInfoPacket);
        }

        return true;
    }
}