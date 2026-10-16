using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbHouseConfiguration : IEntityTypeConfiguration<DbHouse>
{
    public void Configure(EntityTypeBuilder<DbHouse> builder)
    {
        builder.HasKey(h => new { h.Id, h.CharacterId });
        builder.Property(h => h.Id).IsRequired().ValueGeneratedNever();

        builder.Property(h => h.Name).IsRequired().HasMaxLength(32);
        builder.Property(h => h.IsLocked).IsRequired();
        builder.Property(h => h.IsFloraAllowed).IsRequired();
        builder.Property(h => h.PetAutospawn).IsRequired();

        builder.Property(h => h.Created).IsRequired().HasDefaultValueSql("NOW()");
    }
}
