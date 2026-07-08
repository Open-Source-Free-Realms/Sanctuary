using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
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
            checkNameResponsePacket.Result = CheckNameResponse.FoundItem;

            connection.SendTunneled(checkNameResponsePacket);

            return true;
        }

        checkNameResponsePacket.Name = packet.Name;

        checkNameResponsePacket.Result = packet.Type switch
        {
            NameChangeType.Character => OnCheckCharacterName(connection, packet),
            _ => CheckNameResponse.Invalid
        };

        connection.SendTunneled(checkNameResponsePacket);

        return true;
    }

    private static CheckNameResponse OnCheckCharacterName(GatewayConnection connection, CheckNamePacket packet)
    {
        // TODO: Implement the following checks (https://archive.ph/3DB0L)
        //  3 - Profane
        // 11 - IllegalCharacters

        if (string.IsNullOrWhiteSpace(packet.Name.FirstName)
            || packet.Name.LastName != string.Empty && string.IsNullOrWhiteSpace(packet.Name.LastName))
        {
            return CheckNameResponse.IncorrectLength;
        }

        if (packet.Name.FirstName.Length < 3)
            return CheckNameResponse.FirstNameTooShort;

        if (packet.Name.FirstName.Length > 14)
            return CheckNameResponse.FirstNameTooLong;

        if (packet.Name.LastName != string.Empty)
        {
            if (packet.Name.LastName.Length < 3)
                return CheckNameResponse.LastNameTooShort;

            if (packet.Name.LastName.Length > 14)
                return CheckNameResponse.LastNameTooLong;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();
        var taken = dbContext.Characters.Any(x => x.FirstName == packet.Name.FirstName && x.LastName == packet.Name.LastName);

        if (taken)
            return CheckNameResponse.Taken;

        return CheckNameResponse.Available;
    }
}