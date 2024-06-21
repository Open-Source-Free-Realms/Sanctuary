using System;
using System.IO;

using Microsoft.Extensions.Logging;

using nietras.SeparatedValues;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

public class ItemDefinitionCollection : ObservableConcurrentDictionary<int, ItemDefinition>
{
    private readonly ILogger _logger;

    public ItemDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogError("Failed to find file \"{file}\"", filePath);
            return false;
        }

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            using var reader = Sep.New('^')
                .Reader()
                .From(fileStream);

            foreach (var row in reader)
            {
                if (row.ColCount < 51)
                    continue;

                var itemDefinition = new ItemDefinition();

                var column = 0;

                if (!int.TryParse(row[column++].Span, out itemDefinition.Id))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.Type))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.NameId))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.DescriptionId))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.Icon.Id))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.ActivatableAbilityId))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.ActivatableRecastSeconds))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.PassiveAbilityId))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.Cost))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.Class))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.MaxStackSize))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.ProfileOverride))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.Slot))
                    continue;

                if (!float.TryParse(row[column++].Span, out itemDefinition.Range))
                    continue;

                if (!int.TryParse(row[column++].Span, out var noTrade))
                    continue;

                itemDefinition.NoTrade = noTrade != 0;

                if (!int.TryParse(row[column++].Span, out var singleUse))
                    continue;

                itemDefinition.SingleUse = singleUse != 0;

                itemDefinition.ModelName = row[column++].ToString();

                if (!int.TryParse(row[column++].Span, out itemDefinition.GenderUsage))
                    continue;

                itemDefinition.TextureAlias = row[column++].ToString();

                if (!int.TryParse(row[column++].Span, out itemDefinition.Icon.TintId))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.CategoryId))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.EquipRequirementId))
                    continue;

                if (!int.TryParse(row[column++].Span, out var membersOnly))
                    continue;

                itemDefinition.MembersOnly = membersOnly != 0;

                if (!int.TryParse(row[column++].Span, out var nonMiniGame))
                    continue;

                itemDefinition.NonMiniGame = nonMiniGame != 0;

                if (!int.TryParse(row[column++].Span, out itemDefinition.Param1))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.Param2))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.Param3))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.Param4))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.Param5))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.Param6))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.Param7))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.Param8))
                    continue;

                if (!int.TryParse(row[column++].Span, out var noSale))
                    continue;

                itemDefinition.NoSale = noSale != 0;

                if (!int.TryParse(row[column++].Span, out itemDefinition.ItemMaterialType))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.WeaponTrailEffectId))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.UseRequirementId))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.CompositeEffectId))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.PowerRating))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.MinProfileRank))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.Rarity))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.ContentId))
                    continue;

                itemDefinition.TintAlias = row[column++].ToString();

                if (!int.TryParse(row[column++].Span, out var combatOnly))
                    continue;

                itemDefinition.CombatOnly = combatOnly != 0;

                if (!int.TryParse(row[column++].Span, out itemDefinition.PreferredItemBarSlot))
                    continue;

                if (!int.TryParse(row[column++].Span, out var isTintable))
                    continue;

                itemDefinition.IsTintable = isTintable != 0;

                itemDefinition.StringParam1 = row[column++].ToString();

                if (!int.TryParse(row[column++].Span, out var forceDisablePreview))
                    continue;

                itemDefinition.ForceDisablePreview = forceDisablePreview != 0;

                if (!int.TryParse(row[column++].Span, out itemDefinition.MemberDiscount))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.RaceSetId))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.VipRankRequired))
                    continue;

                if (!int.TryParse(row[column++].Span, out itemDefinition.ClientEquipReqSetId))
                    continue;

                if (!TryAdd(itemDefinition.Id, itemDefinition))
                {
                    _logger.LogWarning("Failed to add entry. {id} \"{file}\"", itemDefinition.Id, filePath);
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }

        if (Count == 0)
        {
            _logger.LogError("No data was loaded. \"{file}\"", filePath);
            return false;
        }

        return true;
    }
}