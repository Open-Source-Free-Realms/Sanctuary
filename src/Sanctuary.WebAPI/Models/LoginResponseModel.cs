using System.Text.Json.Serialization;

namespace Sanctuary.WebAPI.Models;

public record class LoginResponseModel
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; set; }

    [JsonPropertyName("launchArguments")]
    public string? LaunchArguments { get; set; }
}