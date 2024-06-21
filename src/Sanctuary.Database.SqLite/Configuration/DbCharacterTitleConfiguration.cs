using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbCharacterTitleConfiguration : IEntityTypeConfiguration<DbTitle>
{
    public void Configure(EntityTypeBuilder<DbTitle> builder)
    {
        throw new NotImplementedException();
    }
}