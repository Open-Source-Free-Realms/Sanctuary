using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.ChatCommands;

public class MountCommand : IChatCommand
{
    private readonly IResourceManager _resourceManager;

    public string KeyWord => "mount";
    public string Usage => "<model_id> [tint_id] [flying]";
    public string Description => "Mounts the specified model. 'tint_id' (1-558) and 'flying' (true/false) are optional.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public MountCommand(IResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        bool isFlying = false;
        int tintId = 0;

        if (args.Length < 1 || args.Length > 3)
        {
            ChatHelper.SendSystemMessage(invoker, $"Invalid usage. Use: !{KeyWord} {Usage}");
            return false;
        }

        if (!int.TryParse(args[0], out int modelId))
        {
            ChatHelper.SendSystemMessage(invoker, "Invalid model ID. Please enter a valid number.");
            return false;
        }

        if (args.Length >= 2 && !int.TryParse(args[1], out tintId))
        {
            ChatHelper.SendSystemMessage(invoker, "Invalid tint ID. Please enter a valid number (1-558).");
            return false;
        }

        if (args.Length == 3 && !bool.TryParse(args[2], out isFlying))
        {
            ChatHelper.SendSystemMessage(invoker, "Invalid flying argument. Use 'true' or 'false'.");
            return false;
        }

        if (invoker.Mount is not null)
            invoker.Dismount();

        var officialMountDef = _resourceManager.Mounts.Values.FirstOrDefault(m => m.ModelId == modelId);
        var mountDefToUse = officialMountDef ?? new Resources.Definitions.MountDefinition();

        if (!invoker.Zone.TryCreateMount(invoker, mountDefToUse, out var mount))
        {
            ChatHelper.SendSystemMessage(invoker, "Failed to spawn mount.");
            return false;
        }

        mount.Visible = true;
        mount.NameId = mountDefToUse.NameId > 0 ? mountDefToUse.NameId : 1;
        mount.ModelId = modelId;
        mount.TextureAlias = mountDefToUse.TextureAlias ?? default;
        mount.TintAlias = mountDefToUse.TintAlias ?? default;
        mount.TintId = tintId;
        mount.HideNamePlate = true;
        mount.ImageSetId = mountDefToUse.ImageSetId;
        mount.Seat = 0;
        mount.QueuePosition = 1;

        invoker.Mount = mount;
        invoker.UpdatePosition(invoker.Position, invoker.Rotation);

        foreach (var visiblePlayer in invoker.VisiblePlayers.Values)
        {
            visiblePlayer.OnAddVisiblePlayers([invoker]);
        }
        
        invoker.SendTunneled(mount.GetAddNpcPacket());

        var mountResponse = mount.GetMountResponsePacket();
        mountResponse.CompositeEffectId = 46; // PFX_Teleport_Flash
        invoker.SendTunneledToVisible(mountResponse, sendToSelf: true);

        var mountStats = officialMountDef?.Stats ?? new Resources.Definitions.MountDefinition.MountStats();

        if (officialMountDef is null) 
        {
            mountStats.MaxMovementSpeed = 12.5f;
        }

        if (modelId == 4591)
        {
            mountStats.MaxMovementSpeed = 12.5f;
            mountStats.GlideDefaultForwardSpeed = 14f;
            mountStats.GlideMinForwardSpeed = 8f;
            mountStats.GlideMaxForwardSpeed = 24f;
            mountStats.GlideFallTime = 0.75f;
            mountStats.GlideFallSpeed = 2f;
            mountStats.GlideEnabled = 1;
            mountStats.GlideAccel = 4f;
            mountStats.JumpHeight = 5f;
        }
        else if (isFlying)
        {
            mountStats.MaxMovementSpeed = 12.5f;
            mountStats.GlideDefaultForwardSpeed = 8f;
            mountStats.GlideMinForwardSpeed = 2f;
            mountStats.GlideMaxForwardSpeed = 18f;
            mountStats.GlideFallTime = 0.75f;
            mountStats.GlideFallSpeed = 4f;
            mountStats.GlideEnabled = 1;
            mountStats.GlideAccel = 4f;
        }

        invoker.UpdateCharacterStats(
            CharacterStats.MaxMovementSpeed.Set(mountStats.MaxMovementSpeed),
            CharacterStats.GlideDefaultForwardSpeed.Set(mountStats.GlideDefaultForwardSpeed),
            CharacterStats.GlideMinForwardSpeed.Set(mountStats.GlideMinForwardSpeed),
            CharacterStats.GlideMaxForwardSpeed.Set(mountStats.GlideMaxForwardSpeed),
            CharacterStats.GlideFallTime.Set(mountStats.GlideFallTime),
            CharacterStats.GlideFallSpeed.Set(mountStats.GlideFallSpeed),
            CharacterStats.GlideEnabled.Set(mountStats.GlideEnabled),
            CharacterStats.GlideAccel.Set(mountStats.GlideAccel),
            CharacterStats.JumpHeight.Set(mountStats.JumpHeight)
        );

        string statusText = officialMountDef is not null ? "official" : "custom";
        string tintText = tintId > 0 ? $" with Tint: {tintId}" : "";
        ChatHelper.SendSystemMessage(invoker, $"Spawned {statusText} mount (Model ID: {modelId}){tintText}");

        return true;
    }
}