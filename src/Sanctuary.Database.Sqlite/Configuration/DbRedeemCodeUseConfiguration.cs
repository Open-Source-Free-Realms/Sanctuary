using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbRedeemCodeUseConfiguration : IEntityTypeConfiguration<DbRedeemCodeUse>
{
    public void Configure(EntityTypeBuilder<DbRedeemCodeUse> builder)
    {
        builder.HasKey(u => new { u.Code, u.CharacterId });
        builder.Property(u => u.Code).IsRequired().HasMaxLength(32);

        builder.Property(u => u.Count).IsRequired();
        builder.Property(u => u.LastUsed).IsRequired().HasDefaultValueSql("DATE()");
    }
}
