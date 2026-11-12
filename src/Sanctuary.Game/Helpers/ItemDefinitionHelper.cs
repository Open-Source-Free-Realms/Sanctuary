using System.Collections.Generic;
using System.Linq;

using Sanctuary.Core.IO;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Helpers;

public static class ItemDefinitionHelper
{
    public const int NoItemClass = 0;
    public const int CommunityColourIdStep = 10_000_000;
    public const int PetItemType = 2;
    public const int PetClothingItemType = 3;
    public const int PetUtilityItemType = 4;
    public const int MountItemType = 19;
    public const int HousingItemType = 29;
    public const int MysteryChestItem = 28;
    public const int MysteryKeyId = 78137;
    public const int CustomizationItemType = 17;

    private const int NoPurchasesLeftMaxStackSize = 0;
    private const int NoTint = 0;

    public static ClientItemDefinition GetPlayerItemDefinition(Player player, IResourceManager resourceManager, ClientItemDefinition clientItemDefinition)
    {
        if (clientItemDefinition.Type != MountItemType
            || clientItemDefinition.MaxStackSize <= NoPurchasesLeftMaxStackSize
            || player.Mounts.Count(mount => mount.Definition == clientItemDefinition.Param1) < clientItemDefinition.MaxStackSize
            || HasTintChoice(resourceManager, clientItemDefinition))
            return clientItemDefinition;

        var playerItemDefinition = clientItemDefinition.Clone();

        playerItemDefinition.MaxStackSize = NoPurchasesLeftMaxStackSize;

        return playerItemDefinition;
    }

    public static ClientItemDefinition GetTintedItemDefinition(ClientItemDefinition clientItemDefinition, int tint)
    {
        if (tint == NoTint || clientItemDefinition.Icon.TintId == tint)
            return clientItemDefinition;

        var tintedItemDefinition = clientItemDefinition.Clone();

        tintedItemDefinition.Icon = new IconData
        {
            Id = clientItemDefinition.Icon.Id,
            TintId = tint
        };

        return tintedItemDefinition;
    }

    public static void SendItemDefinition(Player player, IResourceManager resourceManager, ClientItemDefinition clientItemDefinition)
    {
        using var writer = new PacketWriter();

        GetPlayerItemDefinition(player, resourceManager, clientItemDefinition).Serialize(writer);

        var playerUpdatePacketItemDefinitionRequest = new PlayerUpdatePacketItemDefinitionRequest
        {
            Id = clientItemDefinition.Id,
            Payload = writer.Buffer
        };

        player.SendTunneled(playerUpdatePacketItemDefinitionRequest);
    }

    public static void SendItemDefinitions(Player player, IResourceManager resourceManager, IEnumerable<ClientItemDefinition> clientItemDefinitions)
    {
        var playerItemDefinitions = clientItemDefinitions
            .Select(clientItemDefinition => GetPlayerItemDefinition(player, resourceManager, clientItemDefinition))
            .ToList();

        if (playerItemDefinitions.Count == 0)
            return;

        using var writer = new PacketWriter();

        writer.Write(playerItemDefinitions);

        var playerUpdatePacketItemDefinitions = new PlayerUpdatePacketItemDefinitions
        {
            Payload = writer.Buffer
        };

        player.SendTunneled(playerUpdatePacketItemDefinitions);
    }

    private static bool HasTintChoice(IResourceManager resourceManager, ClientItemDefinition clientItemDefinition) =>
        resourceManager.Stores.Values.Any(storeDefinition => storeDefinition.Bundles.Values.Any(appStoreBundleDefinition =>
            appStoreBundleDefinition.IsTintable && appStoreBundleDefinition.Entries.Any(entry => entry.MarketingItemId == clientItemDefinition.Id)));
}
