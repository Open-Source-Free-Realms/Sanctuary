using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbMountConfiguration : IEntityTypeConfiguration<DbMount>
{
    public void Configure(EntityTypeBuilder<DbMount> builder)
    {
        builder.HasKey(m => new { m.Id, m.CharacterId });
        builder.Property(m => m.Id).IsRequired().ValueGeneratedNever();
        builder.HasIndex(m => new { m.Tint, m.Definition, m.CharacterId }).IsUnique();

        builder.Property(m => m.Tint).IsRequired();
        builder.Property(m => m.Definition).IsRequired();
        builder.Property(m => m.IsUpgraded).IsRequired();

        builder.Property(m => m.Created).IsRequired().HasDefaultValueSql("DATE()");
    }
}