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

        builder.HasIndex(h => new { h.CharacterId, h.ZoneDefinitionId }).IsUnique();
        builder.HasOne(h => h.Character)
            .WithMany(c => c.Houses)
            .HasForeignKey(h => h.CharacterId);

        builder.Property(h => h.ZoneDefinitionId).IsRequired();

        builder.Property(h => h.Name).HasMaxLength(64);
        builder.Property(h => h.IsLocked).IsRequired();
        builder.Property(h => h.IsMembersOnly).IsRequired();
        builder.Property(h => h.IsFloraAllowed).IsRequired().HasDefaultValue(true);
        builder.Property(h => h.PetAutospawn).IsRequired();
        builder.Property(h => h.MaxFixtureCount).IsRequired().HasDefaultValue(2000);
        builder.Property(h => h.MaxLandmarkCount).IsRequired().HasDefaultValue(0);
        builder.Property(h => h.FurnitureScore).IsRequired().HasDefaultValue(0);
        builder.Property(h => h.IsPublished).IsRequired().HasDefaultValue(false);
        builder.Property(h => h.Votes).IsRequired().HasDefaultValue(0);
        builder.Property(h => h.Rating).IsRequired().HasDefaultValue(0f);
        builder.Property(h => h.Description).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(h => h.KeywordList).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(h => h.CustomizationData).HasColumnType("TEXT");

        builder.Property(h => h.Created).IsRequired().HasDefaultValueSql("NOW()");
        builder.Property(h => h.LastVisited).IsRequired().HasDefaultValueSql("NOW()");
    }
}
