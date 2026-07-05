using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;

namespace Sanctuary.Gateway.Combat;

public static class BrawlerCombatBootstrap
{
    public const int BrawlerProfileId = 43;
    private const float AutoTargetRange = 35f;
    private const float AutoTargetRangeSquared = AutoTargetRange * AutoTargetRange;
    private const int WeaponSlot = 7;
    private static ulong _combatEventGuid = 10_000_000_000;

    private static readonly Dictionary<ulong, Dictionary<int, int>> PlayerAbilityOverrides = new();
    private static readonly Dictionary<ulong, Dictionary<int, byte[]>> PlayerAbilityDefinitions = new();
    private static readonly Dictionary<ulong, Dictionary<int, int>> PlayerAbilityAliases = new();
    private static readonly Dictionary<ulong, Dictionary<int, byte[]>> PlayerLaunchAndLandTemplates = new();
    private static readonly Lazy<Dictionary<int, byte[]>> CapturedAbilityDefinitions = new(LoadCapturedAbilityDefinitions);
    private static readonly Lazy<Dictionary<int, byte[]>> CapturedLaunchAndLandTemplates = new(LoadCapturedLaunchAndLandTemplates);
    private static readonly Lazy<List<CapturedLaunchTemplate>> CapturedLaunchTemplates = new(LoadCapturedLaunchTemplates);
    private static readonly Lazy<List<CapturedCastChain>> CapturedCastChains = new(LoadCapturedCastChains);

    private static readonly Dictionary<int, byte[]> AbilitySetDefinitions = new()
    {
        [2] = Convert.FromHexString(
            "240005000200000008000000030000001F13000000000000EF1000002B680600040000000000A040010000001F1300000000000001030000002313000064000000060400005A680600040000000000704101000000231300000000000001000000000000000000000000000000000000000000000000"),
        [11] = Convert.FromHexString(
            "240005000B000000080000000300000026120000000000005F100000A96D0600040000000000A04001000000261200000000000001030000002512000064000000A7590000B56D0600040000000000704101000000251200000000000001000000000000000000000000000000000000000000000000"),
        [12] = Convert.FromHexString(
            "240005000C000000080000000300000073140000000000007B3800008E8B0600040000000000204101000000731400000000000001030000007414000064000000F2590000908B0600040000000000704101000000741400000000000001000000000000000000000000000000000000000000000000"),
        [32] = Convert.FromHexString(
            "240005002000000008000000030000006E130000000000001D1000001C8B0000040000000000A040010000006E1300000000000001030000006F13000064000000D7590000FD1E00000400000000007041010000006F1300000000000001000000000000000000000000000000000000000000000000"),
        [35] = Convert.FromHexString(
            "24000500230000000800000003000000C41300000000000023100000A0690600040000000000704101000000C4130000000000000103000000C51300006400000012590000A2690600040000000000704101000000C51300000000000001000000000000000000000000000000000000000000000000"),
        [BrawlerProfileId] = Convert.FromHexString(
            "240005002B00000008000000030000008A14000000000000DF370000648C0600040000000000A040010000008A1400000000000001030000008D14000064000000752D00006A8C06000400000000007041010000008D1400000000000001000000000000000000000000000000000000000000000000")
    };

    private static readonly Dictionary<int, int[]> ProfileSlotAbilityIds = new()
    {
        [2] = [0x131F, 0x1323],
        [11] = [0x1226, 0x1225],
        [12] = [0x1473, 0x1474],
        [32] = [0x136E, 0x136F],
        [35] = [0x13C4, 0x13C5],
        [BrawlerProfileId] = [0x148A, 0x148D]
    };

    private static readonly Dictionary<int, int> OneBasedSlotAliases = new()
    {
        [1] = 0,
        [2] = 1
    };

    private static readonly Dictionary<int, byte[]> LaunchAndLandTemplates = new()
    {
        [0x131F] = Convert.FromHexString(
            "2400040091CA73956ACC524B0100000004000000000000000000000000000000000000000206500220000000FFFFFFFF00000000000000004B0400000000000058020000000060040000943B0000000000000000000000000000000000000000803F0000000000000000100000000100000000000000DD3E0000000000000000000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000000000000000803F000000000000000000000000000000000000803F000000000000000000000000000000803F0000803F00000000943B000000000000000000000000000000000000000000000000000000000000000000000000000000000000"),
        [0x1323] = Convert.FromHexString(
            "2400040091CA73956ACC524B04000000040000000000000000000000000000000000000002065002F0010000FFFFFFFF04000000000000000000000000000000000000000206500220020000FFFFFFFF04000000000000000000000000000000000000000206500260020000FFFFFFFF04000000000000000000000000000000000000000206500270020000FFFFFFFF2D000000000000000B0400003A3F00000000000000006004000001000000000000000000000000000000000000000000803F00000000000040409600000001000000010000000000000000000000000000000000000000000000000000A041000000000000000000000000000000000000000000000000000000000000000000000000000000000000803F11000000616C6C5F7061727469636C65732E6164720000000000000000000000000000803F000000000000000000000000000000803F0000803F8D3E00000100000000000000000000000000000000000000000000000000000000000000000000000000704100000000"),
        [0x13C4] = Convert.FromHexString(
            "2400040091CA73956ACC524B0100000004000000000000000000000000000000000000004200000070AD6B01FFFFFFFF0E000000000000004B0400000000000058020000000060040000923B0000000000000000000000000000000000000000803F0000000000000000ACDA32000100000000000000E13E000000000000000000000000000000000000000000F041000000000000000000000000000000000000000000000000000000000000000000000000000000000000803F140000006172726F775F70726F6A656374696C652E6164720000000000000000000000000000803F000000000000000000000000000000803F0000803FE13E0000923B000000000000000000000000000000000000000000000000000000000000000000000000704100000000"),
        [0x13C5] = Convert.FromHexString(
            "2400040091CA73956ACC524B0100000004000000000000000000000000000000000000004200000090AE6B01FFFFFFFF0000000000000000E70910004C3F0000000000000000600400007E140000000000000000000000000000000000000000803F00004040000000002EDB3200010000000100000000000000000000000000000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000000000000000803F000000000000000000000000000000000000803F000000000000000000000000000000803F0000803F000000007E14000000000000000000000000000000000000000000000000000000000000000000000000704100000000"),
        [0x1473] = Convert.FromHexString(
            "2400040091CA73956ACC524B0100000004000000000000000000000000000000000000000206500280000000FFFFFFFF2D000000B3000000000000000000000000000000000000000000DB3E0000000000000000000000000000000000000000803F000000000000803F2100000000000000FFFFFFFF0000000000000000000000000100000000000000000000A041000000000000000000000000000000000000000000000000000000000000000000000000000000000000803F11000000616C6C5F7061727469636C65732E6164720000000000000000000000000000803F000000000000000000000000000000803F0000803FDB3E0000DB3E000000000000000000000000000000000000000000000000000000000000000000000000484200000000"),
        [0x1474] = Convert.FromHexString(
            "2400040091CA73956ACC524B04000000040000000000000000000000000000000000000002065002F0010000FFFFFFFF04000000000000000000000000000000000000000206500220020000FFFFFFFF04000000000000000000000000000000000000000206500260020000FFFFFFFF04000000000000000000000000000000000000000206500270020000FFFFFFFF2D000000000000000B0400003A3F00000000000000006004000001000000000000000000000000000000000000000000803F00000000000040409600000001000000010000000000000000000000000000000000000000000000000000A041000000000000000000000000000000000000000000000000000000000000000000000000000000000000803F11000000616C6C5F7061727469636C65732E6164720000000000000000000000000000803F000000000000000000000000000000803F0000803F8D3E00000100000000000000000000000000000000000000000000000000000000000000000000000000704100000000")
    };

