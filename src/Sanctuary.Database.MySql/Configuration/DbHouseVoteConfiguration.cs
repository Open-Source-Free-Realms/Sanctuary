using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbHouseVoteConfiguration : IEntityTypeConfiguration<DbHouseVote>
{
    public void Configure(EntityTypeBuilder<DbHouseVote> builder)
    {
        builder.HasKey(v => new { v.HouseId, v.VoterId });

        builder.Property(v => v.Rating).IsRequired();
        builder.Property(v => v.Category).IsRequired().HasMaxLength(32);

        builder.Property(v => v.Created).IsRequired().HasDefaultValueSql("NOW()");
    }
}
