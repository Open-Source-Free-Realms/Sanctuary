using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbGuildMemberConfiguration : IEntityTypeConfiguration<DbGuildMember>
{
    public void Configure(EntityTypeBuilder<DbGuildMember> builder)
    {
        builder.HasKey(gm => gm.Id);
        builder.Property(gm => gm.Id).IsRequired().ValueGeneratedNever();

        builder.Property(gm => gm.Joined).IsRequired().HasDefaultValueSql("DATE()");

        builder.HasOne(gm => gm.Guild)
            .WithMany(g => g.Members)
            .HasForeignKey(gm => gm.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
