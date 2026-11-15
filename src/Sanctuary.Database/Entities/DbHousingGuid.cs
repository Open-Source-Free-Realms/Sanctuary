using System;

namespace Sanctuary.Database.Entities;

public class DbHousingGuid
{
    public ulong Id { get; set; }

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
}
