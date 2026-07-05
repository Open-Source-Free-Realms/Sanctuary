using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sanctuary.Core.Configuration;
using Sanctuary.Database;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Login.Handlers;

[PacketHandler]
public static class LoginRequestHandler
{
    private static ILogger _logger = null!;
    private static LoginServerOptions _options = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(LoginRequestHandler));

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();

        var options = serviceProvider.GetRequiredService<IOptionsMonitor<LoginServerOptions>>();
        _options = options.CurrentValue;
        options.OnChange(o => _options = o);
    }

    public static bool HandlePacket(LoginConnection connection, Span<byte> data)
    {
        if (!LoginRequest.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(LoginRequest));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(LoginRequest), packet);

        var loginReply = new LoginReply();

      
        if (connection.UserId > 0)
        {
            connection.Send(loginReply);

            _logger.LogWarning("User tried to login twice. ( UserId: {UserId}, Session: {session} )", connection.UserId, packet.Session);

            return true;
        }

        using var dbContext = _dbContextFactory.CreateDbContext();

        var user = dbContext.Users.SingleOrDefault(x => x.Session == packet.Session);

        if (user == null)
        {
            
            System.Threading.Thread.Sleep(50);

            user = dbContext.Users.SingleOrDefault(x => x.Session == packet.Session);
        }

        if (user is null || !user.SessionCreated.HasValue)
        {
            connection.Send(loginReply);

            _logger.LogWarning(
                "Invalid session | Session: {session}",
                packet.Session
            );

            return true;
        }

        var now = DateTimeOffset.UtcNow;

        if ((now - user.SessionCreated.Value).TotalMinutes > 20)
        {
            connection.Send(loginReply);

            _logger.LogWarning(
                "Expired session | Session: {session} | Created: {created}",
                packet.Session,
                user.SessionCreated
            );

            return true;
        }

     
        user.LastLogin = now;

        dbContext.SaveChanges();

        
        if (_options.IsLocked && !user.IsAdmin)
        {
            loginReply.Status = 2;

            connection.Send(loginReply);

            return true;
        }

        connection.UserId = user.Id;

        loginReply.LoggedIn = true;
        loginReply.Status = 1;
        loginReply.IsMember = user.IsMember;

        var accountInfo = new AccountInfo
        {
            IsMember = user.IsMember,
            MaxCharacters = user.MaxCharacters,
            IsAdminAccount = user.IsAdmin
        };

        loginReply.Payload = accountInfo.Serialize();

        connection.Send(loginReply);

        return true;
    }
}