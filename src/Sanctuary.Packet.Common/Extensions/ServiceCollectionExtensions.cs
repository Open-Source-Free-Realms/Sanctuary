using System;
using System.Linq;
using System.Reflection;

using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Packet.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static void ConfigurePacketHandlers(this IServiceProvider serviceProvider)
    {
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in loadedAssemblies.Where(x => !string.IsNullOrEmpty(x.FullName) && x.FullName.StartsWith(nameof(Sanctuary))))
        {
            foreach (var assemblyType in assembly.GetTypes())
            {
                if (assemblyType.GetCustomAttribute<PacketHandler>() is null)
                    continue;

                assemblyType.GetMethod(PacketHandler.ConfigureMethodName)?.Invoke(null, [serviceProvider]);
            }
        }
    }
}