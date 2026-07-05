using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game;
using Sanctuary.Game.Combat;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;

namespace Sanctuary.Gateway.Combat;

public static class CombatBootstrap
{
    public const int BrawlerProfileId = 43;
    private const float AutoTargetRange = 35f;
    private const float AutoTargetRangeSquared = AutoTargetRange * AutoTargetRange;
    private const int WeaponSlot = 7;
    private static ulong _combatEventGuid = 10_000_000_000;

    private static readonly Dictionary<ulong, Dictionary<int, int>> PlayerAbilityOverrides = new();
    private static readonly Dictionary<ulong, AnimationProbeState> PlayerAnimationProbes = new();

    public static int? DebugAnimationOverride { get; set; }

    private sealed record AnimationProbeState(string Label, int[] Candidates, int Index)
    {
        public int Current => Candidates[Index];
    }

    private static readonly Dictionary<string, (string Label, int[] Candidates)> AnimationCandidateSets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ninja"] = ("Ninja", BuildCandidateList((1031, 1039), (1011041, 1011055))),
        };

    public static int? ConsumeDebugAnimationOverride(GatewayConnection connection)
    {
        if (PlayerAnimationProbes.TryGetValue(connection.Player.Guid, out var state))
            return state.Current;

        return ConsumeDebugAnimationOverride();
    }

    public static int? ConsumeDebugAnimationOverride()
    {
        var animationId = DebugAnimationOverride;
        DebugAnimationOverride = null;
        return animationId;
    }

    public static string ArmAnimationProbe(GatewayConnection connection, int animationId, string label)
    {
        PlayerAnimationProbes[connection.Player.Guid] = new AnimationProbeState(label, [animationId], 0);
        DebugAnimationOverride = null;
        return $"Animation probe armed: {animationId} ({label}). Press ability slot 1 or 2.";
    }

    public static string ClearAnimationProbe(GatewayConnection connection)
    {
        PlayerAnimationProbes.Remove(connection.Player.Guid);
        DebugAnimationOverride = null;
        return "Animation override cleared. Ability keys now use their normal animation.";
    }

    public static bool TryArmAnimationCandidateSet(GatewayConnection connection, string key, out string result)
    {
        if (!AnimationCandidateSets.TryGetValue(key, out var set))
        {
            result = $"No animation candidate set named '{key}'. Known sets: {DescribeAnimationCandidateSets()}";
            return false;
        }

        PlayerAnimationProbes[connection.Player.Guid] = new AnimationProbeState(set.Label, set.Candidates, 0);
        DebugAnimationOverride = null;
        result = DescribeCurrentAnimationProbe(connection);
        return true;
    }

    public static bool TryArmAnimationRange(GatewayConnection connection, int start, int end, int step, out string result)
    {
        if (step == 0)
        {
            result = "Animation range step cannot be 0.";
            return false;
        }

        if (start < end && step < 0 || start > end && step > 0)
            step *= -1;

        var candidates = new List<int>();
        for (var id = start; step > 0 ? id <= end : id >= end; id += step)
        {
            candidates.Add(id);
            if (candidates.Count > 500)
            {
                result = "Animation range is too large. Keep probes to 500 ids or fewer.";
                return false;
            }
        }

        if (candidates.Count == 0)
        {
            result = "Animation range produced no candidates.";
            return false;
        }

        PlayerAnimationProbes[connection.Player.Guid] = new AnimationProbeState($"{start}..{end}", candidates.ToArray(), 0);
        DebugAnimationOverride = null;
        result = DescribeCurrentAnimationProbe(connection);
        return true;
    }

    public static bool TryMoveAnimationProbe(GatewayConnection connection, int delta, out string result)
    {
        if (!PlayerAnimationProbes.TryGetValue(connection.Player.Guid, out var state))
        {
            result = "No animation candidate list is armed. Use !animjob ninja or !animrange <start> <end> first.";
            return false;
        }

        var nextIndex = (state.Index + delta) % state.Candidates.Length;
        if (nextIndex < 0)
            nextIndex += state.Candidates.Length;

        PlayerAnimationProbes[connection.Player.Guid] = state with { Index = nextIndex };
        result = DescribeCurrentAnimationProbe(connection);
        return true;
    }

    public static string DescribeCurrentAnimationProbe(GatewayConnection connection)
    {
        if (!PlayerAnimationProbes.TryGetValue(connection.Player.Guid, out var state))
            return "No animation probe is armed.";

        return $"Animation probe {state.Label}: {state.Current} ({state.Index + 1}/{state.Candidates.Length}). Press ability slot 1 or 2, then use !animnext or !animprev.";
    }

    public static string DescribeAnimationCandidateSets() =>
        string.Join(", ", AnimationCandidateSets.Select(x => $"{x.Value.Label.ToLowerInvariant()}={x.Value.Candidates.Length} ids"));

    private static int[] BuildCandidateList(params (int Start, int End)[] ranges)
    {
        var ids = new List<int>();
        foreach (var (start, end) in ranges)
        {
            var step = start <= end ? 1 : -1;
            for (var id = start; step > 0 ? id <= end : id >= end; id += step)
                ids.Add(id);
        }

        return ids.ToArray();
    }

    public static void ScheduleAnimationProbeRecovery(GatewayConnection connection, ILogger logger, int animationId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1200);

                connection.SendTunneled(new AbilityPacketStartCasting
                {
                    Unknown = connection.Player.Guid,
                    Unknown2 = connection.Player.Guid,
                    CompositeEffectId = 0,
                    Animation = -1,
                    AbilityId = 0,
                    ActionTime = 0,
                    HasActionProgress = false,
                });

                connection.SendTunneled(new AbilityPacketFailed { StringId = 0 });
                connection.SendTunneled(new EncounterPacketIsFighting { IsFighting = false });

                logger.LogInformation("Sent animation probe recovery. ( AnimationId: {animationId} )", animationId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Animation probe recovery failed. ( AnimationId: {animationId} )", animationId);
            }
        });
    }

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

    public static string DescribeTemporaryAbilityOverrides(GatewayConnection connection)
    {
        if (!PlayerAbilityOverrides.TryGetValue(connection.Player.Guid, out var overrides) || overrides.Count == 0)
            return "No temporary ability overrides are set.";

        return string.Join("\n", overrides
            .OrderBy(x => x.Key)
            .Select(x => $"slot {x.Key + 1}: {x.Value} / 0x{x.Value:X}"));
    }

    public static string DescribeSupportedProfiles() =>
        "Combat profiles: Ninja=2, Medic=11, Wizard=12, Warrior=32, Archer=35, Brawler=43.";

    private static bool TrySendCustomAbilitySetDefinition(
        GatewayConnection connection,
        IResourceManager? resourceManager,
        int profileId,
        ILogger logger,
        string reason)
    {
        if (resourceManager is not null &&
            profileId == NinjaWeaponAbilities.NinjaProfileId &&
            NinjaWeaponAbilities.GetEquippedWeapon(connection.Player) is not null)
        {
            logger.LogInformation("Sending ninja weapon ability toolbar. ( ProfileId: {profileId}, Reason: {reason} )", profileId, reason);
            connection.SendTunneled(NinjaWeaponAbilities.BuildToolbar(connection.Player, resourceManager));
            return true;
        }

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
        if (!AbilityDefinitions.TryGetValue(abilityDefinitionId, out var payload))
            return false;

        logger.LogInformation("Sending captured Brawler ability definition. ( AbilityDefinitionId: {abilityDefinitionId} )", abilityDefinitionId);
        connection.SendTunneled(new RawPacket(payload));
        return true;
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

        var casterGuid = connection.Player.Guid;
        var resolvedTargetGuid = ResolveAbilityTarget(connection, targetGuid);

        using var writer = new PacketWriter();

        writer.Write((short)36);
        writer.Write((short)3);
        writer.Write(casterGuid);
        writer.Write(resolvedTargetGuid);
        writer.Write(0);
        var animationOverride = ConsumeDebugAnimationOverride(connection);

        writer.Write(animationOverride ?? 0);
        writer.Write(abilityDefinitionId);
        writer.Write(0);
        writer.Write((byte)0);

        logger.LogInformation(
            "Sending ability start-casting. ( ProfileId: {profileId}, Slot: {slot}, AbilityDefinitionId: {abilityDefinitionId}, CasterGuid: {casterGuid}, TargetGuid: {targetGuid} )",
            connection.Player.ActiveProfileId,
            slot,
            abilityDefinitionId,
            casterGuid,
            resolvedTargetGuid);

        connection.SendTunneled(new RawPacket(writer.Buffer));

        if (animationOverride is not null)
            ScheduleAnimationProbeRecovery(connection, logger, animationOverride.Value);

        TrySendLaunchAndLand(connection, abilityDefinitionId, casterGuid, resolvedTargetGuid, logger);
        TryApplyAbilityDamage(connection, abilityDefinitionId, resolvedTargetGuid, logger);
        return true;
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

        return false;
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
        if (!LaunchAndLandTemplates.TryGetValue(abilityDefinitionId, out var template))
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
