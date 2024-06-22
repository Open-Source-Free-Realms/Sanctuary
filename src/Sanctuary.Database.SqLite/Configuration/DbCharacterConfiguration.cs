using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbCharacterConfiguration : IEntityTypeConfiguration<DbCharacter>
{
    public void Configure(EntityTypeBuilder<DbCharacter> builder)
    {
        builder.HasKey(c => c.Guid);
        builder.Property(c => c.Guid).IsRequired().ValueGeneratedOnAdd();

        builder.Property(c => c.Ticket).IsRequired(false);

        builder.Property(c => c.FirstName).IsRequired();
        builder.Property(c => c.LastName).IsRequired(false);

        builder.Property(c => c.Model).IsRequired();
        builder.Property(c => c.Head).IsRequired();
        builder.Property(c => c.Hair).IsRequired();

        builder.Property(c => c.ModelCustomization).IsRequired(false);
        builder.Property(c => c.FacePaint).IsRequired(false);

        builder.Property(c => c.SkinTone).IsRequired();
        builder.Property(c => c.EyeColor).IsRequired();
        builder.Property(c => c.HairColor).IsRequired();

        builder.ComplexProperty(c => c.Position).IsRequired();
        builder.ComplexProperty(c => c.Rotation).IsRequired();

        builder.Property(c => c.ActiveProfileId).IsRequired();

        builder.Property(c => c.Gender).IsRequired();

        builder.Property(c => c.ActiveTitleId).IsRequired(false);

        builder.Property(c => c.VipRank).IsRequired();
        builder.Property(c => c.MembershipStatus).IsRequired();

        builder.Property(c => c.ChatBubbleForegroundColor).IsRequired().HasDefaultValue(0x063C67);
        builder.Property(c => c.ChatBubbleBackgroundColor).IsRequired().HasDefaultValue(0xD4E2F0);
        builder.Property(c => c.ChatBubbleSize).IsRequired().HasDefaultValue(1);

        builder.Property(c => c.Created).IsRequired().HasDefaultValueSql("DATE()");
        builder.Property(c => c.LastLogin).IsRequired(false);

        builder.HasMany(c => c.Items)
            .WithOne(i => i.Character)
            .HasForeignKey(i => i.CharacterGuid)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Titles)
            .WithOne(t => t.Character)
            .HasForeignKey(t => t.CharacterGuid)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Mounts)
            .WithOne(m => m.Character)
            .HasForeignKey(p => p.CharacterGuid)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Profiles)
            .WithOne(p => p.Character)
            .HasForeignKey(p => p.CharacterGuid)
            .OnDelete(DeleteBehavior.Cascade);
    }
}