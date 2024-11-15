using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbUserConfiguration : IEntityTypeConfiguration<DbUser>
{
    public void Configure(EntityTypeBuilder<DbUser> builder)
    {
        builder.HasKey(u => u.Guid);
        builder.Property(u => u.Guid).IsRequired().ValueGeneratedOnAdd();

        builder.Property(u => u.Username).IsRequired().HasMaxLength(254);
        builder.Property(u => u.Password).IsRequired().HasMaxLength(254);

        builder.Property(u => u.Session).IsRequired(false).HasMaxLength(32);
        builder.Property(u => u.SessionCreated).IsRequired(false);

        builder.Property(u => u.MaxCharacters).IsRequired().HasDefaultValue(10);

        builder.Property(u => u.IsLocked).IsRequired().HasDefaultValue(false);
        builder.Property(u => u.IsMember).IsRequired().HasDefaultValue(false);
        builder.Property(u => u.IsAdmin).IsRequired().HasDefaultValue(false);

        builder.Property(u => u.Created).IsRequired().HasDefaultValueSql("NOW()");
        builder.Property(u => u.LastLogin).IsRequired(false);

        builder.HasMany(u => u.Characters)
            .WithOne(c => c.User)
            .HasForeignKey(c => c.UserGuid)
            .OnDelete(DeleteBehavior.Cascade);
    }
}