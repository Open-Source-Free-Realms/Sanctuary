using System.Globalization;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class TimeScaleChatCommand : IChatCommand
{
    private const float MaxScalar = 1000f;

    private readonly IZoneManager _zoneManager;
    private readonly IServerSettings _serverSettings;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "timescale";
    public string Usage => "[scalar]";
    public string Description => "Shows or sets the client game-time scalar (day/night speed) for everyone. 1 is normal.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public TimeScaleChatCommand(IZoneManager zoneManager, IServerSettings serverSettings, IChatCommandManager chatCommandManager)
    {
        _zoneManager = zoneManager;
        _serverSettings = serverSettings;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length == 0)
        {
            ChatHelper.SendSystemMessage(invoker, $"Game time scalar is {_serverSettings.GameTimeScalar}.");
            return true;
        }

        if (args.Length != 1
            || !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var scalar)
            || scalar <= 0f || scalar > MaxScalar)
        {
            ChatHelper.SendSystemMessage(invoker, $"Scalar must be a number above 0 and at most {MaxScalar}.");
            return true;
        }

        _serverSettings.GameTimeScalar = scalar;

        var packetClientGameSettings = _serverSettings.CreateClientGameSettings();

        foreach (var player in _zoneManager.Zones.SelectMany(zone => zone.Players))
            player.SendTunneled(packetClientGameSettings);

        _chatCommandManager.LogAction(this, invoker, "Set", null, $"Scalar: {scalar}");
        ChatHelper.SendSystemMessage(invoker, $"Game time scalar set to {scalar}.");

        return true;
    }
}
