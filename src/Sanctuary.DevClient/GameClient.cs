using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Sanctuary.DevClient;

public static class GameClient
{
    private const string DefaultPassword = "testtest";
    private const string ClientExecutable = "FreeRealms.exe";
    private const string WindowsCompatibilityLayer = "wine";
    private const string LocaleArgument = "Internationalization:Locale=8";
    private const int LoginPort = 20042;
    private const int GatewayPort = 20260;
    private const int WebApiPort = 20040;
    private const int CharacterGuidShift = 4;
    private const int PlayerGuidNibble = 1;

    private static readonly string[] DefaultLaunchArguments =
    [
        "AssetDelivery:IndirectServerAddress=http://osfr.editz.dev/assets",
        "Portrait:UploadUrl=http://127.0.0.1:20040/image",
    ];

    private static readonly TimeSpan WebApiWaitTime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan WebApiRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan WebApiRequestTimeout = TimeSpan.FromSeconds(5);

    public static Process StartInWorld(string clientFolder, string address, int? port, ulong characterId)
    {
        var characterGuid = (characterId << CharacterGuidShift) + PlayerGuidNibble;

        return Start(clientFolder, address, port ?? GatewayPort, [$"GUID={characterGuid}", $"Ticket={Guid.NewGuid():N}", .. DefaultLaunchArguments]);
    }

    public static async Task<Process?> StartAtLoginAsync(string clientFolder, string address, int? port, string account)
    {
        var session = await GetSessionAsync(address, account);

        if (session is null)
        {
            Console.Error.WriteLine($"Could not log {account} in through the WebAPI on {address}:{WebApiPort}.");
            return null;
        }

        var launchArguments = session.LaunchArguments?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? DefaultLaunchArguments;

        return Start(clientFolder, address, port ?? LoginPort, [$"SessionId={session.SessionId}", .. launchArguments]);
    }

    private static async Task<SessionResponse?> GetSessionAsync(string address, string account)
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://{address}:{WebApiPort}"),
            Timeout = WebApiRequestTimeout,
        };

        var credentials = new Credentials(account, DefaultPassword);
        var deadline = DateTime.UtcNow + WebApiWaitTime;
        var registered = false;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var loginResponse = await httpClient.PostAsJsonAsync("/login", credentials);

                if (loginResponse.StatusCode == HttpStatusCode.OK)
                    return await loginResponse.Content.ReadFromJsonAsync<SessionResponse>();

                if ((int)loginResponse.StatusCode < (int)HttpStatusCode.InternalServerError && !registered)
                {
                    using var registerResponse = await httpClient.PostAsJsonAsync("/register", credentials);

                    registered = registerResponse.IsSuccessStatusCode;
                    continue;
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
            }

            await Task.Delay(WebApiRetryDelay);
        }

        return null;
    }

    private static Process Start(string clientFolder, string address, int port, IEnumerable<string> arguments)
    {
        var executable = Path.Combine(clientFolder, ClientExecutable);
        var startInfo = new ProcessStartInfo { WorkingDirectory = clientFolder, UseShellExecute = false };

        if (OperatingSystem.IsWindows())
        {
            startInfo.FileName = executable;
        }
        else
        {
            startInfo.FileName = WindowsCompatibilityLayer;
            startInfo.ArgumentList.Add(executable);
        }

        startInfo.ArgumentList.Add(LocaleArgument);
        startInfo.ArgumentList.Add($"Server={address}:{port}");

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        Console.WriteLine($"Starting client in {clientFolder}");

        return Process.Start(startInfo)!;
    }

    private sealed record Credentials(string Username, string Password);

    private sealed record SessionResponse(string SessionId, string? LaunchArguments);
}
