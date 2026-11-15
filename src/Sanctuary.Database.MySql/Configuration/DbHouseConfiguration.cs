using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbHouseConfiguration : IEntityTypeConfiguration<DbHouse>
{
    public void Configure(EntityTypeBuilder<DbHouse> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).IsRequired().ValueGeneratedNever();

        builder.HasIndex(h => new { h.CharacterId, h.Key }).IsUnique();
        builder.Property(h => h.Key).IsRequired();
        builder.Property(h => h.ZoneId).IsRequired();

        builder.Property(h => h.Name).IsRequired().HasMaxLength(32);
        builder.Property(h => h.IsLocked).IsRequired();
        builder.Property(h => h.IsFloraAllowed).IsRequired();
        builder.Property(h => h.PetAutospawn).IsRequired();
        builder.Property(h => h.IsPublished).IsRequired();
        builder.Property(h => h.Description).IsRequired().HasMaxLength(256);
        builder.Property(h => h.Keywords).IsRequired().HasMaxLength(128);
        builder.Property(h => h.PublishedAt).IsRequired(false);

        builder.Property(h => h.Created).IsRequired().HasDefaultValueSql("NOW()");

        builder.HasMany(h => h.Votes)
            .WithOne(v => v.House)
            .HasForeignKey(v => v.HouseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
