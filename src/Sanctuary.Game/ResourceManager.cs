using System.Collections.Generic;
using System.IO;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Resources;

namespace Sanctuary.Game;

public class ResourceManager : IResourceManager
{
    private ILogger _logger;
    private FileSystemWatcher _fileSystemWatcher;

    public static readonly string BaseDirectory = "Resources";

    public static readonly string HairMappingsFile = Path.Combine(BaseDirectory, "CharacterCreate", "HairMappings.txt");
    public static readonly string HeadMappingsFile = Path.Combine(BaseDirectory, "CharacterCreate", "HeadMappings.txt");
    public static readonly string SkinToneMappingsFile = Path.Combine(BaseDirectory, "CharacterCreate", "SkinToneMappings.txt");
    public static readonly string FacePaintMappingsFile = Path.Combine(BaseDirectory, "CharacterCreate", "FacePaintMappings.txt");
    public static readonly string ModelCustomizationMappingsFile = Path.Combine(BaseDirectory, "CharacterCreate", "ModelCustomizationMappings.txt");

    public static readonly string ModelsFile = Path.Combine(BaseDirectory, "Models.txt");

    public static readonly string ClientItemDefinitionsFile = Path.Combine(BaseDirectory, "ClientItemDefinitions.json");

    public static readonly string CoinStoreItemsFile = Path.Combine(BaseDirectory, "CoinStoreItems.json");

    public static readonly string ItemClassesFile = Path.Combine(BaseDirectory, "ItemClasses.txt");
    public static readonly string ItemCategoriesFile = Path.Combine(BaseDirectory, "ItemCategories.txt");
    public static readonly string ItemCategoryGroupsFile = Path.Combine(BaseDirectory, "ItemCategoryGroups.txt");

    public static readonly string StoresFile = Path.Combine(BaseDirectory, "Stores.json");
    public static readonly string StoreBundlesFile = Path.Combine(BaseDirectory, "StoreBundles.json");
    public static readonly string StoreBundleGroupsFile = Path.Combine(BaseDirectory, "StoreBundleGroups.json");
    public static readonly string StoreBundleCategoriesFile = Path.Combine(BaseDirectory, "StoreBundleCategories.json");
    public static readonly string StoreBundleCategoryGroupsFile = Path.Combine(BaseDirectory, "StoreBundleCategoryGroups.json");

    public static readonly string ZonesDirectory = Path.Combine(BaseDirectory, "Zones");
    public static readonly string HousesFile = Path.Combine(BaseDirectory, "Houses.json");
    public static readonly string MountsFile = Path.Combine(BaseDirectory, "Mounts.json");
    public static readonly string ProfilesFile = Path.Combine(BaseDirectory, "Profiles.json");
    public static readonly string QuickChatsFile = Path.Combine(BaseDirectory, "QuickChats.json");
    public static readonly string PlayerTitlesFile = Path.Combine(BaseDirectory, "PlayerTitles.json");
    public static readonly string PointOfInterestsFile = Path.Combine(BaseDirectory, "PointOfInterests.json");
    public static readonly string NameFilterFile = Path.Combine(BaseDirectory, "NameFilter.txt");

    private ICollection<string> _nameFilterBlockedSubstrings = [];

    public ICollection<string> NameFilterBlockedSubstrings => _nameFilterBlockedSubstrings;

    public IdToStringLookup HairMappings { get; }
    public IdToStringLookup HeadMappings { get; }
    public IdToStringLookup SkinToneMappings { get; }
    public IdToStringLookup FacePaintMappings { get; }
    public IdToStringLookup ModelCustomizationMappings { get; }

    public ModelDefinitionCollection Models { get; }

    public ClientItemDefinitionCollection ClientItemDefinitions { get; }

    public CoinStoreItemCollection CoinStoreItems { get; }

    public ItemClassDefinitionCollection ItemClasses { get; }
    public ItemCategoryDefinitionCollection ItemCategories { get; }
    public ItemCategoryGroupDefinitionCollection ItemCategoryGroups { get; }

    public StoreDefinitionCollection Stores { get; }
    public StoreBundleGroupDefinitionCollection StoreBundleGroups { get; }
    public StoreBundleCategoryNodeCollection StoreBundleCategories { get; }
    public StoreBundleCategoryGroupDefinitionCollection StoreBundleCategoryGroups { get; }

    public ZoneDefinitionCollection Zones { get; }
    public HouseDefinitionCollection Houses { get; }
    public MountDefinitionCollection Mounts { get; }
    public PlayerTitleCollection PlayerTitles { get; }
    public ProfileDefinitionCollection Profiles { get; }
    public QuickChatDefinitionCollection QuickChats { get; }
    public PointOfInterestDefinitionCollection PointOfInterests { get; }

    public ResourceManager(ILogger<ResourceManager> logger)
    {
        _logger = logger;
        _fileSystemWatcher = new(BaseDirectory);

        _fileSystemWatcher.Changed += _fileSystemWatcher_Changed;
        _fileSystemWatcher.EnableRaisingEvents = true;

        HairMappings = new(_logger);
        HeadMappings = new(_logger);
        SkinToneMappings = new(_logger);
        FacePaintMappings = new(_logger);
        ModelCustomizationMappings = new(_logger);

        Models = new(_logger);

        ClientItemDefinitions = new(_logger);

        CoinStoreItems = new(_logger);

        ItemClasses = new(_logger);
        ItemCategories = new(_logger);
        ItemCategoryGroups = new(_logger);

        Stores = new(_logger);
        StoreBundleGroups = new(_logger);
        StoreBundleCategories = new(_logger);
        StoreBundleCategoryGroups = new(_logger);

        Zones = new(_logger);
        Houses = new(_logger);
        Mounts = new(_logger);
        Profiles = new(_logger);
        QuickChats = new(_logger);
        PlayerTitles = new(_logger);
        PointOfInterests = new(_logger);
    }

