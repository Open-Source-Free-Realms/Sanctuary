using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class ClientMetrics
{
    public ulong Time;
    public ulong Unknown;
    public ulong Index;
    public ulong GiveTime;
    public ulong ProcessGame;
    public ulong UpdateScene;
    public ulong RenderScene;
    public ulong ProcessPlayer;
    public ulong DynamicsSystem;
    public ulong BBE;
    public ulong Gui;
    public ulong Flip;

    public int RenderLevel;

    public ulong WindowWidth;
    public ulong WindowHeight;

    public ulong WorkingSetSize;

    public ulong ReliableAveragePing;

    public ulong IdleTimeout;
    public ulong Idle;

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out Unknown))
            return false;

        if (!reader.TryRead(out Time))
            return false;

        if (!reader.TryRead(out Index))
            return false;

        if (!reader.TryRead(out GiveTime))
            return false;

        if (!reader.TryRead(out ProcessGame))
            return false;

        if (!reader.TryRead(out UpdateScene))
            return false;

        if (!reader.TryRead(out RenderScene))
            return false;

        if (!reader.TryRead(out ProcessPlayer))
            return false;

        if (!reader.TryRead(out DynamicsSystem))
            return false;

        if (!reader.TryRead(out BBE))
            return false;

        if (!reader.TryRead(out Gui))
            return false;

        if (!reader.TryRead(out Flip))
            return false;

        if (!reader.TryRead(out RenderLevel))
            return false;

        if (!reader.TryRead(out WindowWidth))
            return false;

        if (!reader.TryRead(out WindowHeight))
            return false;

        if (!reader.TryRead(out WorkingSetSize))
            return false;

        if (!reader.TryRead(out ReliableAveragePing))
            return false;

        if (!reader.TryRead(out IdleTimeout))
            return false;

        if (!reader.TryRead(out Idle))
            return false;

        return true;
    }

    public override string ToString()
    {
        return $"{Time}\t{Unknown}\t{Index}\t{GiveTime}\t{ProcessGame}\t{UpdateScene}\t{RenderScene}\t{ProcessPlayer}\t{DynamicsSystem}\t{BBE}\t{Gui}\t{Flip}\t{RenderLevel}\t{WindowWidth}\t{WindowHeight}\t{WorkingSetSize}\t{ReliableAveragePing}\t{IdleTimeout}\t{Idle}";
    }
}