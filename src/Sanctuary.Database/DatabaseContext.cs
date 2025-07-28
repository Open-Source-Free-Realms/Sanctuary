using System;
using System.Reflection;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database;

public sealed class DatabaseContext : DbContext
{
    public DbSet<DbUser> Users => Set<DbUser>();
    public DbSet<DbItem> Items => Set<DbItem>();
    public DbSet<DbTitle> Titles => Set<DbTitle>();
    public DbSet<DbMount> Mounts => Set<DbMount>();
    public DbSet<DbProfile> Profiles => Set<DbProfile>();
    public DbSet<DbCharacter> Characters => Set<DbCharacter>();

    public DatabaseContext()
    {
    }

    public DatabaseContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
#if DEBUG
        // optionsBuilder.EnableDetailedErrors();
        // optionsBuilder.EnableSensitiveDataLogging();
#endif
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var assembly = LoadProviderAssembly();

        ArgumentNullException.ThrowIfNull(assembly);

        modelBuilder.ApplyConfigurationsFromAssembly(assembly);

        base.OnModelCreating(modelBuilder);
    }

    private Assembly? LoadProviderAssembly()
    {
        string? providerAssembly = null;

        if (Database.IsMySql())
            providerAssembly = $"{typeof(DatabaseFactory).Namespace}.MySql";
        else if (Database.IsSqlite())
            providerAssembly = $"{typeof(DatabaseFactory).Namespace}.Sqlite";

        ArgumentException.ThrowIfNullOrEmpty(providerAssembly);

        Assembly? assembly = null;

        try
        {
            assembly = EF.IsDesignTime
                     ? Assembly.Load(providerAssembly)
                     : Assembly.LoadFrom($"{providerAssembly}.dll");
        }
        catch
        {
        }

        return assembly;
    }
}