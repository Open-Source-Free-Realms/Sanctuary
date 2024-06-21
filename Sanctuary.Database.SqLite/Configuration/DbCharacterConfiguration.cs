using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbCharacterConfiguration : IEntityTypeConfiguration<DbCharacter>
{
    public void Configure(EntityTypeBuilder<DbCharacter> builder)
    {
        throw new NotImplementedException();
    }
}