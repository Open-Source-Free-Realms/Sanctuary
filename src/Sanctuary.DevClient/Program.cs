using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sanctuary.DevClient;

public static class Program
{
    private const string LocalAddress = "localhost";
    private const string DefaultAccount = "test";
    private const string SolutionPath = "src/Sanctuary.slnx";
    private const string ClientPathVariable = "SANCTUARY_CLIENT_PATH";
    private const string AddressOption = "-a";
    private const string GatewayOption = "-g";
    private const string LoginOption = "-l";
    private const string ClientCountOption = "-n";
    private const string ClientPathOption = "--client";
    private const string NoServersOption = "--no-servers";
    private const int FirstNumberedClient = 2;

    private static readonly TimeSpan ClientStartDelay = TimeSpan.FromSeconds(3);

    public static async Task<int> Main(string[] args)
    {
        var options = ParseOptions(args);

        if (options is null)
        {
            Console.Error.WriteLine("Usage: Sanctuary.DevClient [-a address[:port]] [-l account | -g characterId] [-n clients] [--client path] [--no-servers]");
            return 1;
        }

        var repositoryRoot = FindRepositoryRoot();
        var servers = new ServerHost(repositoryRoot);

        AppDomain.CurrentDomain.ProcessExit += (_, _) => servers.Stop();
        Console.CancelKeyPress += (_, _) => servers.Stop();

        var clientFolders = await ClientInstaller.GetClientFoldersAsync(repositoryRoot, options.ClientPath, options.ClientCount);
        var addressParts = options.Address.Split(':');
        var address = addressParts[0];
        int? port = addressParts.Length == 2 ? int.Parse(addressParts[1]) : null;

        if (address == LocalAddress)
        {
            var ready = options.StartServers ? await servers.StartAsync() : await ServerHost.WaitForRunningServersAsync();

            if (!ready)
            {
                servers.Stop();
                return 1;
            }
        }

        var clients = await StartClientsAsync(options, clientFolders, address, port);

        await Task.WhenAll(clients.Select(client => client.WaitForExitAsync()));

        servers.Stop();

        return clients.Count == options.ClientCount ? 0 : 1;
    }

    private static async Task<List<Process>> StartClientsAsync(Options options, IReadOnlyList<string> clientFolders, string address, int? port)
    {
        var clients = new List<Process>();

        for (var index = 0; index < clientFolders.Count; index++)
        {
            if (index > 0)
                await Task.Delay(ClientStartDelay);

            var client = options.GatewayCharacterId is { } characterId
                ? GameClient.StartInWorld(clientFolders[index], address, port, characterId)
                : await GameClient.StartAtLoginAsync(clientFolders[index], address, port, GetAccountName(options.Account, index));

            if (client is not null)
                clients.Add(client);
        }

        return clients;
    }

    private static string GetAccountName(string account, int index) =>
        index == 0 ? account : $"{account}{index + FirstNumberedClient - 1}";

    private static Options? ParseOptions(string[] args)
    {
        var options = new Options(LocalAddress, DefaultAccount, null, 1, Environment.GetEnvironmentVariable(ClientPathVariable), true);

        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] == NoServersOption)
            {
                options = options with { StartServers = false };
                continue;
            }

            if (++index >= args.Length)
                return null;

            var value = args[index];

            switch (args[index - 1])
            {
                case AddressOption:
                    options = options with { Address = value };
                    break;
                case LoginOption:
                    options = options with { Account = value };
                    break;
                case ClientPathOption:
                    options = options with { ClientPath = value };
                    break;
                case GatewayOption when ulong.TryParse(value, out var characterId):
                    options = options with { GatewayCharacterId = characterId };
                    break;
                case ClientCountOption when int.TryParse(value, out var clientCount) && clientCount > 0:
                    options = options with { ClientCount = clientCount };
                    break;
                default:
                    return null;
            }
        }

        return options.GatewayCharacterId is not null && options.ClientCount > 1 ? null : options;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionPath)))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException($"Could not find {SolutionPath} above {AppContext.BaseDirectory}");
    }

    private sealed record Options(string Address, string Account, ulong? GatewayCharacterId, int ClientCount, string? ClientPath, bool StartServers);
}
