using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbIgnoreConfiguration : IEntityTypeConfiguration<DbIgnore>
{
    public void Configure(EntityTypeBuilder<DbIgnore> builder)
    {
        builder.HasKey(i => new { i.IgnoreCharacterId, i.CharacterId });

        builder.Property(i => i.Created).IsRequired().HasDefaultValueSql("DATE()");

        builder.HasOne(i => i.IgnoreCharacter)
            .WithMany()
            .HasForeignKey(f => f.IgnoreCharacterId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}