    private static readonly Dictionary<int, byte[]> AbilityDefinitions = new()
    {
        [0x121F] = Convert.FromHexString(
            "24000D001F12000001002B6806006D730600070B0000000000000000000000000000943B0000000000004B04000060040000000000000400000001000000000000000000A0400000000000000000000000000000000000000000000000000000003443000000006D730600000000000000000000000000DD3E00000000000000000000000000000003000000000000000000000000000000000000020000002DE600002DE60000010000001F120000000000006400000000000000000300000000503545000000000000000000000000000000000000000000000000"),
        [0x1225] = Convert.FromHexString(
            "24000D00251200000100B56D060059730600000000000000000000000000333F0000D13E00000000000015311000600400000C04000004000000010000000000000000007041000000000000000000000000000000000000000000000000000000344300000000597306000000000000000000000000003C16000000000000000000000000000000000000000000000000000000000000000000000100000052E4000052E400000100000025120000000000006400000000000000000300000000703545000000000000000000000000000000000000000000000000"),
        [0x1226] = Convert.FromHexString(
            "24000D00261200000100A96D060062730600070B0000000000000000000000000000963B0000000000004B04000060040000000000000400000001000000000000000000A04000000000000000000000000000000000000000000000000000000034430000000062730600000000000000000000000000DD3E000000000000000000000000000000030000000000000000000000000000000000000300000053E4000053E400000100000026120000000000006400000000000000000300000000403545000000000000000000000000000000000000000000000000"),
        [0x148A] = Convert.FromHexString(
            "24000D008A1400000100648C0600658C0600070B0000000000000000000000000000563B0000000000004B040000600400000000000004000000010000000000A0400000A040000000000000000000000000000000000000000000000000000000344300000000658C06000000000000000000000000003E16000000000000000000000000000000000000000000000000000000000000000000000500000027E9000027E90000010000008A140000000000006400000000770000000300000000503145000000000000000000000000000000000000000000000000"),
        [0x148D] = Convert.FromHexString(
            "24000D008D14000001006A8C06006B8C06000000000000000000114000000000000055040000000000001C04000060040000280A0000040000000100000000007041000070410000000000000000000000000000000000000000000000000000000000000000006B8C06000000000000000000000000003E16000000000000000000000000000000000000000000000000000000000000000000000500000036E9000036E90000010000008D140000000000006400000000770000000300000000603145000000000000000000000000000000000000000000000000"),
        [0x1473] = Convert.FromHexString(
            "24000D007314000001008E8B06008F8B0600070B0000000000000000000000000000563B0000000000004B04000060040000000000000400000001000000000020410000204124000000FA000000000000000000000000000000000000000000000743000000008F8B060000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000005000000C3E80000C3E800000100000073140000000000006400000000770000000300000000503145000000000000000000000000000000000000000000000000"),
        [0x1474] = Convert.FromHexString(
            "24000D00741400000100908B0600918B06000000000000000000000000001D3F00001F3F0000000000007004000060040000280A000004000000010000000000704100007041000000000000000000000000000000000000000000000000000000000000000000918B060000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000005000000C8E80000C8E800000100000074140000000000006400000000770000000300000000603145000000000000000000000000000000000000000000000000"),
        [0x136E] = Convert.FromHexString(
            "24000D006E13000001001C8B000089730600070B000000000000000000000000000007000000000000004B04000060040000000000000400000001000000000000000000A04000000000000000000000000000000000000000000000000000000000000000000089730600000000000000000000000000E63E00000000000000000000000000000000000000000000000000000000000000000000020000009DE600009DE60000010000006E130000000000006400000000000000000300000000403545000000000000000000000000000000000000000000000000"),
        [0x136F] = Convert.FromHexString(
            "24000D006F1300000100FD1E00007D730600000000000000000000000000683F000000000000000000000E040000000000000C040000040000000100000000000000000070410000000000000000000000000000000000000000000000000000003443000000007D730600000000000000000000000000E63E00000000000000000000000000000005000000000000000000000000000000000000020000009FE600009FE60000050000006F130000C509000064000000000000000003000000007035450000803F0000000000000000693F00000000000000000000"),
        [0x13C4] = Convert.FromHexString(
            "24000D00C41300000100A0690600AD730600070B0000000000000000000000000000923B0000000000004B0400006004000000000000040000000100000000007041000070410E0000000000000000000000000000000000000000000000000000B442E13E0000AD730600000000000000000000000000E13E00000000000000000000000000000000000000000000000000000000000000000000010000006DE700006DE7000001000000C4130000000000006400000000000000000300000000503545000000000000000000000000000000000000000000000000"),
        [0x13C5] = Convert.FromHexString(
            "24000D00C51300000100A2690600097306000000000000000000560400004C3F00007E14000000000000E7091000600400000C0400000400000001000000000070410000704100000000000000000000000000000000000000000000000000000000000000000009730600000040400000000000000000000000000000000000000000000000003000000000000000000000000000000000000010000006EE700006EE7000005000000C5130000C709000064000000000000000003000000006035450000803F00000000000000000000000000000000000000000"),
        [0x131F] = Convert.FromHexString(
            "24000D001F13000001002B6806006D730600070B0000000000000000000000000000943B0000000000004B04000060040000000000000400000001000000000000000000A0400000000000000000000000000000000000000000000000000000003443000000006D730600000000000000000000000000DD3E00000000000000000000000000000003000000000000000000000000000000000000020000002DE600002DE60000010000001F130000000000006400000000000000000300000000503545000000000000000000000000000000000000000000000000"),
        [0x1323] = Convert.FromHexString(
            "24000D002313000001005A680600637306000000000000000000000000003A3F000001000000000000000B040000600400000C040000040000000100000000007041000070412D000000000000000000000000000000000000000000000000000034438D3E0000637306000000000000004040000000000000000000000000000000000000000000000000000000000000000000000000000000000100000032E6000032E600000100000023130000000000006400000000000000000300000000703545000000000000000000000000000000000000000000000000")
    };

    public readonly record struct AbilityDefinitionMutation(int Offset, string Type, string Value);
    private readonly record struct CapturedLaunchTemplate(string Sha1, int Frame, int Length, string FileName, byte[] Payload);
    private readonly record struct CapturedCastChain(string Key, string SourceFile, int StartFrame, int[] AbilityIds, CapturedCastChainPacket[] Packets);
    private readonly record struct CapturedCastChainPacket(string Name, int Frame, string Sha1, int Length, byte[] Payload);

    public static void SendForActiveProfile(GatewayConnection connection, ILogger logger)
    {
        if (!AbilitySetDefinitions.ContainsKey(connection.Player.ActiveProfileId))
        {
            SendEmptyAbilitySetDefinition(connection, connection.Player.ActiveProfileId, logger);
            return;
        }

        SendAbilitySetDefinition(connection, connection.Player.ActiveProfileId, logger, "active-profile");
    }

    public static void SendForActiveProfile(GatewayConnection connection, IResourceManager resourceManager, ILogger logger)
    {
        if (TrySendCustomAbilitySetDefinition(connection, resourceManager, connection.Player.ActiveProfileId, logger, "active-profile"))
            return;

        if (!AbilitySetDefinitions.ContainsKey(connection.Player.ActiveProfileId))
        {
            SendEmptyAbilitySetDefinition(connection, connection.Player.ActiveProfileId, logger);
            return;
        }

        SendAbilitySetDefinition(connection, connection.Player.ActiveProfileId, logger, "active-profile-fallback");
    }

    public static void SendForProfile(GatewayConnection connection, int profileId, ILogger logger)
    {
        if (!AbilitySetDefinitions.ContainsKey(profileId))
        {
            SendEmptyAbilitySetDefinition(connection, profileId, logger);
            return;
        }

        SendAbilitySetDefinition(connection, profileId, logger, "profile-switch");
    }

    public static void SendForProfile(GatewayConnection connection, IResourceManager resourceManager, int profileId, ILogger logger)
    {
        if (TrySendCustomAbilitySetDefinition(connection, resourceManager, profileId, logger, "profile-switch"))
            return;

        if (!AbilitySetDefinitions.ContainsKey(profileId))
        {
            SendEmptyAbilitySetDefinition(connection, profileId, logger);
            return;
        }

        SendAbilitySetDefinition(connection, profileId, logger, "profile-switch-fallback");
    }

    public static void SetTemporaryAbilityOverride(
        GatewayConnection connection,
        int displaySlot,
        int abilityDefinitionId,
        ILogger logger)
    {
        var slot = NormalizeDisplaySlot(displaySlot);

        if (!PlayerAbilityOverrides.TryGetValue(connection.Player.Guid, out var overrides))
        {
            overrides = new Dictionary<int, int>();
            PlayerAbilityOverrides[connection.Player.Guid] = overrides;
        }

        overrides[slot] = abilityDefinitionId;

        logger.LogInformation(
            "Set temporary ability override. ( Player: {player}, DisplaySlot: {displaySlot}, Slot: {slot}, AbilityDefinitionId: {abilityDefinitionId}, AbilityDefinitionHex: {abilityDefinitionHex} )",
            connection.Player.Name.FullName,
            displaySlot,
            slot,
            abilityDefinitionId,
            $"0x{abilityDefinitionId:X}");
    }

