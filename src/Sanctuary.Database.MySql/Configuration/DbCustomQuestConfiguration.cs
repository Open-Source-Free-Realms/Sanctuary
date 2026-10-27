using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbCustomQuestConfiguration : IEntityTypeConfiguration<DbCustomQuest>
{
    public void Configure(EntityTypeBuilder<DbCustomQuest> builder)
    {
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).IsRequired().ValueGeneratedOnAdd();

        builder.Property(q => q.Title).IsRequired().HasMaxLength(64);
        builder.Property(q => q.Description).IsRequired(false).HasMaxLength(400);

        builder.Property(q => q.TitleId).IsRequired();
        builder.Property(q => q.DescriptionId).IsRequired();

        builder.Property(q => q.GiverNpcId).IsRequired();
        builder.Property(q => q.TurnInNpcId).IsRequired();

        builder.Property(q => q.RewardCoins).IsRequired();

        builder.Property(q => q.PrerequisiteQuestId).IsRequired(false);
        builder.Property(q => q.NextQuestId).IsRequired(false);

        builder.Property(q => q.Goals).IsRequired();
        builder.Property(q => q.Rewards).IsRequired();

        builder.Property(q => q.IsPublished).IsRequired();

        builder.Property(q => q.UpdatedBy).IsRequired(false);
        builder.Property(q => q.Updated).IsRequired().HasDefaultValueSql("NOW()");
    }
}
