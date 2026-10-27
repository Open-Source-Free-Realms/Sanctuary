using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbScriptLibraryEntryConfiguration : IEntityTypeConfiguration<DbScriptLibraryEntry>
{
    public void Configure(EntityTypeBuilder<DbScriptLibraryEntry> builder)
    {
        builder.ToTable("ScriptLibrary");

        builder.HasKey(e => e.Name);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(32);

        builder.Property(e => e.Source).IsRequired();
        builder.Property(e => e.Description).IsRequired(false).HasMaxLength(200);

        builder.Property(e => e.UpdatedBy).IsRequired(false);
        builder.Property(e => e.Updated).IsRequired().HasDefaultValueSql("NOW()");
    }
}
