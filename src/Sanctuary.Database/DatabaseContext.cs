using Microsoft.EntityFrameworkCore;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database;

public abstract class DatabaseContext : DbContext
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
}