using System;

using BitArmory.ReCaptcha;
using BitArmory.Turnstile;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NLog.Extensions.Logging;

using Sanctuary.Core.Configuration;
using Sanctuary.Database;
using Sanctuary.WebAPI.Endpoints;
using Sanctuary.WebAPI.Options;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls();

// Options
builder.Services.AddOptions<DatabaseOptions>()
    .BindConfiguration(DatabaseOptions.Section)
    .ValidateOnStart();

builder.Services.AddOptions<WebAPIOptions>()
    .BindConfiguration(WebAPIOptions.Section)
    .ValidateOnStart();

builder.Services.AddOptions<CaptchaOptions>()
    .BindConfiguration(CaptchaOptions.Section)
    .ValidateOnStart();

// Database
builder.Services.AddDatabase(builder.Configuration);

// CAPTCHA
var captchaOptions = builder.Configuration.GetSection(CaptchaOptions.Section).Get<CaptchaOptions>();

if (captchaOptions is not null)
{
    ArgumentException.ThrowIfNullOrEmpty(captchaOptions.Secret);

    switch (captchaOptions.Provider)
    {
        case CaptchaProvider.Turnstile:
            builder.Services.AddSingleton<TurnstileService>();
            break;

        case CaptchaProvider.ReCaptcha:
            builder.Services.AddSingleton<ReCaptchaService>();
            break;

        default:
            throw new NotImplementedException($"Captcha provider not implemented. {captchaOptions.Provider}");
    }
}

builder.Logging.ClearProviders();

#if DEBUG

builder.Logging.SetMinimumLevel(LogLevel.Debug);

builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.All;
});

#endif

builder.Logging.AddNLog();

var app = builder.Build();

app.UseHttpLogging();

// Configure the HTTP request pipeline.

app.MapAuthEndpoints();
app.MapPortraitEndpoints();

app.Run();