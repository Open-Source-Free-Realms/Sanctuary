using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbWorldNpcConfiguration : IEntityTypeConfiguration<DbWorldNpc>
{
    public void Configure(EntityTypeBuilder<DbWorldNpc> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).IsRequired().ValueGeneratedOnAdd();
        builder.HasIndex(n => n.Zone);

        builder.Property(n => n.Zone).IsRequired();
        builder.Property(n => n.Definition).IsRequired();

        builder.Property(n => n.Name).IsRequired(false).HasMaxLength(64);
        builder.Property(n => n.NameId).IsRequired();

        builder.Property(n => n.ModelId).IsRequired();
        builder.Property(n => n.Scale).IsRequired().HasDefaultValue(1.0f);

        builder.Property(n => n.Animation).IsRequired();
        builder.Property(n => n.Disposition).IsRequired();
        builder.Property(n => n.HideNamePlate).IsRequired();

        builder.Property(n => n.Behavior).IsRequired();
        builder.Property(n => n.WanderRadius).IsRequired();
        builder.Property(n => n.Speed).IsRequired();

        builder.Property(n => n.PositionX).IsRequired();
        builder.Property(n => n.PositionY).IsRequired();
        builder.Property(n => n.PositionZ).IsRequired();

        builder.Property(n => n.RotationX).IsRequired();
        builder.Property(n => n.RotationY).IsRequired();
        builder.Property(n => n.RotationZ).IsRequired();
        builder.Property(n => n.RotationW).IsRequired();

        builder.Property(n => n.EventKey).IsRequired(false).HasMaxLength(32);

        builder.Property(n => n.Script).IsRequired(false);
        builder.Property(n => n.Conversation).IsRequired(false);

        builder.Property(n => n.CreatedBy).IsRequired(false);

        builder.Property(n => n.Created).IsRequired().HasDefaultValueSql("DATE()");
    }
}