    public static void ClearTemporaryAbilityOverride(
        GatewayConnection connection,
        int? displaySlot,
        ILogger logger)
    {
        if (!PlayerAbilityOverrides.TryGetValue(connection.Player.Guid, out var overrides))
            return;

        if (displaySlot is null)
        {
            overrides.Clear();
            PlayerAbilityOverrides.Remove(connection.Player.Guid);
            logger.LogInformation("Cleared all temporary ability overrides. ( Player: {player} )", connection.Player.Name.FullName);
            return;
        }

        var slot = NormalizeDisplaySlot(displaySlot.Value);
        overrides.Remove(slot);

        if (overrides.Count == 0)
            PlayerAbilityOverrides.Remove(connection.Player.Guid);

        logger.LogInformation(
            "Cleared temporary ability override. ( Player: {player}, DisplaySlot: {displaySlot}, Slot: {slot} )",
            connection.Player.Name.FullName,
            displaySlot.Value,
            slot);
    }

    public static void ClearTemporaryAbilityState(GatewayConnection connection, ILogger logger)
    {
        PlayerAbilityOverrides.Remove(connection.Player.Guid);
        PlayerAbilityDefinitions.Remove(connection.Player.Guid);
        PlayerAbilityAliases.Remove(connection.Player.Guid);
        PlayerLaunchAndLandTemplates.Remove(connection.Player.Guid);

        logger.LogInformation(
            "Cleared all temporary ability test state. ( Player: {player}, PlayerGuid: {playerGuid} )",
            connection.Player.Name.FullName,
            connection.Player.Guid);
    }

    public static void RepairAbilityClientState(GatewayConnection connection, IResourceManager? resourceManager, ILogger logger)
    {
        ClearTemporaryAbilityState(connection, logger);
        SendAbilityCastInterrupt(connection, logger);
        SendAbilityFailed(connection, 3079, logger);

        TrySendAbilityDefinition(connection, 0x13C4, logger);
        TrySendAbilityDefinition(connection, 0x13C5, logger);

        if (resourceManager is null)
            SendForActiveProfile(connection, logger);
        else
            SendForActiveProfile(connection, resourceManager, logger);

        logger.LogInformation(
            "Repaired ability client state. ( Player: {player}, ProfileId: {profileId} )",
            connection.Player.Name.FullName,
            connection.Player.ActiveProfileId);
    }

    public static void RestoreKnownArcherAbilities(GatewayConnection connection, IResourceManager? resourceManager, ILogger logger)
    {
        ClearTemporaryAbilityState(connection, logger);
        SetTemporaryAbilityOverride(connection, 1, 0x13C4, logger);
        SetTemporaryAbilityOverride(connection, 2, 0x13C5, logger);

        TrySendAbilityDefinition(connection, 0x13C4, logger);
        TrySendAbilityDefinition(connection, 0x13C5, logger);

        if (resourceManager is null)
            SendForActiveProfile(connection, logger);
        else
            SendForActiveProfile(connection, resourceManager, logger);

        logger.LogInformation(
            "Restored known Archer test abilities. ( Player: {player}, ProfileId: {profileId}, Slot1: {slot1}, Slot2: {slot2} )",
            connection.Player.Name.FullName,
            connection.Player.ActiveProfileId,
            "0x13C4",
            "0x13C5");
    }

    public static string DescribeTemporaryAbilityOverrides(GatewayConnection connection)
    {
        if (!PlayerAbilityOverrides.TryGetValue(connection.Player.Guid, out var overrides) || overrides.Count == 0)
            return "No temporary ability overrides are set.";

        return string.Join("\n", overrides
            .OrderBy(x => x.Key)
            .Select(x => $"slot {x.Key + 1}: {x.Value} / 0x{x.Value:X}"));
    }

    public static string DescribeKnownAbilities(GatewayConnection connection)
    {
        var ids = AbilityDefinitions.Keys
            .Concat(CapturedAbilityDefinitions.Value.Keys)
            .Distinct()
            .OrderBy(x => x)
            .Select(x =>
            {
                var hasDefinition = TryGetAbilityDefinitionPayload(connection, x, out _);
                var hasLaunch = TryGetLaunchAndLandTemplate(connection, x, out _);
                var source = AbilityDefinitions.ContainsKey(x) ? "hardcoded" : "capture";
                return $"0x{x:X4} ({x}) def={(hasDefinition ? "yes" : "no")} launch={(hasLaunch ? "yes" : "no")} source={source}";
            });

        return "Known abilities:\n" + string.Join("\n", ids);
    }

    public static string DescribeCapturedLaunchTemplates(int count)
    {
        count = Math.Clamp(count, 1, 50);

        var templates = CapturedLaunchTemplates.Value
            .OrderBy(x => x.Frame)
            .Take(count)
            .Select(x => $"frame={x.Frame} sha={x.Sha1} len={x.Length} file={x.FileName}");

        return $"Captured launch templates (first {count}):\n" + string.Join("\n", templates);
    }

    public static string DescribeCapturedCastChains(int count, int? abilityDefinitionId = null)
    {
        count = Math.Clamp(count, 1, 50);

        var chains = CapturedCastChains.Value.AsEnumerable();
        if (abilityDefinitionId is not null)
            chains = chains.Where(x => x.AbilityIds.Contains(abilityDefinitionId.Value));

        var rows = chains
            .OrderBy(x => x.StartFrame)
            .Take(count)
            .Select(x =>
            {
                var launchCount = x.Packets.Count(packet => packet.Name == "AbilityPacketLaunchAndLand");
                var attackCount = x.Packets.Count(packet => packet.Name == "CombatPacketAttackProcessed");
                return $"start={x.StartFrame} abilities={string.Join("/", x.AbilityIds.Select(id => $"0x{id:X4}"))} packets={x.Packets.Length} launches={launchCount} attacks={attackCount} file={x.SourceFile}";
            });

        return $"Captured cast chains (first {count}):\n" + string.Join("\n", rows);
    }

    private static bool TrySendCustomAbilitySetDefinition(
        GatewayConnection connection,
        IResourceManager? resourceManager,
        int profileId,
        ILogger logger,
        string reason)
    {
        if (!AbilitySetDefinitions.TryGetValue(profileId, out var template))
            return false;

        if (!ProfileSlotAbilityIds.TryGetValue(profileId, out var fallbackAbilityIds))
            return false;

        var effectiveAbilityIds = fallbackAbilityIds.ToArray();
        var changed = false;

        if (resourceManager is not null &&
            TryGetEquippedWeaponAbilityIds(connection, resourceManager, logger, out var weaponAbilityIds) &&
            weaponAbilityIds.Length > 0)
        {
            for (var i = 0; i < effectiveAbilityIds.Length && i < weaponAbilityIds.Length; i++)
            {
                if (weaponAbilityIds[i] <= 0)
                    continue;

                effectiveAbilityIds[i] = weaponAbilityIds[i];
                changed = true;
            }
        }

        if (PlayerAbilityOverrides.TryGetValue(connection.Player.Guid, out var overrides))
        {
            foreach (var (slot, abilityDefinitionId) in overrides)
            {
                if (slot < 0 || slot >= effectiveAbilityIds.Length)
                    continue;

                effectiveAbilityIds[slot] = abilityDefinitionId;
                changed = true;
            }
        }

        if (!changed)
            return false;

        var payload = new byte[template.Length];
        Array.Copy(template, payload, payload.Length);

        for (var i = 0; i < fallbackAbilityIds.Length && i < effectiveAbilityIds.Length; i++)
            ReplaceInt32(payload, fallbackAbilityIds[i], effectiveAbilityIds[i]);

        logger.LogInformation(
            "Sending custom ability set definition. ( ProfileId: {profileId}, Reason: {reason}, Abilities: {abilities} )",
            profileId,
            reason,
            string.Join(", ", effectiveAbilityIds.Select((x, i) => $"slot {i + 1}={x}/0x{x:X}")));

        connection.SendTunneled(new RawPacket(payload));
        return true;
    }

    private static int NormalizeDisplaySlot(int displaySlot)
    {
        return displaySlot switch
        {
            1 => 0,
            2 => 1,
            _ => displaySlot
        };
    }

