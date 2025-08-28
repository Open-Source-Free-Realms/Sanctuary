using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbItemConfiguration : IEntityTypeConfiguration<DbItem>
{
    public void Configure(EntityTypeBuilder<DbItem> builder)
    {
        builder.HasKey(i => new { i.Id, i.CharacterGuid });
        builder.Property(i => i.Id).IsRequired().ValueGeneratedNever();

        builder.Property(i => i.Tint).IsRequired();
        builder.Property(i => i.Count).IsRequired().HasDefaultValue(1);

        builder.HasIndex(i => new { i.Tint, i.Definition, i.CharacterGuid }).IsUnique();
        builder.Property(i => i.Definition).IsRequired();

        builder.Property(i => i.Created).IsRequired().HasDefaultValueSql("NOW()");
    }
}