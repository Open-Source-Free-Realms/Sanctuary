using System.IO;

using Microsoft.Extensions.Logging;
using Sanctuary.Core.Helpers;
using Sanctuary.Game.Resources;

namespace Sanctuary.Game;

public class ResourceManager : IResourceManager
{
    private ILogger _logger;
    private FileSystemWatcher _fileSystemWatcher;

    public const string BaseDirectory = "Resources";

    public readonly string HairMappingsFile = Path.Combine(BaseDirectory, "CharacterCreate", "HairMappings.txt");
    public readonly string HeadMappingsFile = Path.Combine(BaseDirectory, "CharacterCreate", "HeadMappings.txt");
    public readonly string SkinToneMappingsFile = Path.Combine(BaseDirectory, "CharacterCreate", "SkinToneMappings.txt");
    public readonly string FacePaintMappingsFile = Path.Combine(BaseDirectory, "CharacterCreate", "FacePaintMappings.txt");
    public readonly string ModelCustomizationMappingsFile = Path.Combine(BaseDirectory, "CharacterCreate", "ModelCustomizationMappings.txt");

    public readonly string ModelsFile = Path.Combine(BaseDirectory, "Models.txt");

    public readonly string ItemDefinitionsFile = Path.Combine(BaseDirectory, "ItemDefinitions.txt");
    public readonly string ItemClassesFile = Path.Combine(BaseDirectory, "ItemClasses.txt");
    public readonly string ItemCategoriesFile = Path.Combine(BaseDirectory, "ItemCategories.txt");
    public readonly string ItemCategoryGroupsFile = Path.Combine(BaseDirectory, "ItemCategoryGroups.txt");

    public readonly string ZonesFile = Path.Combine(BaseDirectory, "Zones.json");
    public readonly string ZonesDirectory = Path.Combine(BaseDirectory, "Zones");
    public readonly string MountsFile = Path.Combine(BaseDirectory, "Mounts.json");
    public readonly string ProfilesFile = Path.Combine(BaseDirectory, "Profiles.json");
    public readonly string QuickChatsFile = Path.Combine(BaseDirectory, "QuickChats.json");
    public readonly string PlayerTitlesFile = Path.Combine(BaseDirectory, "PlayerTitles.json");
    public readonly string PointOfInterestsFile = Path.Combine(BaseDirectory, "PointOfInterests.json");

    public IdToStringLookup HairMappings { get; }
    public IdToStringLookup HeadMappings { get; }
    public IdToStringLookup SkinToneMappings { get; }
    public IdToStringLookup FacePaintMappings { get; }
    public IdToStringLookup ModelCustomizationMappings { get; }

    public ModelDefinitionCollection Models { get; }

    public ItemDefinitionCollection ItemDefinitions { get; }
    public ItemClassDefinitionCollection ItemClasses { get; }
    public ItemCategoryDefinitionCollection ItemCategories { get; }
    public ItemCategoryGroupDefinitionCollection ItemCategoryGroups { get; }

    public ZoneDefinitionCollection Zones { get; }
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

        ItemDefinitions = new(_logger);
        ItemClasses = new(_logger);
        ItemCategories = new(_logger);
        ItemCategoryGroups = new(_logger);

        Zones = new(_logger);
        Mounts = new(_logger);
        Profiles = new(_logger);
        QuickChats = new(_logger);
        PlayerTitles = new(_logger);
        PointOfInterests = new(_logger);
    }

    public bool Load()
    {
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

        if (!ItemDefinitions.Load(ItemDefinitionsFile))
            return false;

        if (!ItemClasses.Load(ItemClassesFile))
            return false;

        if (!ItemCategories.Load(ItemCategoriesFile))
            return false;

        if (!ItemCategoryGroups.Load(ItemCategoryGroupsFile))
            return false;

        if (!Zones.Load(ZonesFile, ZonesDirectory))
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
            _fileSystemWatcher.EnableRaisingEvents = false;

            var filePath = e.FullPath;
            new Switch()
            {
                { () => filePath == HairMappingsFile, () => HairMappings.Load(HairMappingsFile) },
                { () => filePath == HeadMappingsFile, () => HeadMappings.Load(HeadMappingsFile) },
                { () => filePath == SkinToneMappingsFile, () => SkinToneMappings.Load(SkinToneMappingsFile) },
                { () => filePath == FacePaintMappingsFile, () => FacePaintMappings.Load(FacePaintMappingsFile) },
                { () => filePath == ModelCustomizationMappingsFile, () => ModelCustomizationMappings.Load(ModelCustomizationMappingsFile) },
                
                { () => filePath == ModelsFile, () => Models.Load(ModelsFile) },

                { () => filePath == ItemDefinitionsFile, () => ItemDefinitions.Load(ItemDefinitionsFile) },
                { () => filePath == ItemClassesFile, () => ItemClasses.Load(ItemClassesFile) },
                { () => filePath == ItemCategoriesFile, () => ItemCategories.Load(ItemCategoriesFile) },
                { () => filePath == ItemCategoryGroupsFile, () => ItemCategoryGroups.Load(ItemCategoryGroupsFile) },

                { () => filePath == ZonesFile, () => Zones.Load(ZonesFile, ZonesDirectory) },
                { () => filePath == MountsFile, () => Mounts.Load(MountsFile) },
                { () => filePath == ProfilesFile, () => Profiles.Load(ProfilesFile) },
                { () => filePath == QuickChatsFile, () => QuickChats.Load(QuickChatsFile) },
                { () => filePath == PlayerTitlesFile, () => PlayerTitles.Load(PlayerTitlesFile) },
                { () => filePath == PointOfInterestsFile, () => PointOfInterests.Load(PointOfInterestsFile) },

                {
                    () => true /* fallback case */,
                    () => _logger.LogWarning("Unknown file changed. File: {filepath}", e.FullPath)
                }
            }.Execute();
        }
        finally
        {
            _fileSystemWatcher.EnableRaisingEvents = true;
        }
    }
}