    public static bool TrySendAbilityDefinition(GatewayConnection connection, int abilityDefinitionId, ILogger logger)
    {
        if (!TryGetAbilityDefinitionPayload(connection, abilityDefinitionId, out var payload))
            return false;

        logger.LogInformation("Sending ability definition. ( AbilityDefinitionId: {abilityDefinitionId}, RuntimeOverride: {runtimeOverride} )",
            abilityDefinitionId,
            PlayerAbilityDefinitions.TryGetValue(connection.Player.Guid, out var definitions) && definitions.ContainsKey(abilityDefinitionId));
        connection.SendTunneled(new RawPacket(payload));
        return true;
    }

    public static bool TrySendMutatedAbilityDefinition(
        GatewayConnection connection,
        int abilityDefinitionId,
        IReadOnlyList<AbilityDefinitionMutation> mutations,
        ILogger logger,
        out string result)
    {
        if (!TryGetAbilityDefinitionPayload(connection, abilityDefinitionId, out var template))
        {
            result = $"No captured AbilityDefinition blob is known for {abilityDefinitionId} / 0x{abilityDefinitionId:X}.";
            return false;
        }

        if (mutations.Count == 0)
        {
            result = "No mutations provided.";
            return false;
        }

        var payload = new byte[template.Length];
        Array.Copy(template, payload, payload.Length);

        var applied = new List<string>();
        var effectiveAbilityDefinitionId = abilityDefinitionId;

        foreach (var mutation in mutations)
        {
            if (!TryApplyMutation(payload, mutation, out var description, out var error))
            {
                result = error;
                return false;
            }

            applied.Add(description);

            if (mutation.Offset == 4 &&
                (mutation.Type.Equals("i32", StringComparison.OrdinalIgnoreCase) ||
                 mutation.Type.Equals("int", StringComparison.OrdinalIgnoreCase) ||
                 mutation.Type.Equals("u32", StringComparison.OrdinalIgnoreCase) ||
                 mutation.Type.Equals("uint", StringComparison.OrdinalIgnoreCase)) &&
                TryParseInt32(mutation.Value, out var mutatedAbilityDefinitionId) &&
                mutatedAbilityDefinitionId > 0)
            {
                effectiveAbilityDefinitionId = mutatedAbilityDefinitionId;
            }
        }

        if (effectiveAbilityDefinitionId != abilityDefinitionId)
        {
            ReplaceInt32(payload, abilityDefinitionId, effectiveAbilityDefinitionId);
            WriteInt32(payload, 4, effectiveAbilityDefinitionId);
            applied.Add($"replace-all-id={abilityDefinitionId}/0x{abilityDefinitionId:X}->{effectiveAbilityDefinitionId}/0x{effectiveAbilityDefinitionId:X}");

            if (!PlayerAbilityAliases.TryGetValue(connection.Player.Guid, out var playerAliases))
            {
                playerAliases = new Dictionary<int, int>();
                PlayerAbilityAliases[connection.Player.Guid] = playerAliases;
            }

            playerAliases[effectiveAbilityDefinitionId] = abilityDefinitionId;
        }

        if (!PlayerAbilityDefinitions.TryGetValue(connection.Player.Guid, out var playerDefinitions))
        {
            playerDefinitions = new Dictionary<int, byte[]>();
            PlayerAbilityDefinitions[connection.Player.Guid] = playerDefinitions;
        }

        playerDefinitions[effectiveAbilityDefinitionId] = payload;

        logger.LogInformation(
            "Sending mutated ability definition. ( SourceAbilityDefinitionId: {abilityDefinitionId}, EffectiveAbilityDefinitionId: {effectiveAbilityDefinitionId}, Mutations: {mutations}, Length: {length} )",
            abilityDefinitionId,
            effectiveAbilityDefinitionId,
            string.Join("; ", applied),
            payload.Length);

        connection.SendTunneled(new RawPacket(payload));
        result = $"Sent mutated ability definition {effectiveAbilityDefinitionId} / 0x{effectiveAbilityDefinitionId:X}. Mutations: {string.Join("; ", applied)}";
        return true;
    }

    public static bool TrySendMutatedLaunchAndLand(
        GatewayConnection connection,
        int abilityDefinitionId,
        IReadOnlyList<AbilityDefinitionMutation> mutations,
        ILogger logger,
        out string result)
    {
        var behaviorAbilityDefinitionId = ResolveAbilityBehaviorId(connection, abilityDefinitionId);

        if (!TryGetLaunchAndLandTemplate(connection, behaviorAbilityDefinitionId, out var template))
        {
            result = $"No LaunchAndLand template is known for {abilityDefinitionId} / 0x{abilityDefinitionId:X}.";
            return false;
        }

        if (mutations.Count == 0)
        {
            result = "No mutations provided.";
            return false;
        }

        var payload = new byte[template.Length];
        Array.Copy(template, payload, payload.Length);

        var applied = new List<string>();

        foreach (var mutation in mutations)
        {
            if (!TryApplyMutation(payload, mutation, out var description, out var error))
            {
                result = error;
                return false;
            }

            applied.Add(description);
        }

        if (!PlayerLaunchAndLandTemplates.TryGetValue(connection.Player.Guid, out var playerTemplates))
        {
            playerTemplates = new Dictionary<int, byte[]>();
            PlayerLaunchAndLandTemplates[connection.Player.Guid] = playerTemplates;
        }

        playerTemplates[behaviorAbilityDefinitionId] = payload;

        logger.LogInformation(
            "Stored mutated launch-and-land template. ( AbilityDefinitionId: {abilityDefinitionId}, BehaviorAbilityDefinitionId: {behaviorAbilityDefinitionId}, Mutations: {mutations}, Length: {length} )",
            abilityDefinitionId,
            behaviorAbilityDefinitionId,
            string.Join("; ", applied),
            payload.Length);

        result = $"Stored mutated launch template for 0x{behaviorAbilityDefinitionId:X}. Mutations: {string.Join("; ", applied)}";
        return true;
    }

    private static bool TryGetAbilityDefinitionPayload(GatewayConnection connection, int abilityDefinitionId, out byte[] payload)
    {
        if (PlayerAbilityDefinitions.TryGetValue(connection.Player.Guid, out var playerDefinitions) &&
            playerDefinitions.TryGetValue(abilityDefinitionId, out payload!))
        {
            return true;
        }

        if (IsKnownStableArcherAbility(abilityDefinitionId) &&
            AbilityDefinitions.TryGetValue(abilityDefinitionId, out payload!))
        {
            return true;
        }

        if (CapturedAbilityDefinitions.Value.TryGetValue(abilityDefinitionId, out payload!))
            return true;

        return AbilityDefinitions.TryGetValue(abilityDefinitionId, out payload!);
    }

    public static bool TrySendStartCasting(GatewayConnection connection, int slot, ulong targetGuid, ILogger logger)
    {
        return TrySendStartCasting(connection, null, slot, targetGuid, logger);
    }

    public static bool TrySendStartCasting(GatewayConnection connection, IResourceManager? resourceManager, int slot, ulong targetGuid, ILogger logger)
    {
        if (!TryResolveAbilityDefinitionId(connection, resourceManager, slot, logger, out var abilityDefinitionId))
        {
            logger.LogInformation(
                "No ability mapping found. ( ProfileId: {profileId}, Slot: {slot} )",
                connection.Player.ActiveProfileId,
                slot);
            return false;
        }

        SendAbilityExecution(connection, abilityDefinitionId, slot, targetGuid, logger, "slot-request", out _, out _);
        return true;
    }

    public static bool TrySendAbilityById(
        GatewayConnection connection,
        int abilityDefinitionId,
        ulong targetGuid,
        ILogger logger,
        out string result)
    {
        if (abilityDefinitionId <= 0)
        {
            result = "Ability id must be greater than zero.";
            return false;
        }

        var definitionKnown = TrySendAbilityDefinition(connection, abilityDefinitionId, logger);
        var sentLaunch = SendAbilityExecution(connection, abilityDefinitionId, -1, targetGuid, logger, "direct-test", out var resolvedTargetGuid, out var appliedDamage);

        result =
            $"Direct ability cast sent: {abilityDefinitionId} / 0x{abilityDefinitionId:X}. " +
            $"definition={(definitionKnown ? "yes" : "no")} launch={(sentLaunch ? "yes" : "no")} damage={(appliedDamage ? "yes" : "no")} target={resolvedTargetGuid}.";

        return true;
    }

