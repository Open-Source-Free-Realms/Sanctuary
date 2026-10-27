using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbCustomItemConfiguration : IEntityTypeConfiguration<DbCustomItem>
{
    public void Configure(EntityTypeBuilder<DbCustomItem> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).IsRequired().ValueGeneratedOnAdd();

        builder.Property(i => i.BaseItemId).IsRequired();
        builder.Property(i => i.Label).IsRequired().HasMaxLength(64);
        builder.Property(i => i.Overrides).IsRequired();

        builder.Property(i => i.UpdatedBy).IsRequired(false);
        builder.Property(i => i.Updated).IsRequired().HasDefaultValueSql("DATE()");
    }
}
