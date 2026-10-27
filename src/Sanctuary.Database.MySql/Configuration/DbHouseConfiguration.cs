using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbHouseConfiguration : IEntityTypeConfiguration<DbHouse>
{
    public void Configure(EntityTypeBuilder<DbHouse> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).IsRequired().ValueGeneratedOnAdd();
        builder.HasIndex(h => new { h.Definition, h.CharacterId }).IsUnique();

        builder.Property(h => h.Definition).IsRequired();

        builder.Property(h => h.NameId).IsRequired();
        builder.Property(h => h.Name).HasMaxLength(32);

        builder.Property(h => h.IsLocked).IsRequired();
        builder.Property(h => h.IsFloraAllowed).IsRequired().HasDefaultValue(true);
        builder.Property(h => h.PetAutospawn).IsRequired();

        builder.Property(h => h.Created).IsRequired().HasDefaultValueSql("NOW()");
        builder.Property(h => h.LastVisited).IsRequired(false);

        builder.HasMany(h => h.Fixtures)
            .WithOne(f => f.House)
            .HasForeignKey(f => f.HouseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}