using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbCharacterQuestProgressConfiguration : IEntityTypeConfiguration<DbCharacterQuestProgress>
{
    public void Configure(EntityTypeBuilder<DbCharacterQuestProgress> builder)
    {
        builder.HasKey(p => new { p.CharacterId, p.QuestId, p.GoalId, p.TargetId });

        builder.Property(p => p.GoalId).IsRequired().ValueGeneratedNever();
        builder.Property(p => p.TargetId).IsRequired().ValueGeneratedNever();
        builder.Property(p => p.Count).IsRequired();
    }
}
