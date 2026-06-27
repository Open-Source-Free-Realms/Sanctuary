using System;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class CheckNamePacketHandler
{
    private static ILogger _logger = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;
    private static IResourceManager _resourceManager = null!;
    private const int MinNameLength = 3;
    private const int MaxNameLength = 14;
    private static readonly Regex ValidNamePartRegex = new("^[A-Za-z'-]+$", RegexOptions.Compiled);

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CheckNamePacketHandler));

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!CheckNamePacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(CheckNamePacket));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(CheckNamePacket), packet);

        var checkNameResponsePacket = new CheckNameResponsePacket();

        checkNameResponsePacket.Type = packet.Type;
        checkNameResponsePacket.Guid = packet.Guid;

        // TODO: Check if we have the item that let's us change name.
        if (packet.Token)
        {
            checkNameResponsePacket.Result = CheckNameResponse.FoundItem;

            connection.SendTunneled(checkNameResponsePacket);

            return true;
        }

        checkNameResponsePacket.Name = packet.Name;

        checkNameResponsePacket.Result = packet.Type switch
        {
            NameChangeType.Character => OnCheckCharacterName(packet),
            _ => CheckNameResponse.Invalid
        };

        connection.SendTunneled(checkNameResponsePacket);

        return true;
    }

    private static CheckNameResponse OnCheckCharacterName(CheckNamePacket packet)
    {
        var firstName = (packet.Name.FirstName ?? string.Empty).Trim();
        var lastName = (packet.Name.LastName ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(firstName))
            return CheckNameResponse.IncorrectLength;

        if (firstName.Length < MinNameLength)
            return CheckNameResponse.FirstNameTooShort;

        if (firstName.Length > MaxNameLength)
            return CheckNameResponse.FirstNameTooLong;

        if (lastName.Length > 0)
        {
            if (lastName.Length < MinNameLength)
                return CheckNameResponse.LastNameTooShort;

            if (lastName.Length > MaxNameLength)
                return CheckNameResponse.LastNameTooLong;
        }

        if (!HasValidCharacters(firstName) || (lastName.Length > 0 && !HasValidCharacters(lastName)))
            return CheckNameResponse.IllegalCharacters;

        if (_resourceManager.NameFilterBlockedSubstrings.Any(token => !string.IsNullOrWhiteSpace(token)
            && (firstName.Contains(token, StringComparison.OrdinalIgnoreCase)
                || (lastName.Length > 0 && lastName.Contains(token, StringComparison.OrdinalIgnoreCase)))))
        {
            return CheckNameResponse.Profane;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();
        var taken = dbContext.Characters.Any(x => x.FirstName == firstName && (x.LastName ?? string.Empty) == lastName);

        if (taken)
            return CheckNameResponse.Taken;

        return CheckNameResponse.Available;
    }

    private static bool HasValidCharacters(string namePart)
    {
        return ValidNamePartRegex.IsMatch(namePart);
    }
}
