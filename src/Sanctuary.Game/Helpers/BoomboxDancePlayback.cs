using System;
using System.Linq;
using System.Collections.Generic;

using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Helpers;

public sealed class BoomboxDancePlayback(int[] sequence, int[] durationsMs, int blendMs = 0)
{
    private int _index;
    private long _nextClipAt;

    public long NextClipAt => _nextClipAt;
    public void Resume() => _nextClipAt = 0;

    public static BoomboxDancePlayback? CreateSynchronized(BoomboxDefinition definition) =>
        definition.SynchronizedDances && definition.DanceDurationsMs.Count > 0
            ? new BoomboxDancePlayback(definition.DanceSequence,
                Enumerable.Range(0, definition.DanceSequence.Length)
                    .Select(i => definition.DanceDurationsMs.Values.Max(d => d[i])).ToArray(), definition.DanceBlendMs)
            : null;

    public int Advance(long nowMs)
    {
        if (nowMs < _nextClipAt || sequence.Length == 0 || durationsMs.Length != sequence.Length)
            return 0;

        var animation = sequence[_index];
        var duration = durationsMs[_index];
        if (duration <= 0)
            return 0;

        _index = (_index + 1) % sequence.Length;
        _nextClipAt = nowMs + duration - Math.Clamp(blendMs, 0, duration - 1);
        return animation;
    }
}

public sealed class BoomboxDanceSelection
{
    private sealed class Turn
    {
        public ulong Owner;
        public long EndsAt;
        public readonly Dictionary<ulong, int> Plays = new();
    }
    private readonly Dictionary<ulong, Turn> _turns = new();

    public ulong Select(ulong player, long now, IEnumerable<(ulong Guid, int Priority)> sources)
    {
        var available = sources.ToArray();
        var priority = available.Select(source => source.Priority).DefaultIfEmpty().Max();
        var candidates = available.Where(source => source.Priority == priority)
            .Select(source => source.Guid).Distinct().Order().ToArray();
        if (candidates.Length == 0)
        {
            _turns.Remove(player);
            return 0;
        }
        if (!_turns.TryGetValue(player, out var turn))
            _turns[player] = turn = new Turn();
        foreach (var guid in turn.Plays.Keys.Where(guid => !available.Any(source => source.Guid == guid)).ToArray())
            turn.Plays.Remove(guid);

        if (candidates.Contains(turn.Owner) && now < turn.EndsAt)
            return turn.Owner;
        var minimum = candidates.Min(id => turn.Plays.GetValueOrDefault(id));
        var leastServed = candidates.Where(id => turn.Plays.GetValueOrDefault(id) == minimum).ToArray();
        var owner = leastServed.FirstOrDefault(id => id > turn.Owner);
        if (owner == 0) owner = leastServed[0];
        turn.Owner = owner;
        turn.EndsAt = long.MaxValue;
        return owner;
    }

    public void Remove(ulong player) => _turns.Remove(player);

    public bool Owns(ulong player, ulong owner) =>
        _turns.TryGetValue(player, out var turn) && turn.Owner == owner;

    public void Started(ulong player, ulong owner, long endsAt)
    {
        if (_turns.TryGetValue(player, out var turn) && turn.Owner == owner)
        {
            turn.EndsAt = endsAt;
            turn.Plays[owner] = turn.Plays.GetValueOrDefault(owner) + 1;
        }
    }

}
