using System;
using System.Linq;
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
    public DbSet<DbFriend> Friends => Set<DbFriend>();
    public DbSet<DbIgnore> Ignores => Set<DbIgnore>();
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
        var prefix = $"{typeof(DatabaseFactory).Namespace}.";

        return AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a =>
            {
                var name = a.GetName().Name;
                return name is not null
                       && name.StartsWith(prefix, StringComparison.Ordinal)
                       && (name.EndsWith(".MySql", StringComparison.Ordinal) || name.EndsWith(".Sqlite", StringComparison.Ordinal));
            });
    }
}