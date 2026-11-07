using System;
using System.IO;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using nietras.SeparatedValues;

using Sanctuary.Core.Collections;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Resources;

/// <summary>
/// This class allows you to read TintGroupMappings as group/tint/order for the purpose of assigning them to items/clothing/etc without super 
/// severe code dupes. It is being used by Mystery Chests to assign the clothing random tints each time a piece of clothing is unlocked.
/// It works for clothes that are not or have never been put into the Coin Store or SC Shop and can probably also work for random tints from
/// spinning wheels after combat/a game/on the Daily wheel etc in the future. 
/// It allows the server to know what tints go where, when the player is not in control on the client side of what tint they will receive,
/// unlike Coin & SC entries which the player picks off of visually, which the client handles.
/// </summary>

public class TintGroupCollection : ObservableConcurrentDictionary<int, List<int>>
{
    private readonly ILogger _logger;

    public TintGroupCollection(ILogger logger)
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

        Clear();

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            using var reader = Sep.New('^')
                .Reader()
                .From(fileStream);

            foreach (var row in reader)
            {
                if (row.ColCount < 2)
                    continue;

                if (!int.TryParse(row[0].Span, out var groupId) || !int.TryParse(row[1].Span, out var tintId))
                    continue;

                if (!TryGetValue(groupId, out var tints))
                {
                    tints = [];
                    TryAdd(groupId, tints);
                }

                tints.Add(tintId);
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