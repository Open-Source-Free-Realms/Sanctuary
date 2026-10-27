using System;
using System.Globalization;
using System.Numerics;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions.Zones;
using Sanctuary.Packet;

namespace Sanctuary.Game.ChatCommands;

public class MovePlayerChatCommand : IChatCommand
{
    private readonly IZoneManager _zoneManager;
    private readonly IResourceManager _resourceManager;
    private readonly IChatCommandManager _chatCommandManager;

    public string KeyWord => "moveplayer";
    public string Usage => "<x> <y> <z> <player> | zone <zoneId> <player>";
    public string Description => "Moves another player to coordinates in their zone, or to a world zone's spawn point.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public MovePlayerChatCommand(IZoneManager zoneManager, IResourceManager resourceManager, IChatCommandManager chatCommandManager)
    {
        _zoneManager = zoneManager;
        _resourceManager = resourceManager;
        _chatCommandManager = chatCommandManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length >= 3 && args[0].Equals("zone", StringComparison.OrdinalIgnoreCase))
            return HandleZone(invoker, args[1..]);

        if (args.Length < 4
            || !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || !float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            return false;

        if (!TryGetTarget(invoker, args[3..], out var target))
            return true;

        var position = new Vector4(x, y, z, target.Position.W);

        target.UpdatePosition(position, target.Rotation, false);

        target.SendTunneled(new ClientUpdatePacketUpdateLocation
        {
            Position = position,
            Rotation = target.Rotation,
            Teleport = true
        });

        _chatCommandManager.LogAction(this, invoker, "Move", target.Name.FullName, $"Position: {x:F2}, {y:F2}, {z:F2}");
        ChatHelper.SendSystemMessage(invoker, $"Moved {target.Name.FullName} to ({x:F2}, {y:F2}, {z:F2}).");
        ChatHelper.SendSystemMessage(target, $"You were moved by {invoker.Name.FullName}.");

        return true;
    }

    private bool HandleZone(Player invoker, string[] args)
    {
        if (!int.TryParse(args[0], out var zoneId))
            return false;

        if (!_resourceManager.Zones.TryGetValue(zoneId, out var zoneDefinition) || zoneDefinition is not WorldZoneDefinition)
        {
            ChatHelper.SendSystemMessage(invoker, $"{zoneId} is not a world zone. Houses and combat zones need an owner or encounter.");
            return true;
        }

        if (!TryGetTarget(invoker, args[1..], out var target))
            return true;

        if (!target.TeleportToZone(zoneId, null, zoneDefinition.SpawnPosition, zoneDefinition.SpawnRotation))
        {
            ChatHelper.SendSystemMessage(invoker, $"Failed to move {target.Name.FullName} to {zoneDefinition.Name}.");
            return true;
        }

        _chatCommandManager.LogAction(this, invoker, "MoveZone", target.Name.FullName, $"Zone: {zoneId}");
        ChatHelper.SendSystemMessage(invoker, $"Moved {target.Name.FullName} to {zoneDefinition.Name}.");
        ChatHelper.SendSystemMessage(target, $"You were moved by {invoker.Name.FullName}.");

        return true;
    }

    private bool TryGetTarget(Player invoker, string[] nameArgs, out Player target)
    {
        var targetName = string.Join(' ', nameArgs);

        if (_zoneManager.TryGetPlayer(targetName, out target!))
            return true;

        ChatHelper.SendSystemMessage(invoker, $"No online player named \"{targetName}\" was found.");
        return false;
    }
}
