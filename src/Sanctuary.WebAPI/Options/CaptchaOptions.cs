using System;

namespace Sanctuary.WebAPI.Options;

public class CaptchaOptions
{
    public const string Section = "Captcha";

    public required string Secret { get; set; }
    public required CaptchaProvider Provider { get; set; }

    public bool IsConfigured => !string.IsNullOrEmpty(Secret) && Enum.IsDefined(Provider);
}