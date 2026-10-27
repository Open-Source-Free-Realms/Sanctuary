using System.Threading.Tasks;

using Lua;

namespace Sanctuary.Scripting;

internal sealed class PlayerUserData(IScriptablePlayer player) : ILuaUserData
{
    private IScriptablePlayer _player => player;

    public LuaTable? Metatable { get; set; } = SharedMetatable;

    public IScriptablePlayer Player => _player;

    #region API

    private const int MaxGiveQuantity = 999;

    private static readonly LuaFunction MessageFunction = new("message", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<PlayerUserData>(0);
        var message = context.GetArgument<string>(1);

        self._player.Message(message);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction GiveItemFunction = new("giveItem", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<PlayerUserData>(0);
        var itemDefinitionId = context.GetArgument<int>(1);
        var quantity = 1;

        if (context.ArgumentCount > 2)
            quantity = context.GetArgument<int>(2);

        if (quantity is < 1 or > MaxGiveQuantity)
            throw new LuaRuntimeException(context.State, $"giveItem quantity must be between 1 and {MaxGiveQuantity}.");

        self._player.GiveItem(itemDefinitionId, quantity);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction AcceptQuestFunction = new("acceptQuest", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<PlayerUserData>(0);
        var questId = context.GetArgument<int>(1);

        self._player.AcceptQuest(questId);

        return new ValueTask<int>(context.Return());
    });

    private static readonly LuaFunction HasCompletedQuestFunction = new("hasCompletedQuest", static (context, cancellationToken) =>
    {
        var self = context.GetArgument<PlayerUserData>(0);
        var questId = context.GetArgument<int>(1);

        return new ValueTask<int>(context.Return(self._player.HasCompletedQuest(questId)));
    });

    #endregion

    private static readonly LuaTable SharedMetatable = BuildMetatable();

    private static LuaTable BuildMetatable()
    {
        var metatable = new LuaTable();

        metatable["__index"] = new LuaFunction("__index", static (context, cancellationToken) =>
        {
            var self = context.GetArgument<PlayerUserData>(0);
            var key = context.GetArgument<string>(1);

            var result = key switch
            {
                "guid" => new LuaValue(self._player.Guid),
                "name" => new LuaValue(self._player.Name),
                "position" => new LuaValue(new LuaTable
                {
                    ["x"] = self._player.Position.x,
                    ["y"] = self._player.Position.y,
                    ["z"] = self._player.Position.z
                }),
                //
                "message" => MessageFunction,
                "giveItem" => GiveItemFunction,
                "acceptQuest" => AcceptQuestFunction,
                "hasCompletedQuest" => HasCompletedQuestFunction,
                _ => LuaValue.Nil
            };

            return new ValueTask<int>(context.Return(result));
        });

        return metatable;
    }
}
