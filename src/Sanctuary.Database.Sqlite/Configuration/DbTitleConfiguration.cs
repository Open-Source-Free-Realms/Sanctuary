using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbTitleConfiguration : IEntityTypeConfiguration<DbTitle>
{
    public void Configure(EntityTypeBuilder<DbTitle> builder)
    {
        builder.HasKey(t => new { t.Id, t.CharacterId });
        builder.Property(t => t.Id).IsRequired().ValueGeneratedNever();
    }
}