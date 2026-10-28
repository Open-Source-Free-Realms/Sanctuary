using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbCharacterQuestConfiguration : IEntityTypeConfiguration<DbCharacterQuest>
{
    public void Configure(EntityTypeBuilder<DbCharacterQuest> builder)
    {
        builder.HasKey(q => new { q.QuestId, q.CharacterId });
        builder.Property(q => q.QuestId).IsRequired().ValueGeneratedNever();

        builder.Property(q => q.Completed).IsRequired();
        builder.Property(q => q.CurrentGoalId).IsRequired();
        builder.Property(q => q.Started).IsRequired().HasDefaultValueSql("DATE()");

        builder.HasMany(q => q.Progress)
            .WithOne(p => p.Quest)
            .HasForeignKey(p => new { p.QuestId, p.CharacterId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
