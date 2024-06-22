using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbTitleConfiguration : IEntityTypeConfiguration<DbTitle>
{
    public void Configure(EntityTypeBuilder<DbTitle> builder)
    {
        builder.ToTable("Titles");

        builder.HasKey(c => new { c.Id, c.CharacterGuid });
        builder.Property(p => p.Id).IsRequired().ValueGeneratedNever();
    }
}