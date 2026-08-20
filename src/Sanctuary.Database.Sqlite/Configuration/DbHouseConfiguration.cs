using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbHouseConfiguration : IEntityTypeConfiguration<DbHouse>
{
    public void Configure(EntityTypeBuilder<DbHouse> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).IsRequired().ValueGeneratedOnAdd();

        builder.HasIndex(h => new { h.CharacterId, h.ZoneDefinitionId }).IsUnique();
        builder.Property(h => h.ZoneDefinitionId).IsRequired();

        builder.Property(h => h.Created).IsRequired().HasDefaultValueSql("DATE()");
    }
}
