namespace Sanctuary.Core.Configuration;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    public required DatabaseProvider Provider { get; set; }

    /// <summary>
    /// Required for <see cref="DatabaseProvider.MySql"/>.
    /// The general format is `major.minor.patch-type`, e.g. `8.0.21-mysql` or `10.5.3-mariadb`.
    /// If the type is being omitted, it is assumed to be MySQL (and not MariaDB).
    /// </summary>
    public string? VersionString { get; set; }
    public required string ConnectionString { get; set; }
}