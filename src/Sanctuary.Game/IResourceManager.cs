using Sanctuary.Game.Resources;

namespace Sanctuary.Game;

public interface IResourceManager
{
    IdToStringLookup HairMappings { get; }
    IdToStringLookup HeadMappings { get; }
    IdToStringLookup SkinToneMappings { get; }
    IdToStringLookup FacePaintMappings { get; }
    IdToStringLookup ModelCustomizationMappings { get; }

    ModelDefinitionCollection Models { get; }

    ClientItemDefinitionCollection ClientItemDefinitions { get; }

    CoinStoreItemCollection CoinStoreItems { get; }

    ItemClassDefinitionCollection ItemClasses { get; }
    ItemCategoryDefinitionCollection ItemCategories { get; }
    ItemCategoryGroupDefinitionCollection ItemCategoryGroups { get; }

    StoreDefinitionCollection Stores { get; }
    StoreBundleGroupDefinitionCollection StoreBundleGroups { get; }
    StoreBundleCategoryNodeCollection StoreBundleCategories { get; }
    StoreBundleCategoryGroupDefinitionCollection StoreBundleCategoryGroups { get; }

    ZoneDefinitionCollection Zones { get; }
    MountDefinitionCollection Mounts { get; }
    PlayerTitleCollection PlayerTitles { get; }
    ProfileDefinitionCollection Profiles { get; }
    QuickChatDefinitionCollection QuickChats { get; }
    PointOfInterestDefinitionCollection PointOfInterests { get; }

    bool Load();
}