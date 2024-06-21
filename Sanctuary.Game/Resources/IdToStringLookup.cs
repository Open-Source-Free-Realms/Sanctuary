using System;
using System.IO;

using Microsoft.Extensions.Logging;

using nietras.SeparatedValues;

using Sanctuary.Core.Collections;

namespace Sanctuary.Game.Resources;

public class IdToStringLookup : ObservableConcurrentDictionary<int, string>
{
    private readonly ILogger _logger;

    public IdToStringLookup(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogError("Failed to find file \"{file}\"", filePath);
            return false;
        }

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            using var reader = Sep.New('^')
                .Reader()
                .From(fileStream);

            foreach (var row in reader)
            {
                if (row.ColCount < 2)
                    continue;

                if (!int.TryParse(row[0].Span, out var id))
                    continue;

                var value = row[1].ToString();

                if (string.IsNullOrEmpty(value))
                    continue;

                if (!TryAdd(id, value))
                {
                    _logger.LogWarning("Failed to add entry. {id} \"{file}\"", id, filePath);
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse file \"{file}\".", filePath);
            return false;
        }

        if (Count == 0)
        {
            _logger.LogError("No data was loaded. \"{file}\"", filePath);
            return false;
        }

        return true;
    }
}