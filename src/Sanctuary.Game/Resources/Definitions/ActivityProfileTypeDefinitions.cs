using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

public class ActivityCategoryProfileTypeDefinition
{
    public int Category { get; set; }
    public string? Comment { get; set; }
    public ProfileType ProfileType { get; set; }
}

public class ActivityFilenameProfileTypeDefinition
{
    public string Marker { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public ProfileType ProfileType { get; set; }
}

public class MiniGameTypeProfileTypeDefinition
{
    public MiniGameType MiniGameType { get; set; }
    public ProfileType ProfileType { get; set; }
}

public class ActivityProfileTypeDefinitions
{
    public List<ActivityCategoryProfileTypeDefinition> Categories { get; set; } = [];
    public List<ActivityFilenameProfileTypeDefinition> FilenameMarkers { get; set; } = [];
    public List<MiniGameTypeProfileTypeDefinition> MiniGameTypes { get; set; } = [];
}
