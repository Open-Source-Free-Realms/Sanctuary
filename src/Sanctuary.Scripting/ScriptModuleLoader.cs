using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Lua;

namespace Sanctuary.Scripting;

public sealed class ScriptModuleLoader : ILuaModuleLoader
{
    public static readonly string SharedDirectory = Path.Combine(ScriptManager.BaseDirectory, "Shared");

    public bool Exists(string moduleName) =>
        moduleName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) < 0 && File.Exists(GetPath(moduleName));

    public async ValueTask<LuaModule> LoadAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        var text = await File.ReadAllTextAsync(GetPath(moduleName), cancellationToken);

        return new LuaModule(moduleName, text);
    }

    private static string GetPath(string moduleName) => Path.Combine(SharedDirectory, moduleName + ".lua");
}
