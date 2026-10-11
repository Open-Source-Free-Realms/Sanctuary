using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;

using System.Numerics;

namespace Sanctuary.Game.ChatCommands;

public class TeleportCommand : IChatCommand
{
    public string KeyWord => "teleport";
    public string Usage => "<x> <y> <z>";
    public string Description => "Teleports you to the specified coordinates.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length != 3)
        {
            ChatHelper.SendSystemMessage(invoker, "Invalid usage. Use: !teleport <x> <y> <z>");
            return false;
        }

        if (!float.TryParse(args[0], out float x) || !float.TryParse(args[1], out float y) || !float.TryParse(args[2], out float z))
        {
            ChatHelper.SendSystemMessage(invoker, "Invalid coordinates. Please enter valid numbers.");
            return false;
        }

        var newPosition = new Vector4(x, y, z, invoker.Position.W);

        var currentRotation = invoker.Rotation;

        invoker.UpdatePosition(newPosition, currentRotation, false);

        var teleportPacket = new ClientUpdatePacketUpdateLocation
        {
            Position = newPosition,
            Rotation = currentRotation,
            Teleport = true
        };
        invoker.SendTunneled(teleportPacket);
        ChatHelper.SendSystemMessage(invoker, $"Teleported to ({x:F2}, {y:F2}, {z:F2})");
        return true;
    }
}