    public static bool TrySendCapturedLaunchTemplate(
        GatewayConnection connection,
        string key,
        int abilityDefinitionId,
        ulong targetGuid,
        ILogger logger,
        out string result)
    {
        result = string.Empty;

        if (!TryFindCapturedLaunchTemplate(key, out var template))
        {
            result = $"No captured LaunchAndLand template found for key '{key}'. Use !launches to list candidates.";
            return false;
        }

        var resolvedTargetGuid = ResolveAbilityTarget(connection, targetGuid);
        var casterGuid = connection.Player.Guid;

        var payload = new byte[template.Payload.Length];
        Array.Copy(template.Payload, payload, payload.Length);

        WriteUInt64(payload, 4, casterGuid);
        if (payload.Length >= 44)
            WriteUInt64(payload, 36, resolvedTargetGuid);

        logger.LogInformation(
            "Sending captured launch template by key. ( Key: {key}, Frame: {frame}, Sha1: {sha1}, Length: {length}, AbilityDefinitionId: {abilityDefinitionId}, CasterGuid: {casterGuid}, TargetGuid: {targetGuid} )",
            key,
            template.Frame,
            template.Sha1,
            template.Length,
            abilityDefinitionId,
            casterGuid,
            resolvedTargetGuid);

        connection.SendTunneled(new RawPacket(payload));
        result = $"Launch template sent: frame={template.Frame} sha={template.Sha1} len={template.Length} ability=0x{abilityDefinitionId:X} target={resolvedTargetGuid}.";
        return true;
    }

    public static bool TryReplayCapturedCastChain(
        GatewayConnection connection,
        string key,
        int? abilityDefinitionIdOverride,
        ulong targetGuid,
        ILogger logger,
        out string result)
    {
        result = string.Empty;

        if (!TryFindCapturedCastChain(key, out var chain))
        {
            result = $"No captured cast chain found for key '{key}'. Use !chains to list candidates.";
            return false;
        }

        var abilityDefinitionId = abilityDefinitionIdOverride ?? chain.AbilityIds.FirstOrDefault();
        if (abilityDefinitionId <= 0)
        {
            result = $"Cast chain {chain.StartFrame} has no ability id candidate. Provide one: !chain {key} 0x13C5";
            return false;
        }

        var casterGuid = connection.Player.Guid;
        var resolvedTargetGuid = ResolveAbilityTarget(connection, targetGuid);

        using var writer = new PacketWriter();
        writer.Write((short)BaseAbilityPacket.OpCode);
        writer.Write((short)3);
        writer.Write(casterGuid);
        writer.Write(resolvedTargetGuid);
        writer.Write(0);
        writer.Write(0);
        writer.Write(abilityDefinitionId);
        writer.Write(0);
        writer.Write((byte)0);
        connection.SendTunneled(new RawPacket(writer.Buffer));

        var launchesSent = 0;
        foreach (var packet in chain.Packets.Where(packet => packet.Name == "AbilityPacketLaunchAndLand"))
        {
            var payload = new byte[packet.Payload.Length];
            Array.Copy(packet.Payload, payload, payload.Length);

            WriteUInt64(payload, 4, casterGuid);
            if (payload.Length >= 44)
                WriteUInt64(payload, 36, resolvedTargetGuid);

            connection.SendTunneled(new RawPacket(payload));
            launchesSent++;
        }

        var appliedDamage = TryApplyAbilityDamage(connection, abilityDefinitionId, resolvedTargetGuid, logger);

        logger.LogInformation(
            "Replayed captured cast chain. ( Key: {key}, StartFrame: {startFrame}, AbilityDefinitionId: {abilityDefinitionId}, LaunchesSent: {launchesSent}, AppliedDamage: {appliedDamage}, TargetGuid: {targetGuid} )",
            key,
            chain.StartFrame,
            abilityDefinitionId,
            launchesSent,
            appliedDamage,
            resolvedTargetGuid);

        result = $"Cast chain replayed: start={chain.StartFrame} ability=0x{abilityDefinitionId:X} launches={launchesSent} damage={(appliedDamage ? "yes" : "no")} target={resolvedTargetGuid}.";
        return true;
    }

    private static bool TryFindCapturedCastChain(string key, out CapturedCastChain chain)
    {
        key = key.Trim();
        foreach (var candidate in CapturedCastChains.Value)
        {
            if (candidate.Key.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                candidate.StartFrame.ToString(CultureInfo.InvariantCulture).Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                chain = candidate;
                return true;
            }
        }

        chain = default;
        return false;
    }

    private static bool TryFindCapturedLaunchTemplate(string key, out CapturedLaunchTemplate template)
    {
        key = key.Trim();

        if (key.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            key = key[2..];

        foreach (var candidate in CapturedLaunchTemplates.Value)
        {
            if (candidate.Sha1.StartsWith(key, StringComparison.OrdinalIgnoreCase) ||
                candidate.FileName.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frame) && candidate.Frame == frame)
            {
                template = candidate;
                return true;
            }
        }

        template = default;
        return false;
    }

    private static bool SendAbilityExecution(
        GatewayConnection connection,
        int abilityDefinitionId,
        int slot,
        ulong targetGuid,
        ILogger logger,
        string reason,
        out ulong resolvedTargetGuid,
        out bool appliedDamage)
    {
        var casterGuid = connection.Player.Guid;
        resolvedTargetGuid = ResolveAbilityTarget(connection, targetGuid);

        using var writer = new PacketWriter();

        writer.Write((short)36);
        writer.Write((short)3);
        writer.Write(casterGuid);
        writer.Write(resolvedTargetGuid);
        writer.Write(0);
        writer.Write(0);
        writer.Write(abilityDefinitionId);
        writer.Write(0);
        writer.Write((byte)0);

        var behaviorAbilityDefinitionId = ResolveAbilityBehaviorId(connection, abilityDefinitionId);

        logger.LogInformation(
            "Sending ability start-casting. ( ProfileId: {profileId}, Slot: {slot}, AbilityDefinitionId: {abilityDefinitionId}, BehaviorAbilityDefinitionId: {behaviorAbilityDefinitionId}, CasterGuid: {casterGuid}, TargetGuid: {targetGuid}, Reason: {reason} )",
            connection.Player.ActiveProfileId,
            slot,
            abilityDefinitionId,
            behaviorAbilityDefinitionId,
            casterGuid,
            resolvedTargetGuid,
            reason);

        connection.SendTunneled(new RawPacket(writer.Buffer));

        var sentLaunch = TrySendLaunchAndLand(connection, behaviorAbilityDefinitionId, casterGuid, resolvedTargetGuid, logger);
        appliedDamage = TryApplyAbilityDamage(connection, behaviorAbilityDefinitionId, resolvedTargetGuid, logger);
        return sentLaunch;
    }

