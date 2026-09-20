using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

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

        var position = invoker.Position;

        invoker.UpdatePosition(position, System.Numerics.Quaternion.Identity, false);

        var clientUpdatePacketUpdateLocation = new ClientUpdatePacketUpdateLocation
        {
            Position = new System.Numerics.Vector4(x, y, z, position.W),
            Rotation = System.Numerics.Quaternion.Identity,
            Teleport = true
        };
        invoker.SendTunneled(clientUpdatePacketUpdateLocation);
        ChatHelper.SendSystemMessage(invoker, $"Teleported to ({x:F2}, {y:F2}, {z:F2})");
        return true;
    }
}