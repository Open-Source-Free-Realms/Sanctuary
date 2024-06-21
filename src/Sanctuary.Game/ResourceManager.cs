using System.IO;

using Microsoft.Extensions.Logging;

using Sanctuary.Game.Resources;

namespace Sanctuary.Game;

public class ResourceManager : IResourceManager
{
    private ILogger _logger;
    private FileSystemWatcher _fileSystemWatcher;

    public const string BaseDirectory = "Resources";

    public const string HairMappingsFile = @$"{BaseDirectory}\CharacterCreate\HairMappings.txt";
    public const string HeadMappingsFile = @$"{BaseDirectory}\CharacterCreate\HeadMappings.txt";
    public const string SkinToneMappingsFile = @$"{BaseDirectory}\CharacterCreate\SkinToneMappings.txt";
    public const string FacePaintMappingsFile = @$"{BaseDirectory}\CharacterCreate\FacePaintMappings.txt";
    public const string ModelCustomizationMappingsFile = @$"{BaseDirectory}\CharacterCreate\ModelCustomizationMappings.txt";

    public const string ModelsFile = @$"{BaseDirectory}\Models.txt";

    public const string ItemDefinitionsFile = @$"{BaseDirectory}\ItemDefinitions.txt";
    public const string ItemClassesFile = @$"{BaseDirectory}\ItemClasses.txt";
    public const string ItemCategoriesFile = @$"{BaseDirectory}\ItemCategories.txt";
    public const string ItemCategoryGroupsFile = @$"{BaseDirectory}\ItemCategoryGroups.txt";

    public const string ZonesFile = @$"{BaseDirectory}\Zones.json";
    public const string ZonesDirectory = @$"{BaseDirectory}\Zones";
    public const string MountsFile = @$"{BaseDirectory}\Mounts.json";
    public const string ProfilesFile = @$"{BaseDirectory}\Profiles.json";
    public const string QuickChatsFile = @$"{BaseDirectory}\QuickChats.json";
    public const string PlayerTitlesFile = @$"{BaseDirectory}\PlayerTitles.json";
    public const string PointOfInterestsFile = @$"{BaseDirectory}\PointOfInterests.json";

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

            switch (e.FullPath)
            {
                case HairMappingsFile: HairMappings.Load(HairMappingsFile); break;
                case HeadMappingsFile: HeadMappings.Load(HeadMappingsFile); break;
                case SkinToneMappingsFile: SkinToneMappings.Load(SkinToneMappingsFile); break;
                case FacePaintMappingsFile: FacePaintMappings.Load(FacePaintMappingsFile); break;
                case ModelCustomizationMappingsFile: ModelCustomizationMappings.Load(ModelCustomizationMappingsFile); break;

                case ModelsFile: Models.Load(ModelsFile); break;

                case ItemDefinitionsFile: ItemDefinitions.Load(ItemDefinitionsFile); break;
                case ItemClassesFile: ItemClasses.Load(ItemClassesFile); break;
                case ItemCategoriesFile: ItemCategories.Load(ItemCategoriesFile); break;
                case ItemCategoryGroupsFile: ItemCategoryGroups.Load(ItemCategoryGroupsFile); break;

                case ZonesFile: Zones.Load(ZonesFile, ZonesDirectory); break;
                case MountsFile: Mounts.Load(MountsFile); break;
                case ProfilesFile: Profiles.Load(ProfilesFile); break;
                case QuickChatsFile: QuickChats.Load(QuickChatsFile); break;
                case PlayerTitlesFile: PlayerTitles.Load(PlayerTitlesFile); break;
                case PointOfInterestsFile: PointOfInterests.Load(PointOfInterestsFile); break;

                default:
                    _logger.LogWarning("Unknown file changed. File: {filepath}", e.FullPath);
                    break;
            }
        }
        finally
        {
            _fileSystemWatcher.EnableRaisingEvents = true;
        }
    }
}