    public bool Load()
    {
        if (!NameFilterCollection.TryLoad(NameFilterFile, _logger, out var nameFilterBlockedSubstrings))
            return false;

        _nameFilterBlockedSubstrings = nameFilterBlockedSubstrings;

        if (!HairMappings.Load(HairMappingsFile))
            return false;

        if (!HeadMappings.Load(HeadMappingsFile))
            return false;

        if (!SkinToneMappings.Load(SkinToneMappingsFile))
            return false;

        if (!FacePaintMappings.Load(FacePaintMappingsFile))
            return false;

        if (!ModelCustomizationMappings.Load(ModelCustomizationMappingsFile))
            return false;

        if (!Models.Load(ModelsFile))
            return false;

        if (!ClientItemDefinitions.Load(ClientItemDefinitionsFile))
            return false;

        if (!CoinStoreItems.Load(CoinStoreItemsFile))
            return false;

        if (!ItemClasses.Load(ItemClassesFile))
            return false;

        if (!ItemCategories.Load(ItemCategoriesFile))
            return false;

        if (!ItemCategoryGroups.Load(ItemCategoryGroupsFile))
            return false;

        if (!Stores.Load(StoresFile) || !Stores.LoadBundles(StoreBundlesFile))
            return false;

        if (!StoreBundleGroups.Load(StoreBundleGroupsFile))
            return false;

        if (!StoreBundleCategories.Load(StoreBundleCategoriesFile))
            return false;

        if (!StoreBundleCategoryGroups.Load(StoreBundleCategoryGroupsFile))
            return false;

        if (!Zones.Load(ZonesDirectory))
            return false;

        if (!Houses.Load(HousesFile))
            return false;

        if (!Mounts.Load(MountsFile))
            return false;

        if (!Profiles.Load(ProfilesFile))
            return false;

        if (!QuickChats.Load(QuickChatsFile))
            return false;

        if (!PlayerTitles.Load(PlayerTitlesFile))
            return false;

        if (!PointOfInterests.Load(PointOfInterestsFile))
            return false;

        return true;
    }

    private void _fileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        try
        {
            if (File.GetAttributes(e.FullPath).HasFlag(FileAttributes.Directory))
                return;

            _fileSystemWatcher.EnableRaisingEvents = false;

            var loaded = false;

            if (e.FullPath == HairMappingsFile)
                loaded = HairMappings.Load(HairMappingsFile);
            else if (e.FullPath == HeadMappingsFile)
                loaded = HeadMappings.Load(HeadMappingsFile);
            else if (e.FullPath == SkinToneMappingsFile)
                loaded = SkinToneMappings.Load(SkinToneMappingsFile);
            else if (e.FullPath == FacePaintMappingsFile)
                loaded = FacePaintMappings.Load(FacePaintMappingsFile);
            else if (e.FullPath == ModelCustomizationMappingsFile)
                loaded = ModelCustomizationMappings.Load(ModelCustomizationMappingsFile);
            else if (e.FullPath == ModelsFile)
                loaded = Models.Load(ModelsFile);
            else if (e.FullPath == ClientItemDefinitionsFile)
                loaded = ClientItemDefinitions.Load(ClientItemDefinitionsFile);
            else if (e.FullPath == ItemClassesFile)
                loaded = ItemClasses.Load(ItemClassesFile);
            else if (e.FullPath == ItemCategoriesFile)
                loaded = ItemCategories.Load(ItemCategoriesFile);
            else if (e.FullPath == ItemCategoryGroupsFile)
                loaded = ItemCategoryGroups.Load(ItemCategoryGroupsFile);
            else if (e.FullPath == StoresFile)
                loaded = Stores.Load(StoresFile);
            else if (e.FullPath == StoreBundlesFile)
                loaded = Stores.LoadBundles(StoreBundlesFile);
            else if (e.FullPath == StoreBundleGroupsFile)
                loaded = StoreBundleGroups.Load(StoreBundleGroupsFile);
            else if (e.FullPath == StoreBundleCategoriesFile)
                loaded = StoreBundleCategories.Load(StoreBundleCategoriesFile);
            else if (e.FullPath == StoreBundleCategoryGroupsFile)
                loaded = StoreBundleCategoryGroups.Load(StoreBundleCategoryGroupsFile);
            else if (e.FullPath == HousesFile)
                loaded = Houses.Load(HousesFile);
            else if (e.FullPath == MountsFile)
                loaded = Mounts.Load(MountsFile);
            else if (e.FullPath == ProfilesFile)
                loaded = Profiles.Load(ProfilesFile);
            else if (e.FullPath == QuickChatsFile)
                loaded = QuickChats.Load(QuickChatsFile);
            else if (e.FullPath == PlayerTitlesFile)
                loaded = PlayerTitles.Load(PlayerTitlesFile);
            else if (e.FullPath == PointOfInterestsFile)
                loaded = PointOfInterests.Load(PointOfInterestsFile);
            else if (e.FullPath == NameFilterFile)
            {
                loaded = NameFilterCollection.TryLoad(NameFilterFile, _logger, out var nameFilterBlockedSubstrings);

                if (loaded)
                    _nameFilterBlockedSubstrings = nameFilterBlockedSubstrings;
            }
            else
                _logger.LogWarning("Unknown file changed. File: {filepath}", e.FullPath);

            if (!loaded)
                _logger.LogError("Error loading modified file. File: {filepath}", e.FullPath);
        }
        finally
        {
            _fileSystemWatcher.EnableRaisingEvents = true;
        }
    }
}