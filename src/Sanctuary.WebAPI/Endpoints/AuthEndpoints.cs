using System;
using System.Threading;
using System.Threading.Tasks;

using BitArmory.ReCaptcha;
using BitArmory.Turnstile;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.WebAPI.Models;
using Sanctuary.WebAPI.Options;

using BC = BCrypt.Net.BCrypt;

namespace Sanctuary.WebAPI.Endpoints;

public static class AuthEndpoints
{
    private static ILogger _logger = null!;

    private static CaptchaOptions? _captchaOptions;
    private static TurnstileService? _turnstileService;
    private static ReCaptchaService? _reCaptchaService;

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();

        _logger = loggerFactory.CreateLogger(nameof(AuthEndpoints));

        _turnstileService = app.Services.GetService<TurnstileService>();
        _reCaptchaService = app.Services.GetService<ReCaptchaService>();

        var captchaOptions = app.Services.GetService<IOptions<CaptchaOptions>>();

        if (captchaOptions is { Value.IsConfigured: true })
            _captchaOptions = captchaOptions.Value;

        app.MapPost("/login", LoginHandlerAsync);
        app.MapPost("/register", RegisterHandlerAsync).DisableAntiforgery();
    }

    private static async Task<IResult> LoginHandlerAsync(
        LoginRequestModel request,
        DatabaseContext databaseContext,
        CancellationToken cancellationToken,
        IOptionsSnapshot<WebAPIOptions> webAPIOptions)
    {
        var dbUser = await databaseContext.Users.FirstOrDefaultAsync(x => x.Username == request.Username, cancellationToken);

        if (dbUser is null)
        {
            _logger.LogWarning("Login failed, user not found for username: {Username}", request.Username);

            return Results.Unauthorized();
        }

        if (!BC.Verify(request.Password, dbUser.Password))
        {
            _logger.LogWarning("Login failed, invalid password for username: {Username}", request.Username);

            return Results.Unauthorized();
        }

        dbUser.Session = Guid.NewGuid().ToString("N");
        dbUser.SessionCreated = DateTimeOffset.UtcNow;

        if (await databaseContext.SaveChangesAsync(cancellationToken) <= 0)
        {
            _logger.LogError("Failed to update session info for username: {Username}", dbUser.Username);

            return Results.InternalServerError();
        }

        return Results.Ok(new LoginResponseModel
        {
            SessionId = dbUser.Session,
            LaunchArguments = webAPIOptions.Value.LaunchArguments
        });
    }

    private static async Task<IResult> RegisterHandlerAsync(
        HttpContext context,
        [FromForm] string username,
        [FromForm] string password,
        DatabaseContext databaseContext,
        CancellationToken cancellationToken)
    {
        var captchaResult = await VerifyCaptchaAsync(context, cancellationToken);

        if (captchaResult is not null)
            return captchaResult;

        var usernameTaken = await databaseContext.Users.AnyAsync(x => x.Username == username, cancellationToken);

        if (usernameTaken)
        {
            _logger.LogWarning("Registration failed, username already taken {Username}", username);

            return Results.Ok(new
            {
                Success = false,
                ErrorCode = 1
            });
        }

        var salt = BC.GenerateSalt();
        var hashedPassword = BC.HashPassword(password, salt);

        var dbUser = new DbUser
        {
            Username = username,
            Password = hashedPassword
        };

        await databaseContext.Users.AddAsync(dbUser, cancellationToken);

        if (await databaseContext.SaveChangesAsync(cancellationToken) <= 0)
        {
            _logger.LogError("Failed to save new username: {Username}", username);

            return Results.InternalServerError();
        }

        return Results.Ok(new
        {
            Success = true
        });
    }

    private static async Task<IResult?> VerifyCaptchaAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (_captchaOptions is null || !_captchaOptions.IsConfigured)
            return null;

        var remoteIp = context.Connection.RemoteIpAddress?.ToString();

        switch (_captchaOptions.Provider)
        {
            case CaptchaProvider.Turnstile:
                {
                    if (_turnstileService is null)
                        return Results.InternalServerError();

                    if (!context.Request.Form.TryGetValue("cf-turnstile-response", out var turnstileToken))
                        return Results.Unauthorized();

                    var turnstileResult = await _turnstileService.VerifyAsync(
                        turnstileToken, _captchaOptions.Secret, remoteIp, null, cancellationToken);

                    if (!turnstileResult.IsSuccess)
                        return Results.Unauthorized();
                }
                break;

            case CaptchaProvider.ReCaptcha:
                {
                    if (_reCaptchaService is null)
                        return Results.InternalServerError();

                    if (!context.Request.Form.TryGetValue("recaptcha_token", out var recaptchaToken))
                        return Results.Unauthorized();

                    var recaptchaResult = await _reCaptchaService.Verify3Async(
                        recaptchaToken, _captchaOptions.Secret, remoteIp, cancellationToken);

                    if (!recaptchaResult.IsSuccess)
                        return Results.Unauthorized();
                }
                break;

            default:
                throw new NotImplementedException($"Captcha provider not implemented: {_captchaOptions.Provider}");
        }

        return null;
    }
}