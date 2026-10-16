using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

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
        builder.Property(h => h.IsPublished).IsRequired();
        builder.Property(h => h.Description).IsRequired().HasMaxLength(256);
        builder.Property(h => h.Keywords).IsRequired().HasMaxLength(128);
        builder.Property(h => h.PublishedAt).IsRequired(false);

        builder.Property(h => h.Created).IsRequired().HasDefaultValueSql("DATE()");

        builder.HasMany(h => h.Votes)
            .WithOne(v => v.House)
            .HasForeignKey(v => new { v.HouseId, v.CharacterId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
