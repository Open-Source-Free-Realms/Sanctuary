using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbWorldFixtureConfiguration : IEntityTypeConfiguration<DbWorldFixture>
{
    public void Configure(EntityTypeBuilder<DbWorldFixture> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).IsRequired().ValueGeneratedOnAdd();
        builder.HasIndex(f => f.Zone);

        builder.Property(f => f.Zone).IsRequired();

        builder.Property(f => f.Tint).IsRequired();
        builder.Property(f => f.Definition).IsRequired();

        builder.Property(f => f.PositionX).IsRequired();
        builder.Property(f => f.PositionY).IsRequired();
        builder.Property(f => f.PositionZ).IsRequired();

        builder.Property(f => f.RotationX).IsRequired();
        builder.Property(f => f.RotationY).IsRequired();
        builder.Property(f => f.RotationZ).IsRequired();
        builder.Property(f => f.RotationW).IsRequired();

        builder.Property(f => f.Scale).IsRequired().HasDefaultValue(1.0f);

        builder.Property(f => f.Collidable).IsRequired().HasDefaultValue(true);

        builder.Property(f => f.Script).IsRequired(false);

        builder.Property(f => f.CreatedBy).IsRequired(false);

        builder.Property(f => f.Created).IsRequired().HasDefaultValueSql("DATE()");
    }
}