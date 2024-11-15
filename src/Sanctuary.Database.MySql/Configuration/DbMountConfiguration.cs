using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbMountConfiguration : IEntityTypeConfiguration<DbMount>
{
    public void Configure(EntityTypeBuilder<DbMount> builder)
    {
        builder.HasKey(p => new { p.Id, p.CharacterGuid });
        builder.Property(p => p.Id).IsRequired().ValueGeneratedNever();

        builder.Property(p => p.IsUpgraded).IsRequired();

        builder.Property(i => i.Created).IsRequired().HasDefaultValueSql("NOW()");
    }
}