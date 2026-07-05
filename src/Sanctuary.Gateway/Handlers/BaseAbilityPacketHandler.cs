using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseAbilityPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseAbilityPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        if (!reader.TryRead(out short opCode))
        {
            _logger.LogError("Failed to read opcode from packet. ( Data: {data} )", Convert.ToHexString(reader.Span));
            return false;
        }

        var handled = opCode switch
        {
            AbilityPacketClientRequestStartAbility.OpCode => AbilityPacketClientRequestStartAbilityHandler.HandlePacket(connection, reader.Span),
            AbilityPacketRequestAbilityDefinition.OpCode => AbilityPacketRequestAbilityDefinitionHandler.HandlePacket(connection, reader.Span),
            _ => false
        };

        if (!handled)
        {
            _logger.LogInformation(
                "{connection} received unhandled ability packet. ( SubOpCode: {subOpCode}, Length: {length}, Data: {data} )",
                connection,
                opCode,
                reader.Span.Length,
                Convert.ToHexString(reader.Span));
        }

        return handled;
    }
}
