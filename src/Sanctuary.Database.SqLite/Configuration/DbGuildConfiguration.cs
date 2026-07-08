using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbGuildConfiguration : IEntityTypeConfiguration<DbGuild>
{
    public void Configure(EntityTypeBuilder<DbGuild> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).IsRequired().ValueGeneratedOnAdd();

        builder.HasIndex(g => g.Name).IsUnique();
        builder.Property(g => g.Name).IsRequired().HasMaxLength(32);

        builder.Property(g => g.MaxMembers).IsRequired().HasDefaultValue(100);

        builder.Property(g => g.Created).IsRequired().HasDefaultValueSql("DATE()");

        builder.HasMany(g => g.Members)
            .WithOne(gm => gm.Guild)
            .HasForeignKey(gm => gm.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
