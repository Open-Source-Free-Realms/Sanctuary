using System;
using System.Collections.Concurrent;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions.Rewards;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;

namespace Sanctuary.Game.Minigames;

/// <summary>
/// The daily coin wheel ("Spin For The Win!", game_wheel.swf). The movie talks to the server with tab-separated
/// Flash messages (its Wheel.WheelGameClient class); the flow is connect, wheel data, a spin request answered
/// with the slot to land on, and the reward once the wheel stops.
/// </summary>
public static class DailyWheelGame
{
    public const string SwfName = "game_wheel.swf";

    private const int StageId = BaseZone.DailyWheelMinigameId;

    private const int WheelId = 1;

    /// <summary>
    /// The movie's wheel artwork: type 1 is the ten-slot wheel.
    /// </summary>
    private const int WheelType = 1;

    /// <summary>
    /// The coin icon. (The reward window turns icon 22663 into this one plus a marketplace "Shop Now!" button.)
    /// </summary>
    private const int CoinRewardIconId = 4809;

    /// <summary>
    /// String ids for the reward window: "Coins" and "Come back tomorrow for another spin!".
    /// </summary>
    private const int CoinsNameId = 4923;
    private const int ComeBackTomorrowId = 408045;

    /// <summary>
    /// The wheel's slots, clockwise from the arrow: the movie's slot category (its picture) and the coins it pays.
    /// Categories 15-21 are the movie's 2, 5, 10, 20, 30, 40 and 50 coin pictures.
    /// </summary>
    private static readonly (int Category, int Coins, int Weight)[] Slots =
    [
        (15, 2, 10),
        (17, 10, 7),
        (16, 5, 9),
        (18, 20, 5),
        (15, 2, 10),
        (19, 30, 3),
        (16, 5, 9),
        (20, 40, 2),
        (17, 10, 7),
        (21, 50, 1)
    ];

    /// <summary>
    /// The slot a player's spin landed on, paid when the client reports that the wheel stopped.
    /// </summary>
    private static readonly ConcurrentDictionary<ulong, int> PendingSlots = new();

    /// <summary>
    /// The day (UTC) each player last spun. Kept in memory, so a server restart gives everyone a spin back.
    /// </summary>
    private static readonly ConcurrentDictionary<ulong, DateOnly> LastSpinDays = new();

    /// <summary>
    /// Answers the client's start request (sent once the minigame's Info has loaded) by loading the movie.
    /// </summary>
    public static void Start(Player player)
    {
        PendingSlots.TryRemove(player.Guid, out _);

        player.SendTunneled(new CommandPacketStartFlashGame
        {
            GameSwfName = SwfName,
            ReturnToPortal = true
        });

        player.SendTunneled(new MiniGameHeaderPacket
        {
            SubOpCode = BaseMiniGamePacket.StartGame,
            StageId = StageId
        });
    }

    /// <summary>
    /// Handles a Flash message from the wheel. Returns false for messages it doesn't know.
    /// </summary>
    public static bool HandlePayload(Player player, string payload, IRewardManager rewardManager)
    {
        // The movie's messages end with padding (control characters, spaces) after the last field.
        var parts = new string(payload.Where(c => c == '\t' || !char.IsControl(c)).ToArray()).TrimEnd().Split('\t');

        switch (parts[0])
        {
            case "OnConnectMsg":
            {
                var slots = string.Join(' ', Slots.Select(x => x.Category));

                SendPayload(player, $"OnWheelDataMsg\t{WheelId}\t{WheelType}\t{slots}\t0\t0");
                SendWheelUpdate(player);
                SendPayload(player, "OnServerReadyMsg");
                return true;
            }

            case "OnChangeWheelRequestMsg":
                SendPayload(player, $"OnWheelChangedMsg\t{WheelId}");
                return true;

            case "OnWheelSpinRequestMsg":
            {
                if (SpinsLeft(player) <= 0)
                    return true;

                LastSpinDays[player.Guid] = Today;

                // The movie moves the wheel this many slots past where its own spin would stop, which lands the
                // arrow on that slot.
                var slot = RollSlot();
                PendingSlots[player.Guid] = slot;
                SendPayload(player, $"OnSpinInfoMsg\t{slot}");
                return true;
            }

            case "OnWheelStopMsg":
            {
                if (!PendingSlots.TryRemove(player.Guid, out var slot))
                    return true;

                var coins = Slots[slot].Coins;

                rewardManager.TryGrantCurrency(player, CurrencyType.Coins, coins);
                SendPayload(player, $"OnRewardInfoMsg\t{CoinRewardIconId}\t0\t{coins}\t{CoinsNameId}\t{ComeBackTomorrowId}\t0");
                SendWheelUpdate(player);
                return true;
            }

            case "FRServer_GameClose":
            case "FRServer_GameQuit":
                PendingSlots.TryRemove(player.Guid, out _);

                player.SendTunneled(new ExecuteScriptPacket { Script = "MiniGameFlash.StopAllSounds" });

                // Game over and leave return the client to the world, but leave the movie's window up; the
                // client's MiniGameFlash object closes it.
                player.SendTunneled(new MiniGameGameOverPacket
                {
                    StageId = StageId,
                    Won = true
                });

                player.SendTunneled(new MiniGameHeaderPacket
                {
                    SubOpCode = BaseMiniGamePacket.LeaveGame,
                    StageId = StageId
                });

                player.SendTunneled(new ExecuteScriptPacket { Script = "MiniGameFlash.Hide" });
                player.SendTunneled(new ExecuteScriptPacket { Script = "MiniGameFlash.Unload" });
                return true;
        }

        return false;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static int SpinsLeft(Player player)
    {
        return LastSpinDays.TryGetValue(player.Guid, out var day) && day == Today ? 0 : 1;
    }

    /// <summary>
    /// Sends the spins left, the consecutive days played and the seconds until the next spin.
    /// </summary>
    private static void SendWheelUpdate(Player player)
    {
        var secondsUntilTomorrow = (int)(DateTime.UtcNow.Date.AddDays(1) - DateTime.UtcNow).TotalSeconds;

        SendPayload(player, $"OnWheelUpdateMsg\t{WheelId}\t{SpinsLeft(player)}\t1\t{secondsUntilTomorrow}");
    }

    private static void SendPayload(Player player, string payload)
    {
        player.SendTunneled(new MiniGamePayloadPacket
        {
            StageId = StageId,
            Payload = payload
        });
    }

    private static int RollSlot()
    {
        var roll = Random.Shared.Next(Slots.Sum(x => x.Weight));

        for (var i = 0; i < Slots.Length; i++)
        {
            if (roll < Slots[i].Weight)
                return i;

            roll -= Slots[i].Weight;
        }

        return 0;
    }
}
