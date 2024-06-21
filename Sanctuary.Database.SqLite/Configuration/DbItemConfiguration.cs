using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbItemConfiguration : IEntityTypeConfiguration<DbItem>
{
    public void Configure(EntityTypeBuilder<DbItem> builder)
    {
        throw new NotImplementedException();
    }
}