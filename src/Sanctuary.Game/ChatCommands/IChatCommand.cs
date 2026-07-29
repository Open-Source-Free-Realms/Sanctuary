using Sanctuary.Game.Entities;

public enum ChatCommandRole
{
    Player = 0,
    Mod = 1,
    Admin = 2
}

public interface IChatCommandView
{
    string KeyWord { get; }
    string Usage { get; }
    string Description { get; }
    ChatCommandRole RequiredRole { get; }
}

public interface IChatCommand : IChatCommandView
{
    bool Handle(Player invoker, string[] args);
}