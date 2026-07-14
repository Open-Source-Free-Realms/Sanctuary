using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Game.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Interactions;

// "Merchant" - the interaction offered by vendor NPCs (models containing "merchant"). Selecting it
// opens the client's merchant window bound to this NPC and shows its wares. Buying/selling then
// flows through the existing coin-store buy/sell handlers (the client echoes this NPC's guid back
// as the MerchantGuid). See docs/merchant-shops.md for the full click → menu → shop flow.
//
// Type 17 + ButtonText 3227 ("Merchant") + IconId 22665 were recovered from live merchant
// captures. The window items ride in CoinStoreMerchantListPacket (165/10) as fixed 38-byte
// records; the client displays each item's PRICE from its local item definition, which the
// standalone client does NOT have for these wares (they are cooking ingredients, not part of
// the Station-Cash catalog pushed at login). So we must ALSO push the ware item definitions
// (PlayerUpdatePacketItemDefinitions, 37) before the merchant list, or every price shows 0.
public class ShopInteraction : IInteraction
{
    public int Id => Data.Id;

    public static InteractionData Data = new()
    {
        Id = IInteraction.UniqueId++,
        IconId = 22665,
        ButtonText = 3227, // "Merchant"
        Type = 17,         // merchant interaction type
    };

    private readonly IResourceManager _resourceManager;

    public ShopInteraction(IResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    // The wares for merchant set 468 (chef), exactly as captured on the wire (25 items) - the one
    // capture-verified set. Every id is a Type-1 item the buy handler accepts. Per-set ware lists
    // now live in Resources/MerchantSets.json; this array is the fallback seed for set 468.
    public static readonly int[] MerchantWares =
    {
        13231, 2906, 2904, 2902, 2900, 2899, 2898, 2897, 2896, 2895,
        2893, 2862, 2861, 2860, 2859, 883, 882, 881, 880, 879,
        57, 56, 55, 54, 53
    };

    // Resolve which ware set a merchant NPC sells. Real FreeRealms merchants were one-job-one-tier:
    // each named NPC sold a specific tier of a specific job's gear (outfit + weapon + jewelry). We
    // reproduce that from the wiki `==Sells==` inventories keyed by NPC NAME in MerchantNpcSets.json
    // (see the reverse-engineering notes). Resolution order:
    // 1. exact NPC name -> its canonical set (e.g. "McMann" = Brawler tier-16),
    // 2. "*<subtype>" -> that job's default (lowest-tier) set, for NPCs the wiki didn't name,
    // 3. 468 -> the byte-captured chef set (last-resort so a merchant always stocks).
    // Kart/Demolition had no canonical job vendor (their gear was the Marketplace), so their
    // NPCs resolve to themed fallback sets (480/481) via the "*kart"/"*demolition" keys.
    public static int SetIdForNpc(string? name, string? modelFileName)
    {
        var npcSets = MerchantStore.LoadNpcSets();

        if (!string.IsNullOrWhiteSpace(name) && npcSets.TryGetValue(name, out var byName))
            return byName;

        var subtype = SubtypeOf(modelFileName);
        if (subtype is not null && npcSets.TryGetValue("*" + subtype, out var bySubtype))
            return bySubtype;

        return 468;
    }

    // The subtype token embedded in a merchant model file name
    // (e.g. "human_m_merchant_blacksmith_african.agr" -> "blacksmith"), or null if not a merchant.
    public static string? SubtypeOf(string? modelFileName)
    {
        if (modelFileName is null)
            return null;

        var m = System.Text.RegularExpressions.Regex.Match(modelFileName, "merchant_([a-z]+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    public void OnInteract(Player player, IEntity other)
    {
        if (other is not Npc npc)
            return;

        SendMerchantWindow(player, _resourceManager, npc.Guid, npc.MerchantSetId);
    }

    // Builds and sends the merchant window for a vendor: pushes the ware item definitions, then the
    // CoinStoreMerchantListPacket that opens the window bound to this NPC. Called by OnInteract.
    public static void SendMerchantWindow(Player player, IResourceManager resourceManager, ulong merchantGuid, int merchantSetId)
    {
        var packet = new CoinStoreMerchantListPacket
        {
            MerchantSetId = merchantSetId,
            PlayerGuid = player.Guid,
            MerchantGuid = merchantGuid,
        };

        // Item definitions the client needs to render name/icon/PRICE for each ware. The client
        // has no definition for these ids (not in the login catalog), so without this push every
        // price renders as 0 and the member-discount math divides by zero ("NaN%").
        var definitions = new List<ClientItemDefinition>();

        // This merchant SET's wares (per subtype - blacksmith/miner/chef/…), from
        // Resources/MerchantSets.json; costs from the editable Resources/MerchantItems.json.
        foreach (var wareId in MerchantStore.WaresForSet(resourceManager, merchantSetId))
        {
            if (!resourceManager.ClientItemDefinitions.TryGetValue(wareId, out var def))
                continue;

            // The client shows a merchant item's price from its DEFINITION (not the 165/10
            // record), and the buy handler charges def.Cost too - so the configured cost must be
            // written onto the definition we push. These ware defs are merchant-only (not in the
            // Station-Cash catalog), so setting the cost here does not affect anything else.
            def.Cost = MerchantStore.CostFor(resourceManager, wareId);

            // Uniform pricing: merchant items cost the same for everyone. Zeroing the member
            // discount means members pay the same as a normal user (GetMemberPurchasePrice
            // returns full Cost when MemberDiscount is 0), and no "-x% member" line is shown.
            def.MemberDiscount = 0;

            definitions.Add(def);

            packet.Items.Add(new CoinStoreMerchantItem
            {
                Id = def.Id,
                IconId = def.Icon.Id,
                IconTintId = def.Icon.TintId,
                NameId = def.NameId,
                DescriptionId = def.DescriptionId,
                // Cost stays at its -1 default - the captured wire value for every ware. The
                // displayed + charged price come from the pushed ClientItemDefinition (def.Cost)
                // above, not this field. (Buy-ability is gated by coin-store CATALOG membership,
                // registered via the 165/9 dynamic-list + login 165/1 DynamicItems - not by this
                // record.
            });
        }

        // Push the definitions FIRST (tunneled packets are ordered), then open the window.
        using var writer = new PacketWriter();
        writer.Write(definitions);

        player.SendTunneled(new PlayerUpdatePacketItemDefinitions { Payload = writer.Buffer });
        player.SendTunneled(packet);
    }
}