    private static bool TryResolveAbilityDefinitionId(
        GatewayConnection connection,
        IResourceManager? resourceManager,
        int requestedSlot,
        ILogger logger,
        out int abilityDefinitionId)
    {
        abilityDefinitionId = 0;

        if (PlayerAbilityOverrides.TryGetValue(connection.Player.Guid, out var overrides) &&
            overrides.TryGetValue(requestedSlot, out var overrideAbilityDefinitionId))
        {
            abilityDefinitionId = overrideAbilityDefinitionId;

            logger.LogInformation(
                "Resolved ability from temporary override. ( ProfileId: {profileId}, Slot: {slot}, AbilityDefinitionId: {abilityDefinitionId}, AbilityDefinitionHex: {abilityDefinitionHex} )",
                connection.Player.ActiveProfileId,
                requestedSlot,
                abilityDefinitionId,
                $"0x{abilityDefinitionId:X}");

            return true;
        }

        if (PlayerAbilityOverrides.TryGetValue(connection.Player.Guid, out overrides) &&
            OneBasedSlotAliases.TryGetValue(requestedSlot, out var zeroBasedSlot) &&
            overrides.TryGetValue(zeroBasedSlot, out overrideAbilityDefinitionId))
        {
            abilityDefinitionId = overrideAbilityDefinitionId;

            logger.LogInformation(
                "Resolved ability from temporary override using one-based slot alias. ( ProfileId: {profileId}, RequestedSlot: {requestedSlot}, Slot: {slot}, AbilityDefinitionId: {abilityDefinitionId}, AbilityDefinitionHex: {abilityDefinitionHex} )",
                connection.Player.ActiveProfileId,
                requestedSlot,
                zeroBasedSlot,
                abilityDefinitionId,
                $"0x{abilityDefinitionId:X}");

            return true;
        }

        if (resourceManager is not null &&
            TryGetEquippedWeaponAbilityIds(connection, resourceManager, logger, out var weaponAbilityIds) &&
            requestedSlot >= 0 &&
            requestedSlot < weaponAbilityIds.Length)
        {
            abilityDefinitionId = weaponAbilityIds[requestedSlot];

            logger.LogInformation(
                "Resolved ability from equipped weapon. ( ProfileId: {profileId}, Slot: {slot}, AbilityDefinitionId: {abilityDefinitionId} )",
                connection.Player.ActiveProfileId,
                requestedSlot,
                abilityDefinitionId);

            return true;
        }

        if (ProfileSlotAbilityIds.TryGetValue(connection.Player.ActiveProfileId, out var profileAbilityIds) &&
            requestedSlot >= 0 &&
            requestedSlot < profileAbilityIds.Length)
        {
            abilityDefinitionId = profileAbilityIds[requestedSlot];

            logger.LogInformation(
                "Resolved ability from profile fallback. ( ProfileId: {profileId}, Slot: {slot}, AbilityDefinitionId: {abilityDefinitionId} )",
                connection.Player.ActiveProfileId,
                requestedSlot,
                abilityDefinitionId);

            return true;
        }

        if (ProfileSlotAbilityIds.TryGetValue(connection.Player.ActiveProfileId, out profileAbilityIds) &&
            OneBasedSlotAliases.TryGetValue(requestedSlot, out zeroBasedSlot) &&
            zeroBasedSlot >= 0 &&
            zeroBasedSlot < profileAbilityIds.Length)
        {
            abilityDefinitionId = profileAbilityIds[zeroBasedSlot];

            logger.LogInformation(
                "Resolved ability from profile fallback using one-based slot alias. ( ProfileId: {profileId}, RequestedSlot: {requestedSlot}, Slot: {slot}, AbilityDefinitionId: {abilityDefinitionId} )",
                connection.Player.ActiveProfileId,
                requestedSlot,
                zeroBasedSlot,
                abilityDefinitionId);

            return true;
        }

        return false;
    }

    private static int ResolveAbilityBehaviorId(GatewayConnection connection, int abilityDefinitionId)
    {
        if (PlayerAbilityAliases.TryGetValue(connection.Player.Guid, out var playerAliases) &&
            playerAliases.TryGetValue(abilityDefinitionId, out var sourceAbilityDefinitionId))
        {
            return sourceAbilityDefinitionId;
        }

        return abilityDefinitionId;
    }

    private static bool TryGetEquippedWeaponAbilityIds(
        GatewayConnection connection,
        IResourceManager resourceManager,
        ILogger logger,
        out int[] abilityIds)
    {
        abilityIds = [];

        var profile = connection.Player.Profiles
            .SingleOrDefault(x => x.Id == connection.Player.ActiveProfileId);

        if (profile is null)
        {
            logger.LogInformation(
                "Could not resolve equipped weapon abilities because active profile was not found. ( ProfileId: {profileId} )",
                connection.Player.ActiveProfileId);

            return false;
        }

        if (!profile.Items.TryGetValue(WeaponSlot, out var weaponProfileItem))
        {
            logger.LogInformation(
                "Could not resolve equipped weapon abilities because no weapon is equipped. ( ProfileId: {profileId}, WeaponSlot: {weaponSlot} )",
                connection.Player.ActiveProfileId,
                WeaponSlot);

            return false;
        }

        var weaponItem = connection.Player.Items
            .SingleOrDefault(x => x.Id == weaponProfileItem.Id);

        if (weaponItem is null)
        {
            logger.LogInformation(
                "Could not resolve equipped weapon abilities because weapon item was not found. ( WeaponItemId: {weaponItemId} )",
                weaponProfileItem.Id);

            return false;
        }

        if (!resourceManager.ClientItemDefinitions.TryGetValue(weaponItem.Definition, out var weaponDefinition))
        {
            logger.LogInformation(
                "Could not resolve equipped weapon abilities because weapon definition was not found. ( WeaponDefinitionId: {weaponDefinitionId} )",
                weaponItem.Definition);

            return false;
        }

        abilityIds = weaponDefinition.Abilities
            .Where(x => x.Id > 0)
            .OrderBy(x => x.Slot)
            .Select(x => x.Id)
            .ToArray();

        logger.LogInformation(
            "Resolved equipped weapon ability list. ( ProfileId: {profileId}, WeaponDefinitionId: {weaponDefinitionId}, Abilities: {abilities} )",
            connection.Player.ActiveProfileId,
            weaponItem.Definition,
            string.Join(", ", weaponDefinition.Abilities.Select(x => $"slot={x.Slot}/id={x.Id}/unk={x.Unknown}/icon={x.IconId}")));

        return abilityIds.Length > 0;
    }

    private static ulong ResolveAbilityTarget(GatewayConnection connection, ulong requestedTargetGuid)
    {
        if (requestedTargetGuid != 0 &&
            requestedTargetGuid != connection.Player.Guid &&
            connection.Player.Zone.TryGetNpc(requestedTargetGuid, out var requestedTarget) &&
            Vector4.DistanceSquared(connection.Player.Position, requestedTarget.Position) <= AutoTargetRangeSquared)
        {
            return requestedTargetGuid;
        }

        var nearestHostileNpc = connection.Player.Zone.Npcs
            .Where(x => x.Visible && x.Disposition == 0)
            .Where(x => Vector4.DistanceSquared(connection.Player.Position, x.Position) <= AutoTargetRangeSquared)
            .OrderBy(x => Vector4.DistanceSquared(connection.Player.Position, x.Position))
            .FirstOrDefault();

        return nearestHostileNpc?.Guid ?? 0;
    }

    private static bool TrySendLaunchAndLand(GatewayConnection connection, int abilityDefinitionId, ulong casterGuid, ulong targetGuid, ILogger logger)
    {
        if (!TryGetLaunchAndLandTemplate(connection, abilityDefinitionId, out var template))
        {
            logger.LogInformation(
                "No captured ability launch-and-land template. ( ProfileId: {profileId}, AbilityDefinitionId: {abilityDefinitionId} )",
                connection.Player.ActiveProfileId,
                abilityDefinitionId);
            return false;
        }

        var payload = new byte[template.Length];
        Array.Copy(template, payload, payload.Length);

        WriteUInt64(payload, 4, casterGuid);
        if (abilityDefinitionId is 0x13C4 or 0x13C5 or 0x1473 or 0x1474 && payload.Length >= 44)
            WriteUInt64(payload, 36, targetGuid);

        logger.LogInformation(
            "Sending captured ability launch-and-land. ( ProfileId: {profileId}, AbilityDefinitionId: {abilityDefinitionId}, CasterGuid: {casterGuid}, TargetGuid: {targetGuid}, Length: {length} )",
            connection.Player.ActiveProfileId,
            abilityDefinitionId,
            casterGuid,
            targetGuid,
            payload.Length);

        connection.SendTunneled(new RawPacket(payload));
        return true;
    }

    private static void SendAbilityCastInterrupt(GatewayConnection connection, ILogger logger)
    {
        using var writer = new PacketWriter();
        writer.Write((short)BaseAbilityPacket.OpCode);
        writer.Write((short)18);

        logger.LogInformation("Sending ability cast interrupt reset. ( Player: {player} )", connection.Player.Name.FullName);
        connection.SendTunneled(new RawPacket(writer.Buffer));
    }

    private static void SendAbilityFailed(GatewayConnection connection, int stringId, ILogger logger)
    {
        using var writer = new PacketWriter();
        writer.Write((short)BaseAbilityPacket.OpCode);
        writer.Write((short)1);
        writer.Write(stringId);

        logger.LogInformation(
            "Sending ability failed reset. ( Player: {player}, StringId: {stringId} )",
            connection.Player.Name.FullName,
            stringId);

        connection.SendTunneled(new RawPacket(writer.Buffer));
    }

    private static bool TryGetLaunchAndLandTemplate(GatewayConnection connection, int abilityDefinitionId, out byte[] payload)
    {
        if (PlayerLaunchAndLandTemplates.TryGetValue(connection.Player.Guid, out var playerTemplates) &&
            playerTemplates.TryGetValue(abilityDefinitionId, out payload!))
        {
            return true;
        }

        if (IsKnownStableArcherAbility(abilityDefinitionId) &&
            LaunchAndLandTemplates.TryGetValue(abilityDefinitionId, out payload!))
        {
            return true;
        }

        if (CapturedLaunchAndLandTemplates.Value.TryGetValue(abilityDefinitionId, out payload!))
            return true;

        return LaunchAndLandTemplates.TryGetValue(abilityDefinitionId, out payload!);
    }

