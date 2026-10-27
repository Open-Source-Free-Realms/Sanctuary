using System.Collections.Generic;

using Sanctuary.Game.Resources;

namespace Sanctuary.Game;

public interface IResourceManager
{
    IdToStringLookup HairMappings { get; }
    IdToStringLookup HeadMappings { get; }
    IdToStringLookup SkinToneMappings { get; }
    IdToStringLookup FacePaintMappings { get; }
    IdToStringLookup ModelCustomizationMappings { get; }

    IdToStringLookup FirstTimeEvents { get; }
    IdToStringLookup MiniGameTutorials { get; }

    ModelDefinitionCollection Models { get; }

    ClientItemDefinitionCollection ClientItemDefinitions { get; }
    CollectionDefinitionCollection Collections { get; }
    CollectionNodePoolDefinitionCollection CollectionNodePools { get; }
    CollectionNodeTypeDefinitionCollection CollectionNodeTypes { get; }
    CollectionNodeSpawnDefinitionCollection CollectionNodeSpawns { get; }

    CoinStoreItemCollection CoinStoreItems { get; }

    ItemClassDefinitionCollection ItemClasses { get; }
    ItemCategoryDefinitionCollection ItemCategories { get; }
    ItemCategoryGroupDefinitionCollection ItemCategoryGroups { get; }

    StoreDefinitionCollection Stores { get; }
    StoreBundleGroupDefinitionCollection StoreBundleGroups { get; }
    StoreBundleCategoryNodeCollection StoreBundleCategories { get; }
    StoreBundleCategoryGroupDefinitionCollection StoreBundleCategoryGroups { get; }

    ZoneDefinitionCollection Zones { get; }
    HouseDefinitionCollection Houses { get; }
    MountDefinitionCollection Mounts { get; }
    PlayerTitleCollection PlayerTitles { get; }
    ProfileDefinitionCollection Profiles { get; }
    QuickChatDefinitionCollection QuickChats { get; }
    PointOfInterestDefinitionCollection PointOfInterests { get; }
    ConsumableCollection Consumables { get; }
    NpcDefinitionCollection Npcs { get; }
    NameFilterCollection NameFilter { get; }
    MapGraphCollection Maps { get; }

    RewardTableDefinitionCollection RewardTables { get; }

    AbilityDefinitionCollection CombatAbilities { get; }
    JobKitDefinitionCollection CombatJobs { get; }

    bool Load();

    /// <summary>
    /// Names accepted by <see cref="TryReload"/>.
    /// </summary>
    IEnumerable<string> ReloadableResources { get; }

    /// <summary>
    /// Clears and reloads a single resource from disk, so edits to existing entries take effect.
    /// On failure the previous entries are restored.
    /// </summary>
    bool TryReload(string name);
}