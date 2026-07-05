using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Game.Combat;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Gateway.Combat;
using Sanctuary.Gateway.Services;
using Sanctuary.Gateway.Services.Models;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PacketChatHandler
{
    private static ILogger _logger = null!;
    private static ILogger _chatLogger = null!;
    private static IZoneManager _zoneManager = null!;
    private static BanStore _banStore = null!;
    private static IpHistoryStore _ipHistoryStore = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;
    private static IChatLogWriter _chatLogWriter = null!;
    private static IResourceManager _resourceManager = null!;

    private static readonly string NpcJsonPath =
        Path.Combine(AppContext.BaseDirectory, "Data", "Npcs", "npcs.json");

    private static readonly Vector4 CombatMineSpawnPosition = new(460f, 47f, 284f, 1f);
    private static readonly Quaternion CombatMineSpawnRotation = Quaternion.Identity;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketChatHandler));
        _chatLogger = loggerFactory.CreateLogger("Chat");

        _zoneManager = serviceProvider.GetRequiredService<IZoneManager>();
        _banStore = serviceProvider.GetRequiredService<BanStore>();
        _ipHistoryStore = serviceProvider.GetRequiredService<IpHistoryStore>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
        _chatLogWriter = serviceProvider.GetRequiredService<IChatLogWriter>();
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!PacketChat.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketChat));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketChat), packet);

        packet.FromGuid = connection.Player.Guid;
        packet.FromName = connection.Player.Name;

        var message = packet.Message?.Trim() ?? string.Empty;
        packet.Message = message;

        if (TryHandleCommand(connection, packet, message))
            return true;

        _chatLogWriter.Write(
            packet.Channel.ToString(),
            connection.Player?.Name?.FullName ?? "Unknown",
            message
        );

        switch (packet.Channel)
        {
            case ChatChannel.Tell:
                {
                    if (_zoneManager.TryGetPlayer(packet.ToName.FullName, out var toPlayer))
                    {
                        _chatLogger.LogInformation("Tell|From: \"{FromName}\" ({FromGuid}), To: \"{ToName}\" ({ToGuid}), Msg: \"{Message}\"",
                            packet.FromName,
                            packet.FromGuid,
                            packet.ToName,
                            toPlayer.Guid,
                            packet.Message
                        );

                        if (!toPlayer.Ignores.Any(x => x.Guid == connection.Player.Guid))
                            toPlayer.SendTunneled(packet);

                        var tellEchoPacket = new TellEchoPacket
                        {
                            Name = packet.ToName,
                            Message = packet.Message
                        };

                        connection.Player.SendTunneled(tellEchoPacket);
                    }
                    break;
                }

            case ChatChannel.WorldShout:
                {
                    _chatLogger.LogInformation("WorldShout|From: \"{FromName}\" ({FromGuid}), Msg: \"{Message}\"",
                        packet.FromName,
                        packet.FromGuid,
                        packet.Message
                    );

                    foreach (var zonePlayer in connection.Player.Zone.Players)
                    {
                        if (zonePlayer.Ignores.Any(x => x.Guid == connection.Player.Guid))
                            continue;

                        zonePlayer.SendTunneled(packet);
                    }
                    break;
                }

            case ChatChannel.WorldTrade:
            case ChatChannel.WorldLfg:
            case ChatChannel.WorldArea:
            case ChatChannel.WorldMembersOnly:
                {
                    _chatLogger.LogInformation("{Channel}|Area: {AreaNameId}, From: \"{FromName}\" ({FromGuid}), Msg: \"{Message}\"",
                        packet.Channel,
                        packet.AreaNameId,
                        packet.FromName,
                        packet.FromGuid,
                        packet.Message
                    );

                    connection.Player.SendTunneled(packet);

                    foreach (var visiblePlayer in connection.Player.VisiblePlayers)
                    {
                        if (visiblePlayer.Value.ChatChannelStatus.TryGetValue(packet.Channel, out var channelStatus) && !channelStatus)
                            continue;

                        if (visiblePlayer.Value.Ignores.Any(x => x.Guid == connection.Player.Guid))
                            continue;

                        visiblePlayer.Value.SendTunneled(packet);
                    }
                    break;
                }

            case ChatChannel.GuildSay:
                {
                    if (connection.Player.GuildData is null)
                        break;

                    packet.GuildGuid = connection.Player.GuildData.Guid;

                    _chatLogger.LogInformation("GuildSay|GuildGuid: {GuildGuid}, From: \"{FromName}\" ({FromGuid}), Msg: \"{Message}\"",
                        packet.GuildGuid,
                        packet.FromName,
                        packet.FromGuid,
                        packet.Message
                    );

                    foreach (var guildPlayer in _zoneManager.GetPlayers())
                    {
                        if (guildPlayer.GuildData is null || guildPlayer.GuildData.Guid != packet.GuildGuid)
                            continue;

                        if (guildPlayer.Guid != connection.Player.Guid && guildPlayer.Ignores.Any(x => x.Guid == connection.Player.Guid))
                            continue;

                        guildPlayer.SendTunneled(packet);
                    }
                    break;
                }

            default:
                {
                    _chatLogger.LogInformation("{Channel}|From: \"{FromName}\" ({FromGuid}), Msg: \"{Message}\"",
                        packet.Channel,
                        packet.FromName,
                        packet.FromGuid,
                        packet.Message
                    );

                    connection.Player.SendTunneled(packet);

                    foreach (var visiblePlayer in connection.Player.VisiblePlayers)
                    {
                        if (visiblePlayer.Value.Ignores.Any(x => x.Guid == connection.Player.Guid))
                            continue;

                        visiblePlayer.Value.SendTunneled(packet);
                    }
                    break;
                }
        }

        return true;
    }

    private static bool TryHandleCommand(GatewayConnection connection, PacketChat packet, string message)
    {
        if (string.IsNullOrWhiteSpace(message) || !message.StartsWith('!'))
            return false;

        // Public commands for all players.
        if (message.StartsWith("!setspeed", StringComparison.OrdinalIgnoreCase))
            return HandleSetSpeedCommand(connection, message);

        if (message.StartsWith("!jumpforce", StringComparison.OrdinalIgnoreCase))
            return HandleJumpForceCommand(connection, message);

        if (message.Equals("!playerlist", StringComparison.OrdinalIgnoreCase))
            return HandlePlayerListCommand(connection);

        var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return true;

        var command = parts[0].ToLowerInvariant();

        switch (command)
        {
            case "!build":
                return HandleBuildCommand(connection);

            case "!combat":
                return HandleCombatCommand(connection);

            case "!leavecombat":
                return HandleLeaveCombatCommand(connection);

            case "!abilitytest":
            case "!testability":
                return HandleAbilityTestCommand(connection, parts);

            case "!abilityclear":
            case "!clearability":
                return HandleAbilityClearCommand(connection, parts);

            case "!abilityslots":
                return HandleAbilitySlotsCommand(connection);

            case "!anim":
                return HandleAnimTestCommand(connection, parts);

            case "!animjob":
                return HandleAnimJobCommand(connection, parts);

            case "!animrange":
                return HandleAnimRangeCommand(connection, parts);

            case "!animnext":
                return HandleAnimStepCommand(connection, 1);

            case "!animprev":
            case "!animprevious":
                return HandleAnimStepCommand(connection, -1);

            case "!cast":
                return HandleCastTestCommand(connection, parts);

            case "!atk":
                return HandleAttackProcessedTestCommand(connection, parts);

            case "!dmg":
                return HandleDamageNumberTestCommand(connection, parts);

            case "!hp":
            case "!hpme":
                return HandleHitpointsTestCommand(connection, command, parts);

            case "!fight":
                return HandleFightStateCommand(connection, parts);

            case "!ticon":
                return HandleIconProbeCommand(connection, parts);

            case "!combatreset":
            case "!resetcombat":
                return HandleCombatResetCommand(connection);

            case "!combatjobs":
                SendSystemMessage(connection, CombatBootstrap.DescribeSupportedProfiles());
                return true;
        }

        if (!connection.Player.IsAdmin)
        {
            SendSystemMessage(connection, "Unknown command.");
            return true;
        }

        switch (command)
        {
            case "!spawnnpc":
                return HandleSpawnNpcCommand(connection, parts);

            case "!savenpcs":
                return HandleSaveNpcsCommand(connection);

            case "!loadnpcs":
                return HandleLoadNpcsCommand(connection);

            case "!speed":
                return HandleSpeedCommand(connection, message);

            case "!bring":
                return HandleBringCommand(connection, message);

            case "!teleport":
                return HandleTeleportCommand(connection, message);

            case "!ban":
                return HandleBanCommand(connection, message);

            case "!userinfo":
                return HandleUserInfoCommand(connection, message);

            case "!unban":
                return HandleUnbanCommand(connection, message);

            case "!adminhelp":
                SendSingleSystemMessage(connection,
                    "Commands: !setspeed <1-1000>; !spawnnpc <nameId> <modelId> [scale] [textureAlias...]; " +
                    "!speed [character name] <speed>; !bring [character name]; !teleport [player1] [player2]; " +
                    "!userinfo <name> or [character name]; !ban <name> <reason> or [character name] <reason>; " +
                    "!unban <username> or [character name]; !savenpcs; !abilityclear [slot 1|2]; " +
                    "!abilityslots");
                SendChatColorReset(connection);
                return true;

            default:
                return false;
        }
    }

    private static bool HandleBuildCommand(GatewayConnection connection)
    {
        try
        {
            HousingService.SendHousingUi(connection, _resourceManager, _logger);
            SendSystemMessage(connection, "Free-build housing UI opened.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Free-build housing command failed. PlayerGuid={playerGuid}", connection.Player?.Guid);
            SendSystemMessage(connection, "Free-build housing failed. Check gateway logs.");
        }

        return true;
    }

    private static bool HandlePlayerListCommand(GatewayConnection connection)
    {
        var activePlayers = _zoneManager.GetPlayers()
            .Select(x => x.Name.FullName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (activePlayers.Count == 0)
        {
            SendSystemMessage(connection, "No active players.");
            return true;
        }

        SendSystemMessage(connection,
            $"Active players ({activePlayers.Count}):\n" +
            string.Join("\n", activePlayers));

        return true;
    }

    private static bool HandleSetSpeedCommand(GatewayConnection connection, string message)
    {
        const string usage = "Usage: !setspeed <1-1000>";

        var args = message.Substring("!setspeed".Length).Trim();
        if (string.IsNullOrWhiteSpace(args))
        {
            SendSystemMessage(connection, usage);
            return true;
        }

        var split = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (split.Length != 1)
        {
            SendSystemMessage(connection, usage);
            return true;
        }

        if (!float.TryParse(split[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var speed) &&
            !float.TryParse(split[0], NumberStyles.Float, CultureInfo.CurrentCulture, out speed))
        {
            SendSystemMessage(connection, $"Invalid speed: {split[0]}");
            return true;
        }

        if (speed < 1f || speed > 1000f)
        {
            SendSystemMessage(connection, "Speed must be between 1 and 1000.");
            return true;
        }

        connection.Player.UpdateCharacterStats(CharacterStats.MaxMovementSpeed.Set(speed));

        SendSystemMessage(connection,
            $"Your speed is now set to {speed.ToString(CultureInfo.InvariantCulture)}.");

        return true;
    }

    private static bool HandleJumpForceCommand(GatewayConnection connection, string message)
    {
        const string usage = "Usage: !jumpforce <1-1000>";

        var args = message.Substring("!jumpforce".Length).Trim();
        if (string.IsNullOrWhiteSpace(args))
        {
            SendSystemMessage(connection, usage);
            return true;
        }

        var split = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (split.Length != 1)
        {
            SendSystemMessage(connection, usage);
            return true;
        }

        if (!float.TryParse(split[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var jumpForce) &&
            !float.TryParse(split[0], NumberStyles.Float, CultureInfo.CurrentCulture, out jumpForce))
        {
            SendSystemMessage(connection, $"That jumpforce value doesn't work: {split[0]}");
            return true;
        }

        if (jumpForce < 1f || jumpForce > 1000f)
        {
            SendSystemMessage(connection, "That jumpforce value doesn't work. Use a value between 1 and 1000.");
            return true;
        }

        connection.Player.UpdateCharacterStats(new CharacterStat(CharacterStatId.JumpHeight, jumpForce));

        SendSystemMessage(connection,
            $"Your jump force is now set to {jumpForce.ToString(CultureInfo.InvariantCulture)}.");

        return true;
    }

    private static bool HandleBringCommand(GatewayConnection connection, string message)
    {
        const string usage =
            "Usage: !bring <name> OR !bring [character name]";

        var args = message.Substring("!bring".Length).Trim();
        if (string.IsNullOrWhiteSpace(args))
        {
            SendSystemMessage(connection, usage);
            return true;
        }

        if (!TryReadNextCharacterArgument(ref args, out var targetName, out var parseError))
        {
            SendSystemMessage(connection, parseError ?? usage);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(args))
        {
            SendSystemMessage(connection, usage);
            return true;
        }

        if (!_zoneManager.TryGetPlayer(targetName, out var targetPlayer))
        {
            SendSystemMessage(connection, $"Character not found: {targetName}");
            return true;
        }

        if (targetPlayer.Guid == connection.Player.Guid)
        {
            SendSystemMessage(connection, "You cannot bring yourself.");
            return true;
        }

        TeleportPlayerToPlayer(targetPlayer, connection.Player);

        SendSystemMessage(connection,
            $"Brought [{targetPlayer.Name.FullName}] to [{connection.Player.Name.FullName}].");

        SendPrivateCommandMessage(
            targetPlayer,
            $"You were brought to [{connection.Player.Name.FullName}] by an admin.");

        return true;
    }

    private static bool HandleTeleportCommand(GatewayConnection connection, string message)
    {
        const string usage =
            "Usage: !teleport <player1> <player2> OR !teleport [player1] [player2]";

        var args = message.Substring("!teleport".Length).Trim();
        if (string.IsNullOrWhiteSpace(args))
        {
            SendSystemMessage(connection, usage);
            return true;
        }

        if (!TryReadNextCharacterArgument(ref args, out var sourceName, out var parseError1))
        {
            SendSystemMessage(connection, parseError1 ?? usage);
            return true;
        }

        if (!TryReadNextCharacterArgument(ref args, out var destinationName, out var parseError2))
        {
            SendSystemMessage(connection, parseError2 ?? usage);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(args))
        {
            SendSystemMessage(connection, usage);
            return true;
        }

        if (string.Equals(sourceName, destinationName, StringComparison.OrdinalIgnoreCase))
        {
            SendSystemMessage(connection, "The two players must be different.");
            return true;
        }

        if (!_zoneManager.TryGetPlayer(sourceName, out var sourcePlayer))
        {
            SendSystemMessage(connection, $"Character not found: {sourceName}");
            return true;
        }

        if (!_zoneManager.TryGetPlayer(destinationName, out var destinationPlayer))
        {
            SendSystemMessage(connection, $"Character not found: {destinationName}");
            return true;
        }

        if (sourcePlayer.Guid == destinationPlayer.Guid)
        {
            SendSystemMessage(connection, "The two players must be different.");
            return true;
        }

        TeleportPlayerToPlayer(sourcePlayer, destinationPlayer);

        SendSystemMessage(connection,
            $"Teleported [{sourcePlayer.Name.FullName}] to [{destinationPlayer.Name.FullName}].");

        SendPrivateCommandMessage(
            sourcePlayer,
            $"You were teleported to [{destinationPlayer.Name.FullName}] by an admin.");

        return true;
    }

    private static bool TryReadNextCharacterArgument(
        ref string input,
        out string value,
        out string? error)
    {
        value = string.Empty;
        error = null;

        input = (input ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Missing character name.";
            return false;
        }

        if (input.StartsWith("[", StringComparison.Ordinal))
        {
            var closeBracket = input.IndexOf(']');
            if (closeBracket <= 1)
            {
                error = "Invalid bracketed character name.";
                return false;
            }

            value = input.Substring(1, closeBracket - 1).Trim();
            input = input[(closeBracket + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                error = "Character name cannot be empty.";
                return false;
            }

            return true;
        }

        var firstSpace = input.IndexOf(' ');
        if (firstSpace < 0)
        {
            value = input.Trim();
            input = string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        value = input[..firstSpace].Trim();
        input = input[(firstSpace + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Character name cannot be empty.";
            return false;
        }

        return true;
    }

    private static void TeleportPlayerToPlayer(Player playerToMove, Player destinationPlayer)
    {
        if (playerToMove.Zone != destinationPlayer.Zone)
        {
            playerToMove.TeleportToZone(
                destinationPlayer.Zone,
                destinationPlayer.Position,
                destinationPlayer.Rotation);

            return;
        }

        playerToMove.Mount?.UpdatePosition(destinationPlayer.Position, destinationPlayer.Rotation);
        playerToMove.UpdatePosition(destinationPlayer.Position, destinationPlayer.Rotation);

        playerToMove.SendTunneled(new ClientUpdatePacketUpdateLocation
        {
            Position = destinationPlayer.Position,
            Rotation = destinationPlayer.Rotation,
            Teleport = true
        });

        playerToMove.SendTunneledToVisible(new PlayerUpdatePacketUpdatePosition
        {
            Guid = playerToMove.Guid,
            Position = destinationPlayer.Position,
            Rotation = destinationPlayer.Rotation,
            State = 0,
            Unknown = 0
        });
    }

    private static bool HandleSpawnNpcCommand(GatewayConnection connection, string[] parts)
    {
        if (parts.Length < 3)
        {
            SendSystemMessage(connection,
                "Usage: !spawnnpc <nameId> <modelId> [scale] [textureAlias...]");
            return true;
        }

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var nameId))
        {
            SendSystemMessage(connection, $"Invalid nameId: {parts[1]}");
            return true;
        }

        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var modelId))
        {
            SendSystemMessage(connection, $"Invalid modelId: {parts[2]}");
            return true;
        }

        float scale = 1.0f;
        string? textureAlias = null;

        if (parts.Length >= 4)
        {
            if (float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedScale))
            {
                scale = parsedScale;

                if (parts.Length >= 5)
                    textureAlias = string.Join(' ', parts.Skip(4));
            }
            else
            {
                textureAlias = string.Join(' ', parts.Skip(3));
            }
        }

        var npc = connection.Player.SpawnNpc(nameId, modelId, scale, textureAlias);
        if (npc is null)
        {
            SendSystemMessage(connection, "NPC spawn failed.");
            return true;
        }

        SendSystemMessage(connection,
            $"Spawned NPC\n" +
            $"NameId: {npc.NameId}\n" +
            $"ModelId: {npc.ModelId}\n" +
            $"Scale: {npc.Scale.ToString(CultureInfo.InvariantCulture)}\n" +
            $"Texture: {(string.IsNullOrWhiteSpace(npc.TextureAlias) ? "None" : npc.TextureAlias)}");

        return true;
    }

    private static bool HandleSaveNpcsCommand(GatewayConnection connection)
    {
        var count = NpcJsonLoader.SaveCommandSpawnedFromZone(connection.Player.Zone, NpcJsonPath);

        SendSystemMessage(connection, $"Saved {count} NPC(s)");
        return true;
    }

    private static bool HandleLoadNpcsCommand(GatewayConnection connection)
    {
        if (!File.Exists(NpcJsonPath))
        {
            SendSystemMessage(connection, "No NPC json found");
            return true;
        }

        var imported = NpcJsonLoader.LoadIntoZone(connection.Player.Zone, NpcJsonPath, connection.Player.Guid);

        SendSystemMessage(connection, $"Loaded {imported} NPC(s)");
        return true;
    }

    private static bool HandleAbilityTestCommand(GatewayConnection connection, string[] parts)
    {
        const string usage = "Usage: !abilitytest <slot 1|2> <abilityId> OR !abilitytest <abilityId>";

        if (parts.Length is not 2 and not 3)
        {
            SendSystemMessage(connection, usage);
            return true;
        }

        var displaySlot = 1;
        var abilityArgIndex = 1;

        if (parts.Length == 3)
        {
            if (!TryParseFlexibleInt(parts[1], out displaySlot) || displaySlot is < 1 or > 2)
            {
                SendSystemMessage(connection, "Slot must be 1 or 2.");
                return true;
            }

            abilityArgIndex = 2;
        }

        if (!TryParseFlexibleInt(parts[abilityArgIndex], out var abilityDefinitionId) || abilityDefinitionId <= 0)
        {
            SendSystemMessage(connection, $"Invalid ability id: {parts[abilityArgIndex]}");
            return true;
        }

        CombatBootstrap.SetTemporaryAbilityOverride(connection, displaySlot, abilityDefinitionId, _logger);

        var definitionKnown = CombatBootstrap.TrySendAbilityDefinition(connection, abilityDefinitionId, _logger);
        CombatBootstrap.SendForActiveProfile(connection, _resourceManager, _logger);

        SendSystemMessage(connection,
            $"Temporary ability override set: slot {displaySlot} -> {abilityDefinitionId} / 0x{abilityDefinitionId:X}." +
            (definitionKnown ? string.Empty : " Warning: no captured AbilityDefinition blob is known for that id."));

        return true;
    }

    private static bool HandleAbilityClearCommand(GatewayConnection connection, string[] parts)
    {
        const string usage = "Usage: !abilityclear [slot 1|2]";

        if (parts.Length > 2)
        {
            SendSystemMessage(connection, usage);
            return true;
        }

        int? displaySlot = null;

        if (parts.Length == 2)
        {
            if (!TryParseFlexibleInt(parts[1], out var parsedSlot) || parsedSlot is < 1 or > 2)
            {
                SendSystemMessage(connection, "Slot must be 1 or 2.");
                return true;
            }

            displaySlot = parsedSlot;
        }

        CombatBootstrap.ClearTemporaryAbilityOverride(connection, displaySlot, _logger);
        CombatBootstrap.SendForActiveProfile(connection, _resourceManager, _logger);

        SendSystemMessage(connection, displaySlot is null
            ? "Cleared all temporary ability overrides."
            : $"Cleared temporary ability override for slot {displaySlot}.");

        return true;
    }

    private static bool HandleAbilitySlotsCommand(GatewayConnection connection)
    {
        SendSystemMessage(connection, CombatBootstrap.DescribeTemporaryAbilityOverrides(connection));
        return true;
    }

    private static bool HandleAnimTestCommand(GatewayConnection connection, string[] parts)
    {
        if (parts.Length <= 1 ||
            parts[1].Equals("0", StringComparison.OrdinalIgnoreCase) ||
            parts[1].Equals("off", StringComparison.OrdinalIgnoreCase) ||
            parts[1].Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            SendSystemMessage(connection, CombatBootstrap.ClearAnimationProbe(connection));
            return true;
        }

        if (!TryParseFlexibleInt(parts[1], out var animationId))
        {
            SendSystemMessage(connection, $"Invalid animation id: {parts[1]}");
            return true;
        }

        if (parts.Length > 2)
        {
            SendSystemMessage(connection, "Usage: !anim <animationId> OR !anim 0");
            return true;
        }

        SendSystemMessage(connection, CombatBootstrap.ArmAnimationProbe(connection, animationId, "manual"));
        return true;
    }

    private static bool HandleAnimJobCommand(GatewayConnection connection, string[] parts)
    {
        if (parts.Length != 2)
        {
            SendSystemMessage(connection,
                $"Usage: !animjob <job>. Known sets: {CombatBootstrap.DescribeAnimationCandidateSets()}");
            return true;
        }

        CombatBootstrap.TryArmAnimationCandidateSet(connection, parts[1], out var result);
        SendSystemMessage(connection, result);
        return true;
    }

    private static bool HandleAnimRangeCommand(GatewayConnection connection, string[] parts)
    {
        const string usage = "Usage: !animrange <startId> <endId> [step]";

        if (parts.Length is not 3 and not 4)
        {
            SendSystemMessage(connection, usage);
            return true;
        }

        if (!TryParseFlexibleInt(parts[1], out var start) ||
            !TryParseFlexibleInt(parts[2], out var end))
        {
            SendSystemMessage(connection, usage);
            return true;
        }

        var step = 1;
        if (parts.Length == 4 && (!TryParseFlexibleInt(parts[3], out step) || step == 0))
        {
            SendSystemMessage(connection, "Step must be a non-zero integer.");
            return true;
        }

        CombatBootstrap.TryArmAnimationRange(connection, start, end, step, out var result);
        SendSystemMessage(connection, result);
        return true;
    }

    private static bool HandleAnimStepCommand(GatewayConnection connection, int delta)
    {
        CombatBootstrap.TryMoveAnimationProbe(connection, delta, out var result);
        SendSystemMessage(connection, result);
        return true;
    }

    private static bool HandleCastTestCommand(GatewayConnection connection, string[] parts)
    {
        var packet = new AbilityPacketStartCasting
        {
            Unknown = connection.Player.Guid,
            Unknown2 = connection.Player.Guid,
            CompositeEffectId = 0,
            Animation = -1,
            AbilityId = 1,
            ActionTime = 5f,
            HasActionProgress = true,
        };

        if (parts.Length > 1 && ulong.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var unknown))
            packet.Unknown = unknown;
        if (parts.Length > 2 && ulong.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var unknown2))
            packet.Unknown2 = unknown2;
        if (parts.Length > 3 && TryParseFlexibleInt(parts[3], out var compositeEffectId))
            packet.CompositeEffectId = compositeEffectId;
        if (parts.Length > 4 && TryParseFlexibleInt(parts[4], out var animation))
            packet.Animation = animation;
        if (parts.Length > 5 && TryParseFlexibleInt(parts[5], out var abilityId))
            packet.AbilityId = abilityId;
        if (parts.Length > 6 && float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out var actionTime))
            packet.ActionTime = actionTime;
        if (parts.Length > 7)
            packet.HasActionProgress = parts[7] == "1" || parts[7].Equals("true", StringComparison.OrdinalIgnoreCase);

        connection.SendTunneled(packet);
        SendSystemMessage(connection,
            $"Sent cast test: anim={packet.Animation}, fx={packet.CompositeEffectId}, ability={packet.AbilityId}, time={packet.ActionTime.ToString(CultureInfo.InvariantCulture)}.");
        return true;
    }

    private static bool HandleAttackProcessedTestCommand(GatewayConnection connection, string[] parts)
    {
        var npc = FindCombatTarget(connection);
        if (npc is null)
        {
            SendSystemMessage(connection, "No hostile damageable NPC found. Use !combat first.");
            return true;
        }

        var damage = parts.Length > 1 && TryParseFlexibleInt(parts[1], out var parsedDamage) ? parsedDamage : 250;
        var maxHp = parts.Length > 2 && TryParseFlexibleInt(parts[2], out var parsedMaxHp) ? parsedMaxHp : Math.Max(npc.MaxHitpoints, 5000);
        var effectId = parts.Length > 3 && TryParseFlexibleInt(parts[3], out var parsedEffectId) ? parsedEffectId : 7;
        var bool1 = parts.Length > 4 && IsTruthy(parts[4]);
        var bool2 = parts.Length > 5 && IsTruthy(parts[5]);
        var int4 = parts.Length > 6 && TryParseFlexibleInt(parts[6], out var parsedInt4) ? parsedInt4 : 0;
        var int5 = parts.Length > 7 && TryParseFlexibleInt(parts[7], out var parsedInt5) ? parsedInt5 : maxHp;

        connection.SendTunneled(new CombatPacketAttackProcessed
        {
            Guid1 = connection.Player.Guid,
            Guid2 = npc.Guid,
            Guid3 = npc.Guid,
            Int1 = damage,
            Int2 = maxHp,
            Int3 = effectId,
            Bool1 = bool1,
            Bool2 = bool2,
            Int4 = int4,
            Int5 = int5,
        });

        SendSystemMessage(connection, $"Sent AttackProcessed to {npc.Name ?? npc.Guid.ToString(CultureInfo.InvariantCulture)}: dmg={damage}, maxHp={maxHp}, fx={effectId}.");
        return true;
    }

    private static bool HandleDamageNumberTestCommand(GatewayConnection connection, string[] parts)
    {
        var npc = FindCombatTarget(connection);
        if (npc is null)
        {
            SendSystemMessage(connection, "No hostile damageable NPC found. Use !combat first.");
            return true;
        }

        var amount = parts.Length > 1 && TryParseFlexibleInt(parts[1], out var parsedAmount) ? parsedAmount : -50;
        var currentHp = parts.Length > 2 && TryParseFlexibleInt(parts[2], out var parsedCurrentHp) ? parsedCurrentHp : npc.CurrentHitpoints;
        var maxHp = parts.Length > 3 && TryParseFlexibleInt(parts[3], out var parsedMaxHp) ? parsedMaxHp : npc.MaxHitpoints;
        var bool1 = parts.Length > 4 && IsTruthy(parts[4]);
        var bool2 = parts.Length > 5 && IsTruthy(parts[5]);

        connection.SendTunneled(new PlayerUpdatePacketHitPointModification
        {
            Guid = npc.Guid,
            Guid2 = connection.Player.Guid,
            Unknown = bool1,
            Unknown2 = amount,
            Unknown3 = currentHp,
            Unknown4 = maxHp,
            Unknown5 = bool2,
        });

        SendSystemMessage(connection, $"Sent damage-number test to {npc.Name ?? npc.Guid.ToString(CultureInfo.InvariantCulture)}: amount={amount}, hp={currentHp}/{maxHp}.");
        return true;
    }

    private static bool HandleHitpointsTestCommand(GatewayConnection connection, string command, string[] parts)
    {
        var self = command.Equals("!hpme", StringComparison.OrdinalIgnoreCase);
        var currentHp = parts.Length > 1 && TryParseFlexibleInt(parts[1], out var parsedCurrentHp) ? parsedCurrentHp : 100;
        var maxHp = parts.Length > 2 && TryParseFlexibleInt(parts[2], out var parsedMaxHp) ? parsedMaxHp : 5000;
        var unknown = parts.Length > 3 && TryParseFlexibleInt(parts[3], out var parsedUnknown) ? parsedUnknown : 0;

        ulong targetGuid;
        string targetName;

        if (self)
        {
            targetGuid = connection.Player.Guid;
            targetName = connection.Player.Name.FullName;
        }
        else
        {
            var npc = FindCombatTarget(connection);
            if (npc is null)
            {
                SendSystemMessage(connection, "No hostile damageable NPC found. Use !combat first.");
                return true;
            }

            npc.CurrentHitpoints = currentHp;
            npc.MaxHitpoints = maxHp;
            targetGuid = npc.Guid;
            targetName = npc.Name ?? npc.Guid.ToString(CultureInfo.InvariantCulture);
        }

        connection.SendTunneled(new PlayerUpdatePacketUpdateHitpoints
        {
            Guid = targetGuid,
            CurrentHitpoints = currentHp,
            MaxHitpoints = maxHp,
            Unknown = unknown,
        });

        SendSystemMessage(connection, $"Sent HP update to {targetName}: {currentHp}/{maxHp}.");
        return true;
    }

    private static bool HandleFightStateCommand(GatewayConnection connection, string[] parts)
    {
        var enabled = !(parts.Length > 1 && (parts[1] == "0" || parts[1].Equals("off", StringComparison.OrdinalIgnoreCase)));

        connection.SendTunneled(new EncounterOverworldCombatPacket { Unknown3 = enabled });
        connection.SendTunneled(new EncounterPacketIsFighting { IsFighting = enabled });

        SendSystemMessage(connection, enabled ? "Combat/fighting state enabled." : "Combat/fighting state disabled.");
        return true;
    }

    private static bool HandleIconProbeCommand(GatewayConnection connection, string[] parts)
    {
        if (parts.Length <= 1)
        {
            NinjaWeaponAbilities.DebugMeleeIcon = null;
            NinjaWeaponAbilities.DebugSpecialIcon = null;
            CombatBootstrap.SendForActiveProfile(connection, _resourceManager, _logger);
            SendSystemMessage(connection, "Ninja icon override cleared.");
            return true;
        }

        if (!TryParseFlexibleInt(parts[1], out var meleeIcon))
        {
            SendSystemMessage(connection, $"Invalid melee icon id: {parts[1]}");
            return true;
        }

        var specialIcon = meleeIcon;
        if (parts.Length > 2 && !TryParseFlexibleInt(parts[2], out specialIcon))
        {
            SendSystemMessage(connection, $"Invalid special icon id: {parts[2]}");
            return true;
        }

        NinjaWeaponAbilities.DebugMeleeIcon = meleeIcon;
        NinjaWeaponAbilities.DebugSpecialIcon = specialIcon;
        CombatBootstrap.SendForActiveProfile(connection, _resourceManager, _logger);

        SendSystemMessage(connection, $"Ninja icon override set: melee={meleeIcon}, special={specialIcon}.");
        return true;
    }

    private static bool HandleCombatResetCommand(GatewayConnection connection)
    {
        CombatBootstrap.ClearAnimationProbe(connection);
        CombatBootstrap.ClearTemporaryAbilityOverride(connection, null, _logger);
        NinjaWeaponAbilities.DebugMeleeIcon = null;
        NinjaWeaponAbilities.DebugSpecialIcon = null;

        connection.SendTunneled(new EncounterOverworldCombatPacket { Unknown3 = false });
        connection.SendTunneled(new EncounterPacketIsFighting { IsFighting = false });
        SendChatColorReset(connection);
        CombatBootstrap.SendForActiveProfile(connection, _resourceManager, _logger);

        SendSystemMessage(connection, "Combat debug state reset: animation, ability overrides, ninja icon overrides, and fighting state cleared.");
        return true;
    }

    private static bool TryParseFlexibleInt(string value, out int result)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool IsTruthy(string value) =>
        value == "1" ||
        value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("on", StringComparison.OrdinalIgnoreCase);

    private static Npc? FindCombatTarget(GatewayConnection connection)
    {
        return connection.Player.Zone.Npcs
            .Where(n => n.IsHostile && n.IsDamageable && n.IsAlive)
            .OrderBy(n => Vector4.DistanceSquared(connection.Player.Position, n.Position))
            .FirstOrDefault();
    }

    private static bool HandleCombatCommand(GatewayConnection connection)
    {
        if (!_zoneManager.TryGetOrCreateCombatInstance(out var combatZone))
        {
            SendSystemMessage(connection, "Combat instance creation failed.");
            return true;
        }

        EnsureCombatEncounter(combatZone);

        if (_zoneManager.IsCombatInstance(connection.Player.Zone))
        {
            SendSystemMessage(connection, "You are already in the combat instance.");
            return true;
        }

        connection.Player.TeleportToZone(
            combatZone,
            CombatMineSpawnPosition,
            CombatMineSpawnRotation);

        connection.Player.SendTunneled(new EncounterOverworldCombatPacket
        {
            Unknown3 = true
        });

        SendSystemMessage(connection, "Entering combat instance.");
        return true;
    }

    private static bool HandleLeaveCombatCommand(GatewayConnection connection)
    {
        if (!_zoneManager.IsCombatInstance(connection.Player.Zone))
        {
            SendSystemMessage(connection, "You are not in the combat instance.");
            return true;
        }

        var position = connection.Player.StartingZonePosition;
        var rotation = connection.Player.StartingZoneRotation;

        if (position == default)
        {
            position = _zoneManager.StartingZone.SpawnPosition;
            rotation = _zoneManager.StartingZone.SpawnRotation;
        }

        connection.Player.SendTunneled(new EncounterOverworldCombatPacket
        {
            Unknown3 = false
        });

        connection.Player.TeleportToZone(_zoneManager.StartingZone, position, rotation);

        SendSystemMessage(connection, "Leaving combat instance.");
        return true;
    }

    private static void EnsureCombatEncounter(Sanctuary.Game.Zones.CombatInstanceZone combatZone)
    {
        if (combatZone.Npcs.Any(x => x.IsCommandSpawned && x.Name.StartsWith("Combat ", StringComparison.OrdinalIgnoreCase)))
            return;

        CreateCombatNpc(combatZone, "Combat Robgoblin", 4, new Vector4(468f, 47f, 284f, 1f));
        CreateCombatNpc(combatZone, "Combat Ghost Miner", 10, new Vector4(454f, 47f, 292f, 1f));
        CreateCombatNpc(combatZone, "Combat Screecher", 29, new Vector4(462f, 49f, 276f, 1f));
        CreateCombatNpc(combatZone, "Combat Mine Bat", 28, new Vector4(450f, 49f, 282f, 1f));
    }

    private static void CreateCombatNpc(
        Sanctuary.Game.Zones.CombatInstanceZone combatZone,
        string name,
        int modelId,
        Vector4 position)
    {
        if (!combatZone.TryCreateNpc(out var npc))
            return;

        npc.Visible = true;
        npc.IsInteractable = true;
        npc.IsCommandSpawned = true;
        npc.Name = name;
        npc.NameId = 0;
        npc.ModelId = modelId;
        npc.Disposition = 0;
        npc.Scale = 1.0f;
        npc.HasHealthBar = true;
        npc.MaxHitpoints = 500;
        npc.CurrentHitpoints = npc.MaxHitpoints;
        npc.CreatedAtUtc = DateTime.UtcNow;

        npc.UpdatePosition(position, CombatMineSpawnRotation);

        if (npc.ZoneTile == Sanctuary.Game.Zones.ZoneTile.Empty)
            combatZone.TryRemoveNpc(npc.Guid);
    }

    private static bool HandleSpeedCommand(GatewayConnection connection, string message)
    {
        const string usage =
            "Usage: !speed <name> <speed> OR !speed [full name] <speed>";

        if (!TryParseCharacterTarget(message, "!speed", out var targetName, out var trailingText, out var parseError))
        {
            SendSystemMessage(connection, parseError ?? usage);
            return true;
        }

        if (string.IsNullOrWhiteSpace(trailingText))
        {
            SendSystemMessage(connection, usage);
            return true;
        }

        if (!float.TryParse(trailingText, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed) &&
            !float.TryParse(trailingText, NumberStyles.Float, CultureInfo.CurrentCulture, out speed))
        {
            SendSystemMessage(connection, $"Invalid speed: {trailingText}");
            return true;
        }

        if (speed <= 0f)
        {
            SendSystemMessage(connection, "Speed must be greater than 0.");
            return true;
        }

        if (!_zoneManager.TryGetPlayer(targetName, out var targetPlayer))
        {
            SendSystemMessage(connection, $"Character not found: {targetName}");
            return true;
        }

        targetPlayer.UpdateCharacterStats(CharacterStats.MaxMovementSpeed.Set(speed));

        SendSystemMessage(
            connection,
            $"Set speed for [{targetPlayer.Name.FullName}] to {speed.ToString(CultureInfo.InvariantCulture)} (temporary).");

        if (targetPlayer.Guid != connection.Player.Guid)
        {
            SendPrivateCommandMessage(
                targetPlayer,
                $"Your speed was set to {speed.ToString(CultureInfo.InvariantCulture)} by [{connection.Player.Name.FullName}] (temporary).");
        }

        return true;
    }

    private static bool HandleUserInfoCommand(GatewayConnection connection, string message)
    {
        const string usage =
            "Usage: !userinfo <name> OR !userinfo [character name]";

        if (!TryParseCharacterTarget(message, "!userinfo", out var targetName, out _, out var parseError))
        {
            SendSystemMessage(connection, parseError ?? usage);
            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        if (!TryGetUserInfoByCharacterName(dbContext, targetName, out var username, out var characterNames, out var error))
        {
            SendSystemMessage(connection, error ?? $"Character not found: {targetName}");
            return true;
        }

        SendSystemMessage(connection,
            $"Username: {username}\n" +
            $"Characters: {string.Join(", ", characterNames)}");

        return true;
    }

    private static bool HandleBanCommand(GatewayConnection connection, string message)
    {
        const string usage =
            "Usage: !ban <name> <reason> OR !ban [character name] <reason>";

        var args = message.Substring("!ban".Length).Trim();
        if (string.IsNullOrWhiteSpace(args))
        {
            SendSystemMessage(connection, usage);
            return true;
        }

        string targetName;
        string reason;

        if (args.StartsWith("[", StringComparison.Ordinal))
        {
            int closeBracket = args.IndexOf(']');
            if (closeBracket <= 1)
            {
                SendSystemMessage(connection, usage);
                return true;
            }

            targetName = args.Substring(1, closeBracket - 1).Trim();
            reason = args[(closeBracket + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(targetName) || string.IsNullOrWhiteSpace(reason))
            {
                SendSystemMessage(connection, usage);
                return true;
            }
        }
        else
        {
            var split = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (split.Length < 2)
            {
                SendSystemMessage(connection, usage);
                return true;
            }

            targetName = split[0];
            reason = split[1];
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        if (!TryGetBanInfoByCharacterName(
                dbContext,
                targetName,
                out var userId,
                out var username,
                out var characterNames,
                out var knownIps,
                out var error))
        {
            SendSystemMessage(connection, error ?? $"Character not found: {targetName}");
            return true;
        }

        var entry = new BanEntry
        {
            UserId = userId,
            Username = username,
            CharacterNames = characterNames,
            KnownIps = knownIps,
            Reason = reason,
            BannedBy = connection.Player.Name.FullName,
            BannedAtUtc = DateTime.UtcNow
        };

        _banStore.AddOrUpdateBan(entry);

        if (_zoneManager.TryGetPlayer(targetName, out var onlinePlayer))
        {
            SendPrivateCommandMessage(
                onlinePlayer,
                $"You were banned by [{connection.Player.Name.FullName}]. Reason: {reason}");

            onlinePlayer.Disconnect();
        }

        SendSystemMessage(connection,
            $"Banned [{username}] | Reason: {reason}");

        return true;
    }

    private static bool HandleUnbanCommand(GatewayConnection connection, string message)
    {
        const string usage =
            "Usage: !unban <username> OR !unban [character name]";

        var args = message.Substring("!unban".Length).Trim();
        if (string.IsNullOrWhiteSpace(args))
        {
            SendSystemMessage(connection, usage);
            return true;
        }

        if (args.StartsWith("[", StringComparison.Ordinal))
        {
            if (!TryParseCharacterTarget(message, "!unban", out var targetName, out _, out var parseError))
            {
                SendSystemMessage(connection, parseError ?? usage);
                return true;
            }

            using var dbContext = _dbContextFactory.CreateDbContext();

            if (!TryGetBanInfoByCharacterName(
                    dbContext,
                    targetName,
                    out var userId,
                    out var username,
                    out _,
                    out _,
                    out var error))
            {
                SendSystemMessage(connection, error ?? $"Character not found: {targetName}");
                return true;
            }

            if (_banStore.RemoveBanByUserId(userId))
                SendSystemMessage(connection, $"Unbanned user [{username}].");
            else
                SendSystemMessage(connection, $"No ban entry found for [{username}].");

            return true;
        }

        var usernameArg = args;
        if (_banStore.RemoveBanByUsername(usernameArg))
            SendSystemMessage(connection, $"Unbanned user [{usernameArg}].");
        else
            SendSystemMessage(connection, $"No ban entry found for [{usernameArg}].");

        return true;
    }

    private static bool TryParseCharacterTarget(
        string message,
        string commandName,
        out string targetName,
        out string trailingText,
        out string? error)
    {
        targetName = string.Empty;
        trailingText = string.Empty;
        error = null;

        var args = message.Substring(commandName.Length).Trim();
        if (string.IsNullOrWhiteSpace(args))
        {
            error = $"Usage: {commandName} <name> OR {commandName} [character name]";
            return false;
        }

        if (args.StartsWith("[", StringComparison.Ordinal))
        {
            var closeBracket = args.IndexOf(']');
            if (closeBracket <= 1)
            {
                error = $"Usage: {commandName} <name> OR {commandName} [character name]";
                return false;
            }

            targetName = args.Substring(1, closeBracket - 1).Trim();
            trailingText = args[(closeBracket + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(targetName))
            {
                error = $"Usage: {commandName} <name> OR {commandName} [character name]";
                return false;
            }

            return true;
        }

        var split = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (split.Length == 0)
        {
            error = $"Usage: {commandName} <name> OR {commandName} [character name]";
            return false;
        }

        targetName = split[0];
        trailingText = split.Length > 1 ? split[1] : string.Empty;
        return true;
    }

    private static bool TryGetUserInfoByCharacterName(
        DatabaseContext dbContext,
        string fullName,
        out string username,
        out List<string> characterNames,
        out string? error)
    {
        username = string.Empty;
        characterNames = new List<string>();
        error = null;

        var (firstName, lastName) = SplitCharacterName(fullName);

        var dbCharacter = dbContext.Characters
            .AsNoTracking()
            .Include(x => x.User)
            .SingleOrDefault(x => x.FirstName == firstName && x.LastName == lastName);

        if (dbCharacter is null || dbCharacter.User is null)
        {
            error = $"Character not found: {fullName}";
            return false;
        }

        username = dbCharacter.User.Username;

        var rawCharacterNames = dbContext.Characters
            .AsNoTracking()
            .Where(x => x.UserId == dbCharacter.User.Id)
            .Select(x => new
            {
                x.FirstName,
                x.LastName
            })
            .ToList();

        characterNames = rawCharacterNames
            .Select(x => $"{x.FirstName} {x.LastName}".Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return true;
    }

    private static bool TryGetBanInfoByCharacterName(
        DatabaseContext dbContext,
        string fullName,
        out ulong userId,
        out string username,
        out List<string> characterNames,
        out List<string> knownIps,
        out string? error)
    {
        userId = 0;
        username = string.Empty;
        characterNames = new List<string>();
        knownIps = new List<string>();
        error = null;

        var (firstName, lastName) = SplitCharacterName(fullName);

        var dbCharacter = dbContext.Characters
            .AsNoTracking()
            .Include(x => x.User)
            .SingleOrDefault(x => x.FirstName == firstName && x.LastName == lastName);

        if (dbCharacter is null || dbCharacter.User is null)
        {
            error = $"Character not found: {fullName}";
            return false;
        }

        var resolvedUserId = dbCharacter.User.Id;
        var resolvedUsername = dbCharacter.User.Username;

        var rawCharacterNames = dbContext.Characters
            .AsNoTracking()
            .Where(x => x.UserId == resolvedUserId)
            .Select(x => new
            {
                x.FirstName,
                x.LastName
            })
            .ToList();

        var resolvedCharacterNames = rawCharacterNames
            .Select(x => $"{x.FirstName} {x.LastName}".Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resolvedKnownIps = _ipHistoryStore.GetKnownIpsForUser(resolvedUserId, resolvedUsername)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        userId = resolvedUserId;
        username = resolvedUsername;
        characterNames = resolvedCharacterNames;
        knownIps = resolvedKnownIps;

        return true;
    }

    private static (string FirstName, string LastName) SplitCharacterName(string fullName)
    {
        var trimmed = (fullName ?? string.Empty).Trim();
        var firstSpace = trimmed.IndexOf(' ');

        if (firstSpace < 0)
            return (trimmed, string.Empty);

        var firstName = trimmed[..firstSpace].Trim();
        var lastName = trimmed[(firstSpace + 1)..].Trim();

        return (firstName, lastName);
    }

    private static void SendSystemMessage(GatewayConnection connection, string message)
    {
        SendPrivateCommandMessage(connection.Player, message);
    }

    private static void SendSingleSystemMessage(GatewayConnection connection, string message)
    {
        connection.Player.SendTunneled(new PacketChat
        {
            Channel = ChatChannel.System,
            Message = message
        });
    }

    private static void SendChatColorReset(GatewayConnection connection)
    {
        connection.Player.SendTunneled(new ChatPacketFromStringId
        {
            SpeakerGuid = connection.Player.Guid,
            StringId = 0,
            IsEmote = false,
            IsChatLogged = false,
            HasColor = true,
            ColorId = 0,
            OwnerGuid = connection.Player.Guid,
            TargetGuid = connection.Player.Guid,
            ElapsedTime = 0
        });
    }

    private static void SendPrivateCommandMessage(Player player, string message)
    {
        foreach (var line in SplitSystemMessageLines(message))
        {
            player.SendTunneled(new PacketChat
            {
                Channel = ChatChannel.System,
                Message = line
            });
        }
    }

    private static IEnumerable<string> SplitSystemMessageLines(string message)
    {
        var normalized = (message ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n', StringSplitOptions.None);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
                yield return trimmed;
        }
    }
}

