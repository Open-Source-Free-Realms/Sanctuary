using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbRedeemCodeConfiguration : IEntityTypeConfiguration<DbRedeemCode>
{
    public void Configure(EntityTypeBuilder<DbRedeemCode> builder)
    {
        builder.HasKey(c => c.Code);
        builder.Property(c => c.Code).IsRequired().HasMaxLength(32);

        builder.Property(c => c.Items).IsRequired();
        builder.Property(c => c.Coins).IsRequired();

        builder.Property(c => c.MaxUses).IsRequired();
        builder.Property(c => c.Uses).IsRequired();
        builder.Property(c => c.PerCharacterLimit).IsRequired().HasDefaultValue(1);

        builder.Property(c => c.StartsAt).IsRequired(false);
        builder.Property(c => c.ExpiresAt).IsRequired(false);

        builder.Property(c => c.MembersOnly).IsRequired();
        builder.Property(c => c.Enabled).IsRequired().HasDefaultValue(true);

        builder.Property(c => c.Note).IsRequired(false).HasMaxLength(200);

        builder.Property(c => c.CreatedBy).IsRequired(false);
        builder.Property(c => c.Created).IsRequired().HasDefaultValueSql("NOW()");
    }
}
