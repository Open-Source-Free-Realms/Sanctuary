using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Login.Handlers;

[PacketHandler]
public static class PacketCheckNameRequestHandler
{
    private static ILogger _logger = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PacketCheckNameRequestHandler));

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(LoginConnection connection, Span<byte> data)
    {
        if (!PacketCheckNameRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PacketCheckNameRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PacketCheckNameRequest), packet);

        if (string.IsNullOrEmpty(packet.FirstName) || string.IsNullOrWhiteSpace(packet.LastName))
        {
            _logger.LogError("Invalid name received.");
            return true;
        }

        var packetCheckNameReply = new PacketCheckNameReply
        {
            FirstName = packet.FirstName,
            LastName = packet.LastName
        };

        // TODO: Validate name against CharacterCreateNames.txt

        // Results
        // -1	Char_Name_Invalid_PS3
        //  4	Char_Name_Invalid_PS3
        //  2	Char_Name_Taken_PS3
        //  3	Char_Name_Profane_PS3
        //  5	Char_Name_Incorrect_Length_PS3
        //  6	Char_Server_Failure_PS3
        //  7	First_Name_Short_PS3
        //  8	Last_Name_Short_PS3
        //  9	First_Name_Long_PS3
        // 10	Last_Name_Long_PS3
        // def	Char_Unable_Log_Server_PS3

        using var dbContext = _dbContextFactory.CreateDbContext();

        var nameExists = dbContext.Characters.Any(x => x.FirstName == packet.FirstName && x.LastName == packet.LastName);

        packetCheckNameReply.Result = nameExists ? 2 : 1;

        connection.SendTunneled(packetCheckNameReply);

        return true;
    }
}