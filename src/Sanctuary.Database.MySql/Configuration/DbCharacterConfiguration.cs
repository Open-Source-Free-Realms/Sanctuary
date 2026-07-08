using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbCharacterConfiguration : IEntityTypeConfiguration<DbCharacter>
{
    public void Configure(EntityTypeBuilder<DbCharacter> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).IsRequired().ValueGeneratedOnAdd();

        builder.Property(c => c.Ticket).IsRequired(false);

        builder.Property(c => c.FirstName).IsRequired().HasMaxLength(16);
        builder.Property(c => c.LastName).IsRequired(false).HasMaxLength(16);
        builder.Property(c => c.FullName).HasMaxLength(33).HasComputedColumnSql($"CONCAT_WS(' ', `{nameof(DbCharacter.FirstName)}`, NULLIF(`{nameof(DbCharacter.LastName)}`, ''))", true);

        builder.Property(c => c.Model).IsRequired();

        builder.Property(c => c.Head).IsRequired();
        builder.Property(c => c.HeadId).IsRequired();

        builder.Property(c => c.Hair).IsRequired();
        builder.Property(c => c.HairId).IsRequired();

        builder.Property(c => c.ModelCustomization).IsRequired(false);
        builder.Property(c => c.ModelCustomizationId).IsRequired(false);

        builder.Property(c => c.FacePaint).IsRequired(false);
        builder.Property(c => c.FacePaintId).IsRequired(false);

        builder.Property(c => c.SkinTone).IsRequired();
        builder.Property(c => c.SkinToneId).IsRequired();

        builder.Property(c => c.EyeColor).IsRequired();
        builder.Property(c => c.HairColor).IsRequired();

        builder.Property(c => c.PositionX).IsRequired(false);
        builder.Property(c => c.PositionY).IsRequired(false);
        builder.Property(c => c.PositionZ).IsRequired(false);

        builder.Property(c => c.RotationX).IsRequired(false);
        builder.Property(c => c.RotationZ).IsRequired(false);

        builder.Property(c => c.ActiveProfileId).IsRequired();

        builder.Property(c => c.Gender).IsRequired();

        builder.Property(c => c.ActiveTitleId).IsRequired(false);

        builder.Property(c => c.VipRank).IsRequired();
        builder.Property(c => c.MembershipStatus).IsRequired();

        builder.Property(c => c.ChatBubbleForegroundColor).IsRequired().HasDefaultValue(0x063C67);
        builder.Property(c => c.ChatBubbleBackgroundColor).IsRequired().HasDefaultValue(0xD4E2F0);
        builder.Property(c => c.ChatBubbleSize).IsRequired().HasDefaultValue(1);

        builder.Property(c => c.Coins).IsRequired();
        builder.Property(c => c.StationCash).IsRequired();

        builder.Property(c => c.Created).IsRequired().HasDefaultValueSql("NOW()");
        builder.Property(c => c.LastLogin).IsRequired(false);

        builder.HasMany(c => c.Items)
            .WithOne(i => i.Character)
            .HasForeignKey(i => i.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Titles)
            .WithOne(t => t.Character)
            .HasForeignKey(t => t.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Mounts)
            .WithOne(m => m.Character)
            .HasForeignKey(m => m.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Friends)
            .WithOne(f => f.Character)
            .HasForeignKey(f => f.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Ignores)
            .WithOne(i => i.Character)
            .HasForeignKey(i => i.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Profiles)
            .WithOne(p => p.Character)
            .HasForeignKey(p => p.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}