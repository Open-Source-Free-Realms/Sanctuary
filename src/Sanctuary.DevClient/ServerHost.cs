using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace Sanctuary.DevClient;

public sealed class ServerHost(string repositoryRoot)
{
    private const string DotnetCommand = "dotnet";
    private const string DatabaseProviderVariable = "Database__Provider";
    private const string DatabaseConnectionVariable = "Database__ConnectionString";
    private const string DotnetEnvironmentVariable = "DOTNET_ENVIRONMENT";
    private const string AspNetEnvironmentVariable = "ASPNETCORE_ENVIRONMENT";
    private const string DefaultDatabaseProvider = "Sqlite";
    private const string DefaultDatabaseFile = "sanctuary.db";
    private const string DevelopmentEnvironment = "Development";
    private const string LogFolderName = "logs";
    private const string SourceFolderName = "src";
    private const int LoginPort = 20042;
    private const int GatewayPort = 20260;
    private const int WebApiPort = 20040;

    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan ReadyPollDelay = TimeSpan.FromMilliseconds(250);

    private static readonly ServerProgram[] Programs =
    [
        new("Login", false, LoginPort, false),
        new("Gateway", false, GatewayPort, false),
        new("WebAPI", true, WebApiPort, true),
    ];

    private readonly object _stopLock = new();
    private readonly List<Process> _started = [];
    private readonly string _binaryFolder = GetBinaryFolder();

    public async Task<bool> StartAsync()
    {
        foreach (var program in Programs)
        {
            if (IsListening(program))
            {
                Console.WriteLine($"{program.Name} is already running.");
                continue;
            }

            if (!Build(program))
            {
                Console.Error.WriteLine($"{program.Name} failed to build.");
                return false;
            }

            var process = Start(program);

            lock (_stopLock)
                _started.Add(process);

            if (!await WaitUntilListeningAsync(program, process))
            {
                Console.Error.WriteLine($"{program.Name} did not start. See {GetLogPath(program)}");
                return false;
            }

            Console.WriteLine($"{program.Name} is ready.");
        }

        return true;
    }

    public static async Task<bool> WaitForRunningServersAsync()
    {
        foreach (var program in Programs)
        {
            Console.WriteLine($"Waiting for {program.Name}...");

            if (!await WaitUntilListeningAsync(program, null))
                return false;
        }

        return true;
    }

    public void Stop()
    {
        lock (_stopLock)
        {
            foreach (var process in _started.Where(process => !process.HasExited))
                process.Kill(true);

            _started.Clear();
        }
    }

    private static string GetBinaryFolder()
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory);

        return Path.Combine("bin", output.Parent!.Name, output.Name);
    }

    private string GetProjectFolder(ServerProgram program) =>
        Path.Combine(repositoryRoot, SourceFolderName, $"Sanctuary.{program.Name}");

    private string GetLogPath(ServerProgram program) =>
        Path.Combine(repositoryRoot, LogFolderName, $"{program.Name}.log");

    private bool Build(ServerProgram program)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var startInfo = new ProcessStartInfo(DotnetCommand) { WorkingDirectory = repositoryRoot, UseShellExecute = false };

        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(Path.Combine(GetProjectFolder(program), $"Sanctuary.{program.Name}.csproj"));
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(configuration);
        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("q");

        using var process = Process.Start(startInfo)!;

        process.WaitForExit();

        return process.ExitCode == 0;
    }

    private Process Start(ServerProgram program)
    {
        var projectFolder = GetProjectFolder(program);
        var startInfo = new ProcessStartInfo(DotnetCommand)
        {
            WorkingDirectory = program.RunsFromProjectFolder ? projectFolder : Path.Combine(repositoryRoot, SourceFolderName),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        startInfo.ArgumentList.Add(Path.Combine(projectFolder, _binaryFolder, $"Sanctuary.{program.Name}.dll"));
        startInfo.Environment.TryAdd(DatabaseProviderVariable, DefaultDatabaseProvider);
        startInfo.Environment.TryAdd(DatabaseConnectionVariable, $"Data Source={Path.Combine(repositoryRoot, DefaultDatabaseFile)}");
        startInfo.Environment.TryAdd(DotnetEnvironmentVariable, DevelopmentEnvironment);
        startInfo.Environment.TryAdd(AspNetEnvironmentVariable, DevelopmentEnvironment);

        var logPath = GetLogPath(program);

        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        var log = new StreamWriter(logPath) { AutoFlush = true };
        var logLock = new object();
        var process = new Process { StartInfo = startInfo };

        void WriteLine(object sender, DataReceivedEventArgs line)
        {
            if (line.Data is null)
                return;

            lock (logLock)
                log.WriteLine(line.Data);
        }

        process.OutputDataReceived += WriteLine;
        process.ErrorDataReceived += WriteLine;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    private static async Task<bool> WaitUntilListeningAsync(ServerProgram program, Process? process)
    {
        var deadline = DateTime.UtcNow + ReadyTimeout;

        while (DateTime.UtcNow < deadline)
        {
            if (IsListening(program))
                return true;

            if (process is { HasExited: true })
                return false;

            await Task.Delay(ReadyPollDelay);
        }

        return false;
    }

    private static bool IsListening(ServerProgram program)
    {
        var properties = IPGlobalProperties.GetIPGlobalProperties();
        var listeners = program.IsTcp ? properties.GetActiveTcpListeners() : properties.GetActiveUdpListeners();

        return listeners.Any(listener => listener.Port == program.Port);
    }

    private sealed record ServerProgram(string Name, bool IsTcp, int Port, bool RunsFromProjectFolder);
}
