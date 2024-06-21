using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbProfileConfiguration : IEntityTypeConfiguration<DbProfile>
{
    public void Configure(EntityTypeBuilder<DbProfile> builder)
    {
        builder.HasKey(p => new { p.Id, p.CharacterGuid });
        builder.Property(p => p.Id).IsRequired().ValueGeneratedNever();

        builder.Property(p => p.Level).IsRequired();
        builder.Property(p => p.LevelXP).IsRequired();

        builder.HasMany(p => p.Items)
            .WithMany(i => i.Profiles)
            .UsingEntity(b => b.ToTable("ProfileItems"));
    }
}