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
    }
}
