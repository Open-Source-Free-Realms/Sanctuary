using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbHousingGuidConfiguration : IEntityTypeConfiguration<DbHousingGuid>
{
    public void Configure(EntityTypeBuilder<DbHousingGuid> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).IsRequired().ValueGeneratedOnAdd();

        builder.Property(g => g.Created).IsRequired().HasDefaultValueSql("DATE()");
    }
}
