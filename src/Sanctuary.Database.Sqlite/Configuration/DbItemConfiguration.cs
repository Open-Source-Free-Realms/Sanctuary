using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbItemConfiguration : IEntityTypeConfiguration<DbItem>
{
    public void Configure(EntityTypeBuilder<DbItem> builder)
    {
        builder.HasKey(i => new { i.Id, i.CharacterId });
        builder.Property(i => i.Id).IsRequired().ValueGeneratedNever();
        builder.HasIndex(i => new { i.Tint, i.Definition, i.CharacterId }).IsUnique();

        builder.Property(i => i.Tint).IsRequired();
        builder.Property(i => i.Count).IsRequired();
        builder.Property(i => i.Definition).IsRequired();

        builder.Property(i => i.Created).IsRequired().HasDefaultValueSql("DATE()");
    }
}