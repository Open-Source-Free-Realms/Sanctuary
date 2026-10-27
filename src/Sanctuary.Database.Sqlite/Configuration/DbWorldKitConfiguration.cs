using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbWorldKitConfiguration : IEntityTypeConfiguration<DbWorldKit>
{
    public void Configure(EntityTypeBuilder<DbWorldKit> builder)
    {
        builder.HasKey(k => k.Name);
        builder.Property(k => k.Name).IsRequired().HasMaxLength(32);

        builder.Property(k => k.Entries).IsRequired();
        builder.Property(k => k.Note).IsRequired(false).HasMaxLength(200);

        builder.Property(k => k.UpdatedBy).IsRequired(false);
        builder.Property(k => k.Updated).IsRequired().HasDefaultValueSql("DATE()");
    }
}
