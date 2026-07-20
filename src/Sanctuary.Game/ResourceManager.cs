using System;
using System.IO;
using System.Linq;

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
    public static readonly string CollectionsFile = Path.Combine(BaseDirectory, "Collections.json");
    public static readonly string CollectionNodePoolsFile = Path.Combine(BaseDirectory, "CollectionNodePools.json");
    public static readonly string CollectionNodeTypesFile = Path.Combine(BaseDirectory, "CollectionNodeTypes.json");
    public static readonly string CollectionNodeSpawnsDirectory = Path.Combine(BaseDirectory, "CollectionNodeSpawns");

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
    public static readonly string NpcsFile = Path.Combine(BaseDirectory, "Npcs.json");
    public static readonly string NameFilterFile = Path.Combine(BaseDirectory, "NameFilter.txt");

    public IdToStringLookup HairMappings { get; }
    public IdToStringLookup HeadMappings { get; }
    public IdToStringLookup SkinToneMappings { get; }
    public IdToStringLookup FacePaintMappings { get; }
    public IdToStringLookup ModelCustomizationMappings { get; }

    public ModelDefinitionCollection Models { get; }

    public ClientItemDefinitionCollection ClientItemDefinitions { get; }
    public CollectionDefinitionCollection Collections { get; }
    public CollectionNodePoolDefinitionCollection CollectionNodePools { get; }
    public CollectionNodeTypeDefinitionCollection CollectionNodeTypes { get; }
    public CollectionNodeSpawnDefinitionCollection CollectionNodeSpawns { get; }

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
    public NpcDefinitionCollection Npcs { get; }
    public NameFilterCollection NameFilter { get; }

    public ResourceManager(ILogger<ResourceManager> logger)
    {
        _logger = logger;
        _fileSystemWatcher = new(BaseDirectory)
        {
            IncludeSubdirectories = true
        };

        _fileSystemWatcher.Changed += _fileSystemWatcher_Changed;
        _fileSystemWatcher.EnableRaisingEvents = true;

        HairMappings = new(_logger);
        HeadMappings = new(_logger);
        SkinToneMappings = new(_logger);
        FacePaintMappings = new(_logger);
        ModelCustomizationMappings = new(_logger);

        Models = new(_logger);

        ClientItemDefinitions = new(_logger);
        Collections = new(_logger);
        CollectionNodePools = new(_logger);
        CollectionNodeTypes = new(_logger);
        CollectionNodeSpawns = new(_logger);

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
        Npcs = new(_logger);
        NameFilter = new(_logger);
    }

    public bool Load()
    {
        if (!NameFilter.Load(NameFilterFile))
            return false;

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

        if (!Collections.Load(CollectionsFile))
            return false;

        if (!CollectionNodeTypes.Load(CollectionNodeTypesFile))
            return false;

        if (!CollectionNodePools.Load(CollectionNodePoolsFile))
            return false;

        if (!CollectionNodeSpawns.Load(CollectionNodeSpawnsDirectory))
            return false;

        foreach (var collection in Collections.Values)
        {
            if (collection.Entries.Any(entry => !ClientItemDefinitions.ContainsKey(entry.ItemDefinitionId)))
            {
                _logger.LogError("Collection {id} references an unknown item definition.", collection.Id);
                return false;
            }
        }

        foreach (var type in CollectionNodeTypes.Values)
        {
            if (!Models.ContainsKey(type.ModelId) ||
                type.DropTable.Any(drop => !ClientItemDefinitions.ContainsKey(drop.ItemDefinitionId)))
            {
                _logger.LogError("Collection node type {type} has an invalid model or drop reference.", type.Key);
                return false;
            }
        }

        foreach (var spawn in CollectionNodeSpawns.Values)
        {
            if (!CollectionNodePools.TryGetValue(spawn.Pool, out var pool) ||
                pool.ZoneDefinitionId != spawn.ZoneDefinitionId)
            {
                _logger.LogError("Collection node spawn {id} references an unknown pool or mismatched zone.", spawn.Id);
                return false;
            }
        }

        foreach (var pool in CollectionNodePools.Values)
        {
            if (!CollectionNodeTypes.ContainsKey(pool.NodeType))
            {
                _logger.LogError("Collection node pool {pool} references an unknown node type.", pool.Key);
                return false;
            }
        }

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

        foreach (var pool in CollectionNodePools.Values)
        {
            if (!Zones.ContainsKey(pool.ZoneDefinitionId))
            {
                _logger.LogError("Collection node pool {pool} references unknown zone {zone}.",
                    pool.Key, pool.ZoneDefinitionId);
                return false;
            }
        }

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

        if (!Npcs.Load(NpcsFile))
            return false;

        return true;
    }

    private void _fileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        if (e.FullPath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) || !File.Exists(e.FullPath))
            return;

        var directoryPath = Path.GetDirectoryName(e.FullPath);
        var isTopLevelResource = string.Equals(directoryPath, BaseDirectory, StringComparison.OrdinalIgnoreCase);
        var collectionNodeSpawnPrefix = CollectionNodeSpawnsDirectory + Path.DirectorySeparatorChar;
        var isCollectionNodeSpawn = directoryPath is not null &&
            directoryPath.StartsWith(collectionNodeSpawnPrefix, StringComparison.OrdinalIgnoreCase);

        if (!isTopLevelResource && !isCollectionNodeSpawn)
            return;

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
            else if (e.FullPath == CollectionsFile)
                loaded = Collections.Load(CollectionsFile);
            else if (e.FullPath == CollectionNodePoolsFile)
                loaded = CollectionNodePools.Load(CollectionNodePoolsFile);
            else if (e.FullPath == CollectionNodeTypesFile)
                loaded = CollectionNodeTypes.Load(CollectionNodeTypesFile);
            else if (isCollectionNodeSpawn &&
                string.Equals(Path.GetExtension(e.FullPath), ".json", StringComparison.OrdinalIgnoreCase))
                loaded = CollectionNodeSpawns.Load(CollectionNodeSpawnsDirectory);
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
            else if (e.FullPath == NpcsFile)
                loaded = Npcs.Load(NpcsFile);
            else if (e.FullPath == NameFilterFile)
                loaded = NameFilter.Load(NameFilterFile);
            else
                _logger.LogWarning("Unknown file changed. File: {filepath}", e.FullPath);

            if (!loaded)
                _logger.LogError("Error loading modified file. File: {filepath}", e.FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling modified file. File: {filepath}", e.FullPath);
        }
        finally
        {
            _fileSystemWatcher.EnableRaisingEvents = true;
        }
    }
}