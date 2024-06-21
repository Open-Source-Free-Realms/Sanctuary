using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.SqlServer.Configuration;

public sealed class DbProfileConfiguration : IEntityTypeConfiguration<DbProfile>
{
    public void Configure(EntityTypeBuilder<DbProfile> builder)
    {
        throw new NotImplementedException();
    }
}