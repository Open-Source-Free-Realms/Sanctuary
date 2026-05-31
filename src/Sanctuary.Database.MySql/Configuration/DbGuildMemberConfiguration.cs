using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbGuildMemberConfiguration : IEntityTypeConfiguration<DbGuildMember>
{
    public void Configure(EntityTypeBuilder<DbGuildMember> builder)
    {
        builder.HasKey(gm => gm.Id);
        builder.Property(gm => gm.Id).IsRequired().ValueGeneratedNever();

        builder.Property(g => g.Joined).IsRequired().HasDefaultValueSql("NOW()");

        builder.HasOne(gm => gm.Guild)
            .WithMany(g => g.Members)
            .HasForeignKey(gm => gm.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}