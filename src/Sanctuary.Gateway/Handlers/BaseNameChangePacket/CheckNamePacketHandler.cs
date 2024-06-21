using System;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Sanctuary.Packet;
using Sanctuary.Database;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class CheckNamePacketHandler
{
    private static ILogger _logger = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CheckNamePacketHandler));

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
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
            checkNameResponsePacket.Result = -8; // CheckNameResponse.FoundItem

            connection.SendTunneled(checkNameResponsePacket);

            return true;
        }

        checkNameResponsePacket.Name = packet.Name;

        // TODO: Handle other types
        if (packet.Type != Packet.Common.NameChangeType.Character)
        {
            checkNameResponsePacket.Result = 4; // CheckNameResponse.Invalid

            connection.SendTunneled(checkNameResponsePacket);

            return true;
        }

        // TODO: Implement the following checks
        //  3 - Profane
        //  7 - FirstNameTooShort
        //  8 - LastNameTooShort
        //  9 - FirstNameTooLong
        // 10 - LastNameTooLong
        // 11 - IllegalCharacters

        if (string.IsNullOrEmpty(packet.Name.FirstName))
        {
            checkNameResponsePacket.Result = 5; // CheckNameResponse.IncorrectLength

            connection.SendTunneled(checkNameResponsePacket);

            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var taken = dbContext.Characters.Any(x => x.FirstName == packet.Name.FirstName && x.LastName == packet.Name.LastName);

        if (taken)
        {
            checkNameResponsePacket.Result = 2; // CheckNameResponse.Taken

            connection.SendTunneled(checkNameResponsePacket);

            return true;
        }

        checkNameResponsePacket.Result = 1; // CheckNameResponse.Available

        connection.SendTunneled(checkNameResponsePacket);

        return true;
    }
}