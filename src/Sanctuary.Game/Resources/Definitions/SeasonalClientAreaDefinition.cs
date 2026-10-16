using System;
using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

public class SeasonalClientAreaDefinition
{
    public string Name { get; set; } = null!;

    public int StartMonth { get; set; }
    public int StartDay { get; set; }

    public int EndMonth { get; set; }
    public int EndDay { get; set; }

    public List<int> AreaIds { get; set; } = [];

    public bool IsActive(DateTime date)
    {
        var day = (date.Month, date.Day);
        var start = (StartMonth, StartDay);
        var end = (EndMonth, EndDay);

        if (start.CompareTo(end) <= 0)
            return day.CompareTo(start) >= 0 && day.CompareTo(end) <= 0;

        return day.CompareTo(start) >= 0 || day.CompareTo(end) <= 0;
    }
}
