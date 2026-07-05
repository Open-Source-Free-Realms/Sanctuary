using System;

namespace Sanctuary.Gateway.Services;

public interface IChatLogWriter
{
    void Write(string channel, string characterName, string message);
}