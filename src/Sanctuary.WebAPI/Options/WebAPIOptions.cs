namespace Sanctuary.WebAPI.Options;

public class WebAPIOptions
{
    public const string Section = "WebAPI";

    public string? LaunchArguments { get; set; }

    /// <summary>
    /// Local mirror of the asset server (<c>manifest.txt.z</c> plus <c>000</c>-<c>999</c> bucket folders).
    /// </summary>
    public string? AssetsDirectory { get; set; }

    /// <summary>
    /// Asset server used for anything not in <see cref="AssetsDirectory"/> or the client mods.
    /// </summary>
    public string? AssetUpstreamAddress { get; set; }

    /// <summary>
    /// Folder (relative to the content root) holding modified client assets, e.g. the GM panel.
    /// </summary>
    public string? ClientModsDirectory { get; set; }
}