using System;
using System.IO;

namespace Sanctuary.Gateway.Services;

public class ChatLogWriter : IChatLogWriter
{
    private static readonly object _lock = new();

    public void Write(string channel, string characterName, string message)
    {
        try
        {
            var utcNow = DateTime.UtcNow;

            var folderPath = Path.Combine(AppContext.BaseDirectory, "Logs", "Chat");
            Directory.CreateDirectory(folderPath);

            var filePath = Path.Combine(folderPath, $"{utcNow:yyyy-MM-dd}.txt");

            var safeCharacterName = characterName ?? "Unknown";
            var safeMessage = message ?? string.Empty;
            safeMessage = safeMessage.Replace("\r", "\\r").Replace("\n", "\\n");

            var line = $"({utcNow:yyyy-MM-dd HH:mm:ss} UTC) | {channel} | From {safeCharacterName}: \"{safeMessage}\"";

            lock (_lock)
            {
                File.AppendAllText(filePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Never let chat logging crash the server.
        }
    }
}