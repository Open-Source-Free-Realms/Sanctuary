using System;
using System.Collections.Generic;
using System.Linq;

using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.GameCommerce;

namespace Sanctuary.Game.Helpers;

public static class WelcomeScreenHelper
{
    public const int PopularItemsGroupId = 6;
    private const int StationCashCurrencyType = 1;
    private const int PopularItemCount = 8;
    private const int MarketplaceAnnouncementId = 1;
    private const int MarketplaceAnnouncementPriority = 1;
    private const int MarketplaceAnnouncementIconId = 38443;
    private const int MarketplaceAnnouncementTitleStringId = 414;
    private const int MarketplaceAnnouncementBodyStringId = 415;
    private const int MarketplaceAnnouncementButtonStringId = 436323;
    private const int MarketplaceAnnouncementParam2 = 31415;
    private const int WelcomeContent1NameId = 6185;
    private const int WelcomeContent1DescriptionId = 6186;
    private const int WelcomeContent2NameId = 6187;
    private const int WelcomeContent2DescriptionId = 6188;
    private const int WelcomeContent3NameId = 6189;
    private const int WelcomeContent3DescriptionId = 6190;
    private const int MmmDonutClaimCodeNameId = 401519;
    private const int MmmDonutClaimCodeDescriptionId = 401534;
    private const int MmmDonutClaimCodeIconId = 929;
    private const int BerryCupcakeClaimCodeNameId = 401517;
    private const int BerryCupcakeClaimCodeDescriptionId = 401532;
    private const int BerryCupcakeClaimCodeIconId = 939;
    private const int SkeletalClaimCodeNameId = 409157;
    private const int SkeletalClaimCodeDescriptionId = 109132;
    private const int SkeletalClaimCodeIconId = 3459;
    private const int StrawberriesClaimCodeNameId = 409158;
    private const int StrawberriesClaimCodeDescriptionId = 108948;
    private const int StrawberriesClaimCodeIconId = 3441;
    private const int FroggyClaimCodeNameId = 409159;
    private const int FroggyClaimCodeDescriptionId = 3141;
    private const int FroggyClaimCodeIconId = 1258;
    private const int SandwichClaimCodeNameId = 409160;
    private const int SandwichClaimCodeDescriptionId = 2430;
    private const int SandwichClaimCodeIconId = 949;
    private const int FirstLoginSecondsSinceLastLogin = -1;

    public static StoreBundleGroupDefinition GetPopularItems(
        StoreBundleGroupDefinition template, IEnumerable<AppStoreBundleDefinition> catalog,
        int count = PopularItemCount, int currencyType = StationCashCurrencyType)
    {
        var candidates = catalog.Where(x => x.CurrencyType == currencyType && x.NameId > 0
            && x.DescriptionId > 0 && int.TryParse(x.Image.Image, out var imageId) && imageId > 0)
            .DistinctBy(x => x.Id).ToArray();
        Random.Shared.Shuffle(candidates);

        return new StoreBundleGroupDefinition
        {
            Id = template.Id,
            NameId = template.NameId,
            DescriptionId = template.DescriptionId,
            Status = template.Status,
            Image = template.Image,
            IsExclusive = template.IsExclusive,
            Entries = candidates.Take(Math.Max(0, count)).Select((bundle, index) =>
                new StoreBundleGroupDefinition.Entry
                {
                    StoreBundleId = bundle.Id,
                    DisplayOrder = index + 1
                }).ToDictionary(x => x.StoreBundleId)
        };
    }

    public static AnnouncementDataSendPacket GetAnnouncementsPacket(IEnumerable<AnnouncementInfo>? announcements = null) => new()
    {
        Announcements = announcements?.ToList() ??
        [
            new()
            {
                Id = MarketplaceAnnouncementId,
                Priority = MarketplaceAnnouncementPriority,
                IconId = MarketplaceAnnouncementIconId,
                TitleStringId = MarketplaceAnnouncementTitleStringId,
                BodyStringId = MarketplaceAnnouncementBodyStringId,
                ButtonStringId = MarketplaceAnnouncementButtonStringId,
                LuaCall = "Marketplace",
                Param2 = MarketplaceAnnouncementParam2
            }
        ]
    };

    public static int SecondsSinceLastLogin(DateTimeOffset? previousLogin, DateTimeOffset loginTime) =>
        previousLogin.HasValue
            ? (int)Math.Clamp((loginTime - previousLogin.Value).TotalSeconds, 0, int.MaxValue)
            : FirstLoginSecondsSinceLastLogin;

    public static PacketLoadWelcomeScreen GetWelcomeScreenPacket(int secondsSinceLastLogin, int stationCash,
        IEnumerable<ContentInfo>? contents = null, IEnumerable<ClaimCodeInfo>? claimCodes = null)
    {
        var packetLoadWelcomeScreen = new PacketLoadWelcomeScreen
        {
            SecondsSinceLastLogin = secondsSinceLastLogin,
            StartingScWalletBalance = stationCash
        };

        packetLoadWelcomeScreen.Contents.AddRange(contents ??
        [
            new ContentInfo
            {
                NameId = WelcomeContent1NameId,
                DescriptionId = WelcomeContent1DescriptionId,
            },
            new ContentInfo
            {
                NameId = WelcomeContent2NameId,
                DescriptionId = WelcomeContent2DescriptionId,
            },
            new ContentInfo
            {
                NameId = WelcomeContent3NameId,
                DescriptionId = WelcomeContent3DescriptionId,
            }
        ]);

        packetLoadWelcomeScreen.ClaimCodes.AddRange(claimCodes ??
        [
            new ClaimCodeInfo
            {
                Code = "MMMDONUT",
                NameId = MmmDonutClaimCodeNameId,
                DescriptionId = MmmDonutClaimCodeDescriptionId,
                IconId = MmmDonutClaimCodeIconId
            },
            new ClaimCodeInfo
            {
                Code = "BERRYCUPCAKE",
                NameId = BerryCupcakeClaimCodeNameId,
                DescriptionId = BerryCupcakeClaimCodeDescriptionId,
                IconId = BerryCupcakeClaimCodeIconId
            },
            new ClaimCodeInfo
            {
                Code = "SKELETAL",
                NameId = SkeletalClaimCodeNameId,
                DescriptionId = SkeletalClaimCodeDescriptionId,
                IconId = SkeletalClaimCodeIconId,
                TintAlias = "navy"
            },
            new ClaimCodeInfo
            {
                Code = "STRAWBERRIES",
                NameId = StrawberriesClaimCodeNameId,
                DescriptionId = StrawberriesClaimCodeDescriptionId,
                IconId = StrawberriesClaimCodeIconId,
                TintAlias = "concrete"
            },
            new ClaimCodeInfo
            {
                Code = "FROGGY",
                NameId = FroggyClaimCodeNameId,
                DescriptionId = FroggyClaimCodeDescriptionId,
                IconId = FroggyClaimCodeIconId
            },
            new ClaimCodeInfo
            {
                Code = "SANDWICH",
                NameId = SandwichClaimCodeNameId,
                DescriptionId = SandwichClaimCodeDescriptionId,
                IconId = SandwichClaimCodeIconId
            }
        ]);

        return packetLoadWelcomeScreen;
    }
}
