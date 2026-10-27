using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbWorldEventConfiguration : IEntityTypeConfiguration<DbWorldEvent>
{
    public void Configure(EntityTypeBuilder<DbWorldEvent> builder)
    {
        builder.HasKey(e => e.Key);
        builder.Property(e => e.Key).IsRequired().HasMaxLength(32);

        builder.Property(e => e.IsActive).IsRequired();
    }
}
