using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbFriendConfiguration : IEntityTypeConfiguration<DbFriend>
{
    public void Configure(EntityTypeBuilder<DbFriend> builder)
    {
        builder.HasKey(f => new { f.FriendCharacterId, f.CharacterId });

        builder.Property(f => f.Created).IsRequired().HasDefaultValueSql("DATE()");

        builder.HasOne(f => f.FriendCharacter)
            .WithMany()
            .HasForeignKey(f => f.FriendCharacterId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}