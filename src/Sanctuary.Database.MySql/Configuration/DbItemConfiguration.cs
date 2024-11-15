using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbItemConfiguration : IEntityTypeConfiguration<DbItem>
{
    public void Configure(EntityTypeBuilder<DbItem> builder)
    {
        builder.HasKey(p => new { p.Id, p.CharacterGuid });
        builder.Property(p => p.Id).IsRequired().ValueGeneratedNever();

        builder.Property(i => i.Tint).IsRequired();
        builder.Property(i => i.Count).IsRequired().HasDefaultValue(1);
        builder.Property(i => i.Definition).IsRequired();

        builder.Property(i => i.Created).IsRequired().HasDefaultValueSql("NOW()");
    }
}