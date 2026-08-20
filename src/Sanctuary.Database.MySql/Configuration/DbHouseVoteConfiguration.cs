using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbHouseVoteConfiguration : IEntityTypeConfiguration<DbHouseVote>
{
    public void Configure(EntityTypeBuilder<DbHouseVote> builder)
    {
        builder.HasKey(v => new { v.HouseId, v.CharacterId });
        builder.Property(v => v.Value).IsRequired();
        builder.Property(v => v.Created).IsRequired().HasDefaultValueSql("NOW()");
        builder.HasOne(v => v.House)
            .WithMany(h => h.VoteRecords)
            .HasForeignKey(v => v.HouseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
