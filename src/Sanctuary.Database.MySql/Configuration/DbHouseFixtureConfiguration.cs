using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbHouseFixtureConfiguration : IEntityTypeConfiguration<DbHouseFixture>
{
    public void Configure(EntityTypeBuilder<DbHouseFixture> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Property(f => f.PlacementToken).IsRequired();
        builder.Property(f => f.ItemDefinitionId).IsRequired();
        builder.Property(f => f.TintId).IsRequired();
        builder.Property(f => f.PositionX).IsRequired();
        builder.Property(f => f.PositionY).IsRequired();
        builder.Property(f => f.PositionZ).IsRequired();
        builder.Property(f => f.PositionW).IsRequired();
        builder.Property(f => f.RotationX).IsRequired();
        builder.Property(f => f.RotationY).IsRequired();
        builder.Property(f => f.RotationZ).IsRequired();
        builder.Property(f => f.RotationW).IsRequired();
        builder.Property(f => f.Scale).IsRequired().HasDefaultValue(1f);
        builder.Property(f => f.CustomizationData).HasColumnType("TEXT");
        builder.Property(f => f.Created).IsRequired().HasDefaultValueSql("NOW()");
        builder.HasOne(f => f.House)
            .WithMany(h => h.Fixtures)
            .HasForeignKey(f => f.HouseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(f => new { f.HouseId, f.PlacementToken }).IsUnique();
    }
}
