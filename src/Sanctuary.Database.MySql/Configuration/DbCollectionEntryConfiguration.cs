using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbCollectionEntryConfiguration : IEntityTypeConfiguration<DbCollectionEntry>
{
    public void Configure(EntityTypeBuilder<DbCollectionEntry> builder)
    {
        builder.HasKey(entry => new { entry.CharacterId, entry.CollectionId, entry.EntryId });

        builder.Property(entry => entry.CollectionId).IsRequired();
        builder.Property(entry => entry.EntryId).IsRequired();
        builder.Property(entry => entry.Collected).IsRequired().HasDefaultValueSql("NOW()");
    }
}