    private static bool IsKnownStableArcherAbility(int abilityDefinitionId)
    {
        return abilityDefinitionId is 0x13C4 or 0x13C5;
    }

    private static bool TryApplyAbilityDamage(GatewayConnection connection, int abilityDefinitionId, ulong targetGuid, ILogger logger)
    {
        if (targetGuid == 0 ||
            !connection.Player.Zone.TryGetNpc(targetGuid, out var target) ||
            !target.HasHealthBar ||
            target.CurrentHitpoints <= 0)
        {
            return false;
        }

        var damage = GetAbilityDamage(abilityDefinitionId);
        target.CurrentHitpoints = Math.Max(0, target.CurrentHitpoints - damage);

        SendHitPointModification(connection, target.Guid, damage, target.CurrentHitpoints, target.MaxHitpoints);
        SendAttackTargetDamage(connection, target.Guid, damage);
        SendAttackProcessed(connection, abilityDefinitionId, target.Guid, damage);

        logger.LogInformation(
            "Applied combat damage. ( AbilityDefinitionId: {abilityDefinitionId}, TargetGuid: {targetGuid}, Damage: {damage}, CurrentHitpoints: {currentHitpoints}, MaxHitpoints: {maxHitpoints} )",
            abilityDefinitionId,
            target.Guid,
            damage,
            target.CurrentHitpoints,
            target.MaxHitpoints);

        if (target.CurrentHitpoints == 0)
            target.Dispose();

        return true;
    }

    private static void SendAttackProcessed(GatewayConnection connection, int abilityDefinitionId, ulong targetGuid, int damage)
    {
        using var writer = new PacketWriter();

        writer.Write((short)BaseCombatPacket.OpCode);
        writer.Write((short)7);
        writer.Write(targetGuid);
        writer.Write(targetGuid);
        writer.Write(++_combatEventGuid);
        writer.Write(25);
        writer.Write(damage);
        writer.Write(abilityDefinitionId);
        writer.Write(0);
        writer.Write(false);
        writer.Write(false);
        writer.Write(damage);

        connection.Player.SendTunneledToVisible(new RawPacket(writer.Buffer), sendToSelf: true);
    }

    private static void SendAttackTargetDamage(GatewayConnection connection, ulong targetGuid, int damage)
    {
        using var writer = new PacketWriter();

        writer.Write((short)BaseCombatPacket.OpCode);
        writer.Write((short)4);
        writer.Write(connection.Player.Guid);
        writer.Write(targetGuid);
        writer.Write(damage);
        writer.Write(false);

        connection.Player.SendTunneledToVisible(new RawPacket(writer.Buffer), sendToSelf: true);
    }

    private static void SendHitPointModification(GatewayConnection connection, ulong targetGuid, int damage, int currentHitpoints, int maxHitpoints)
    {
        using var writer = new PacketWriter();

        writer.Write((short)BasePlayerUpdatePacket.OpCode);
        writer.Write((short)35);
        writer.Write(targetGuid);
        writer.Write(connection.Player.Guid);
        writer.Write(false);
        writer.Write(-damage);
        writer.Write(currentHitpoints);
        writer.Write(maxHitpoints);
        writer.Write(false);

        connection.Player.SendTunneledToVisible(new RawPacket(writer.Buffer), sendToSelf: true);
    }

    private static int GetAbilityDamage(int abilityDefinitionId)
    {
        return abilityDefinitionId switch
        {
            0x1323 or 0x13C5 or 0x1474 or 0x148D => 175,
            _ => 100
        };
    }

    private static Dictionary<int, byte[]> LoadCapturedAbilityDefinitions()
    {
        var definitions = new Dictionary<int, byte[]>();
        var directory = Path.Combine(AppContext.BaseDirectory, "Resources", "Abilities", "Definitions");

        if (!Directory.Exists(directory))
            return definitions;

        foreach (var path in Directory.EnumerateFiles(directory, "*.bin"))
        {
            var payload = File.ReadAllBytes(path);
            if (payload.Length < 8 ||
                BitConverter.ToInt16(payload, 0) != BaseAbilityPacket.OpCode ||
                BitConverter.ToInt16(payload, 2) != 13)
            {
                continue;
            }

            var abilityDefinitionId = BitConverter.ToInt32(payload, 4);
            definitions[abilityDefinitionId] = payload;
        }

        return definitions;
    }

    private static Dictionary<int, byte[]> LoadCapturedLaunchAndLandTemplates()
    {
        var templates = new Dictionary<int, byte[]>();
        var directory = Path.Combine(AppContext.BaseDirectory, "Resources", "Abilities", "LaunchAndLand");

        if (!Directory.Exists(directory))
            return templates;

        var payloadsBySha1 = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(directory, "*.bin"))
        {
            var payload = File.ReadAllBytes(path);
            if (payload.Length < 4 ||
                BitConverter.ToInt16(payload, 0) != BaseAbilityPacket.OpCode ||
                BitConverter.ToInt16(payload, 2) != 4)
            {
                continue;
            }

            payloadsBySha1.TryAdd(GetShortSha1(payload), payload);
        }

        foreach (var (abilityDefinitionId, sha1) in LoadCapturedLaunchAndLandMap())
        {
            if (payloadsBySha1.TryGetValue(sha1, out var payload))
                templates.TryAdd(abilityDefinitionId, payload);
        }

        var knownAbilityIds = AbilityDefinitions.Keys
            .Concat(CapturedAbilityDefinitions.Value.Keys)
            .Distinct()
            .OrderByDescending(x => x)
            .ToArray();

        foreach (var payload in payloadsBySha1.Values)
        {
            foreach (var abilityDefinitionId in knownAbilityIds)
            {
                if (templates.ContainsKey(abilityDefinitionId))
                    continue;

                if (!ContainsInt32(payload, abilityDefinitionId))
                    continue;

                templates.TryAdd(abilityDefinitionId, payload);
                break;
            }
        }

