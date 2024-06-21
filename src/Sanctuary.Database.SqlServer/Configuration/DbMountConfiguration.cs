using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.SqlServer.Configuration;

public sealed class DbMountConfiguration : IEntityTypeConfiguration<DbMount>
{
    public void Configure(EntityTypeBuilder<DbMount> builder)
    {
        throw new NotImplementedException();
    }
}