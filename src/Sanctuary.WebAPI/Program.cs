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
builder.Services.AddOptionsWithValidateOnStart<DatabaseOptions>()
    .BindConfiguration(DatabaseOptions.Section);

builder.Services.AddOptionsWithValidateOnStart<WebAPIOptions>()
    .BindConfiguration(WebAPIOptions.Section);

// Database
builder.Services.AddDatabase(builder.Configuration);

// CAPTCHA
var captchaOptionsSection = builder.Configuration.GetSection(CaptchaOptions.Section);

if (captchaOptionsSection.Exists())
{
    var captchaOptions = captchaOptionsSection.Get<CaptchaOptions>();

    ArgumentNullException.ThrowIfNull(captchaOptions);

    if (!captchaOptions.IsConfigured)
        throw new InvalidOperationException("Invalid captcha options.");

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

    builder.Services.Configure<CaptchaOptions>(captchaOptionsSection);
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