        return templates;
    }

    private static List<CapturedLaunchTemplate> LoadCapturedLaunchTemplates()
    {
        var templates = new List<CapturedLaunchTemplate>();
        var directory = Path.Combine(AppContext.BaseDirectory, "Resources", "Abilities", "LaunchAndLand");

        if (!Directory.Exists(directory))
            return templates;

        foreach (var path in Directory.EnumerateFiles(directory, "*.bin"))
        {
            var payload = File.ReadAllBytes(path);
            if (payload.Length < 4 ||
                BitConverter.ToInt16(payload, 0) != BaseAbilityPacket.OpCode ||
                BitConverter.ToInt16(payload, 2) != 4)
            {
                continue;
            }

            var fileName = Path.GetFileName(path);
            templates.Add(new CapturedLaunchTemplate(
                GetShortSha1(payload),
                TryReadFrameFromLaunchFileName(fileName),
                payload.Length,
                fileName,
                payload));
        }

        return templates
            .OrderBy(x => x.Frame)
            .ThenBy(x => x.Sha1, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int TryReadFrameFromLaunchFileName(string fileName)
    {
        const string marker = "_frame";
        var start = fileName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return 0;

        start += marker.Length;
        var end = fileName.IndexOf('_', start);
        if (end < 0)
            return 0;

        return int.TryParse(fileName[start..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out var frame)
            ? frame
            : 0;
    }

    private static List<CapturedCastChain> LoadCapturedCastChains()
    {
        var chains = new List<CapturedCastChain>();
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "Abilities", "CastChains", "cast_chains.json");

        if (!File.Exists(path))
            return chains;

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return chains;

        foreach (var chainElement in document.RootElement.EnumerateArray())
        {
            var packets = new List<CapturedCastChainPacket>();
            foreach (var packetElement in chainElement.GetProperty("packets").EnumerateArray())
            {
                packets.Add(new CapturedCastChainPacket(
                    packetElement.GetProperty("name").GetString() ?? string.Empty,
                    packetElement.GetProperty("frame").GetInt32(),
                    packetElement.GetProperty("sha1").GetString() ?? string.Empty,
                    packetElement.GetProperty("length").GetInt32(),
                    Convert.FromHexString(packetElement.GetProperty("payload_hex").GetString() ?? string.Empty)));
            }

            var abilityIds = chainElement.GetProperty("ability_ids")
                .EnumerateArray()
                .Select(x => TryParseInt32(x.GetString() ?? string.Empty, out var abilityId) ? abilityId : 0)
                .Where(x => x > 0)
                .ToArray();

            chains.Add(new CapturedCastChain(
                chainElement.GetProperty("key").GetString() ?? chainElement.GetProperty("start_frame").GetInt32().ToString(CultureInfo.InvariantCulture),
                chainElement.GetProperty("source_file").GetString() ?? string.Empty,
                chainElement.GetProperty("start_frame").GetInt32(),
                abilityIds,
                packets.ToArray()));
        }

        return chains
            .OrderBy(x => x.StartFrame)
            .ToList();
    }

    private static Dictionary<int, string> LoadCapturedLaunchAndLandMap()
    {
        var map = new Dictionary<int, string>();
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "Abilities", "ability_launch_map.csv");

        if (!File.Exists(path))
            return map;

        foreach (var line in File.ReadLines(path).Skip(1))
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
                continue;

            if (!TryParseInt32(parts[0], out var abilityDefinitionId))
                continue;

            var sha1 = parts[1];
            if (sha1.Length < 12)
                continue;

            map[abilityDefinitionId] = sha1[..12];
        }

        return map;
    }

    private static string GetShortSha1(byte[] payload)
    {
        return Convert.ToHexString(SHA1.HashData(payload))[..12].ToLowerInvariant();
    }

    private static bool ContainsInt32(byte[] payload, int value)
    {
        var bytes = BitConverter.GetBytes(value);

        for (var i = 0; i <= payload.Length - bytes.Length; i++)
        {
            if (payload[i] == bytes[0] &&
                payload[i + 1] == bytes[1] &&
                payload[i + 2] == bytes[2] &&
                payload[i + 3] == bytes[3])
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryApplyMutation(byte[] payload, AbilityDefinitionMutation mutation, out string description, out string error)
    {
        description = string.Empty;
        error = string.Empty;

        var type = mutation.Type.Trim().ToLowerInvariant();

        switch (type)
        {
            case "i32":
            case "int":
                if (!TryParseInt32(mutation.Value, out var int32Value))
                {
                    error = $"Invalid i32 value: {mutation.Value}";
                    return false;
                }

                if (!TryWrite(payload, mutation.Offset, BitConverter.GetBytes(int32Value), out error))
                    return false;

                description = $"@{mutation.Offset}:i32={int32Value}/0x{int32Value:X}";
                return true;

            case "u32":
            case "uint":
                if (!TryParseUInt32(mutation.Value, out var uint32Value))
                {
                    error = $"Invalid u32 value: {mutation.Value}";
                    return false;
                }

                if (!TryWrite(payload, mutation.Offset, BitConverter.GetBytes(uint32Value), out error))
                    return false;

                description = $"@{mutation.Offset}:u32={uint32Value}/0x{uint32Value:X}";
                return true;

            case "i16":
            case "short":
                if (!TryParseInt16(mutation.Value, out var int16Value))
                {
                    error = $"Invalid i16 value: {mutation.Value}";
                    return false;
                }

                if (!TryWrite(payload, mutation.Offset, BitConverter.GetBytes(int16Value), out error))
                    return false;

                description = $"@{mutation.Offset}:i16={int16Value}/0x{int16Value:X}";
                return true;

            case "u16":
            case "ushort":
                if (!TryParseUInt16(mutation.Value, out var uint16Value))
                {
                    error = $"Invalid u16 value: {mutation.Value}";
                    return false;
                }

                if (!TryWrite(payload, mutation.Offset, BitConverter.GetBytes(uint16Value), out error))
                    return false;

                description = $"@{mutation.Offset}:u16={uint16Value}/0x{uint16Value:X}";
                return true;

            case "byte":
            case "u8":
                if (!TryParseByte(mutation.Value, out var byteValue))
                {
                    error = $"Invalid byte value: {mutation.Value}";
                    return false;
                }

                if (!TryWrite(payload, mutation.Offset, [byteValue], out error))
                    return false;

                description = $"@{mutation.Offset}:byte={byteValue}/0x{byteValue:X2}";
                return true;

            case "float":
            case "f32":
                if (!float.TryParse(mutation.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue) &&
                    !float.TryParse(mutation.Value, NumberStyles.Float, CultureInfo.CurrentCulture, out floatValue))
                {
                    error = $"Invalid float value: {mutation.Value}";
                    return false;
                }

                if (!TryWrite(payload, mutation.Offset, BitConverter.GetBytes(floatValue), out error))
                    return false;

                description = $"@{mutation.Offset}:float={floatValue.ToString(CultureInfo.InvariantCulture)}";
                return true;

            case "guid":
            case "u64":
                if (!TryParseUInt64(mutation.Value, out var uint64Value))
                {
                    error = $"Invalid guid/u64 value: {mutation.Value}";
                    return false;
                }

                if (!TryWrite(payload, mutation.Offset, BitConverter.GetBytes(uint64Value), out error))
                    return false;

                description = $"@{mutation.Offset}:guid={uint64Value}/0x{uint64Value:X}";
                return true;

            default:
                error = $"Unsupported mutation type: {mutation.Type}";
                return false;
        }
    }

    private static bool TryWrite(byte[] payload, int offset, byte[] value, out string error)
    {
        error = string.Empty;

        if (offset < 0 || offset > payload.Length - value.Length)
        {
            error = $"Mutation offset {offset} with length {value.Length} is outside payload length {payload.Length}.";
            return false;
        }

        Array.Copy(value, 0, payload, offset, value.Length);
        return true;
    }

    private static bool TryParseInt32(string value, out int result)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseUInt32(string value, out uint result)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);

        return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseInt16(string value, out short result)
    {
        result = 0;

        if (!TryParseInt32(value, out var parsed) || parsed is < short.MinValue or > short.MaxValue)
            return false;

        result = (short)parsed;
        return true;
    }

    private static bool TryParseUInt16(string value, out ushort result)
    {
        result = 0;

        if (!TryParseUInt32(value, out var parsed) || parsed > ushort.MaxValue)
            return false;

        result = (ushort)parsed;
        return true;
    }

    private static bool TryParseByte(string value, out byte result)
    {
        result = 0;

        if (!TryParseUInt32(value, out var parsed) || parsed > byte.MaxValue)
            return false;

        result = (byte)parsed;
        return true;
    }

    private static bool TryParseUInt64(string value, out ulong result)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ulong.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);

        return ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static void ReplaceInt32(byte[] payload, int oldValue, int newValue)
    {
        var oldBytes = BitConverter.GetBytes(oldValue);
        var newBytes = BitConverter.GetBytes(newValue);

        for (var i = 0; i <= payload.Length - oldBytes.Length; i++)
        {
            if (payload[i] != oldBytes[0] ||
                payload[i + 1] != oldBytes[1] ||
                payload[i + 2] != oldBytes[2] ||
                payload[i + 3] != oldBytes[3])
            {
                continue;
            }

            Array.Copy(newBytes, 0, payload, i, newBytes.Length);
        }
    }

    private static void WriteInt32(byte[] payload, int offset, int value)
    {
        var bytes = BitConverter.GetBytes(value);
        Array.Copy(bytes, 0, payload, offset, bytes.Length);
    }

    private static void WriteUInt64(byte[] payload, int offset, ulong value)
    {
        var bytes = BitConverter.GetBytes(value);
        Array.Copy(bytes, 0, payload, offset, bytes.Length);
    }

    private static void SendAbilitySetDefinition(GatewayConnection connection, int profileId, ILogger logger, string reason)
    {
        logger.LogInformation("Sending captured ability set definition. ( ProfileId: {profileId}, Reason: {reason} )", profileId, reason);
        connection.SendTunneled(new RawPacket(AbilitySetDefinitions[profileId]));
    }

    private static void SendEmptyAbilitySetDefinition(GatewayConnection connection, int profileId, ILogger logger)
    {
        logger.LogInformation("Clearing ability set definition. ( ProfileId: {profileId} )", profileId);

        using var writer = new PacketWriter();

        writer.Write((short)36);
        writer.Write((short)5);
        writer.Write(profileId);
        writer.Write(8);
        writer.Write(0);

        for (var i = 0; i < 8; i++)
            writer.Write(0);

        connection.SendTunneled(new RawPacket(writer.Buffer));
    }
}
