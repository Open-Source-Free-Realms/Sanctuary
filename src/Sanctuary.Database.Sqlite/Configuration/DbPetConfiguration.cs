using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbPetConfiguration : IEntityTypeConfiguration<DbPet>
{
    public void Configure(EntityTypeBuilder<DbPet> builder)
    {
        builder.HasKey(p => new { p.Id, p.CharacterId });
        builder.Property(p => p.Id).IsRequired().ValueGeneratedNever();

        builder.Property(p => p.Definition).IsRequired();
        builder.Property(p => p.Tint).IsRequired();
        builder.Property(p => p.Name).IsRequired().HasMaxLength(32);
        builder.Property(p => p.Hunger).IsRequired();
        builder.Property(p => p.Hygiene).IsRequired();
        builder.Property(p => p.Play).IsRequired();
        builder.Property(p => p.Mood).IsRequired();
        builder.Property(p => p.Trait).IsRequired();
        builder.Property(p => p.TreasureFoundAt).IsRequired(false);
        builder.Property(p => p.EquippedPetTrick1).IsRequired();
        builder.Property(p => p.EquippedPetTrick2).IsRequired();
        builder.Property(p => p.EquippedPetTrick3).IsRequired();
        builder.Property(p => p.EquippedPetTrick4).IsRequired();
        builder.Property(p => p.ShampooItemId).IsRequired();
        builder.Property(p => p.ConditionerItemId).IsRequired();
        builder.Property(p => p.ToyItemId).IsRequired();
        builder.Property(p => p.BrushItemId).IsRequired();
        builder.Property(p => p.FoodBowlItemId).IsRequired();
        builder.Property(p => p.FoodItemId).IsRequired();
        builder.Property(p => p.VitaminItemId).IsRequired();

        builder.Property(p => p.Created).IsRequired().HasDefaultValueSql("DATE()");

        builder.HasMany(p => p.Items)
            .WithMany(i => i.Pets)
            .UsingEntity(b => b.ToTable("PetItems"));
    }
}
