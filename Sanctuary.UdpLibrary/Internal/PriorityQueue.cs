using System.Numerics;

namespace Sanctuary.UdpLibrary.Internal;

/// <remarks>
/// Classes that wish to be members of the priority queue below must derive themselves from this class
/// in order to pull in the two member variables.  Unlike normal priority queue classes which don't
/// have this restriction, it is required in order to support Remove and reprioritize functionality
/// in a timely manner (otherwise, in order to remove an entry that is not at the top, you would have
/// to linearly scan the entire queue).  This is accomplished by having the member itself contain
/// a pointer to it's position in the queue (<see cref="PriorityQueuePosition"/>).
/// Note: A restriction of this ability is that an object cannot participate in more than one priority
/// queue at a time.
/// </remarks>
public class PriorityQueueMember
{
    /// <summary>
    /// -1 = not in queue
    /// </summary>
    internal int PriorityQueuePosition = -1;
}

/// <summary>
/// This class provides a priority queue that is capable of reprioritizing/removing entries.
/// The compiler will ensure that objects stored in this class are derived from <see cref="PriorityQueueMember"/>.
/// We don't really need this to be a template class, since we could just treat everything as a
/// <see cref="PriorityQueueMember"/>, but then the application would lose some type checking.
/// We don't use references
/// in the api as most priority queue templates do as we can't support non pointer types anyways.
/// </summary>
/// <typeparam name="T">Entry type</typeparam>
/// <typeparam name="P">Priority type</typeparam>
internal class PriorityQueue<T, P> where T : PriorityQueueMember where P : INumber<P>
{
    private struct QueueEntry
    {
        public T entry;
        public P priority;
    }

    private QueueEntry[] Queue;
    private int QueueSize;
    private int QueueEnd;

    public PriorityQueue(int queueSize)
    {
        QueueEnd = 0;
        QueueSize = queueSize;
        Queue = new QueueEntry[QueueSize];
    }

    public T? Top()
    {
        return QueueEnd == 0 ? null : Queue[0].entry;
    }

    public T? TopRemove()
    {
        if (QueueEnd == 0)
            return null;

        var top = Queue[0].entry;

        Remove(top);

        return top;
    }

    public T? TopRemove(P priority)
    {
        return QueueEnd > 0 && Queue[0].priority <= priority ? Remove(Queue[0].entry) : null;
    }

    public T? Add(T entry, P priority)
    {
        if (entry.PriorityQueuePosition == -1)
        {
            // Not in queue, so add it to the bottom.
            if (QueueEnd >= QueueSize)
                return null;

            Queue[QueueEnd].entry = entry;
            Queue[QueueEnd].priority = priority;
            Queue[QueueEnd].entry.PriorityQueuePosition = QueueEnd;

            QueueEnd++;
        }
        else
        {
            // See if priority has actually changed, if not, just return, otherwise change priority and fall through to refloat it.
            if (Queue[entry.PriorityQueuePosition].priority == priority)
                return entry;

            Queue[entry.PriorityQueuePosition].priority = priority;
        }

        Refloat(entry);

        return entry;
    }

    public T Remove(T entry)
    {
        if (entry.PriorityQueuePosition == -1)
            return entry;

        // Move end entry into place of one being removed.
        QueueEnd--;

        var spot = entry.PriorityQueuePosition;

        // Don't remove last item in queue (bottom of tree), so no need to copy bottom
        // one up and refloat it (we would be refloating our own removed entry).
        if (spot != QueueEnd)
        {
            Queue[spot] = Queue[QueueEnd];
            Queue[spot].entry.PriorityQueuePosition = spot;

            Refloat(Queue[spot].entry);
        }

        entry.PriorityQueuePosition = -1;

        return entry;
    }

    public P? GetPriority(T entry)
    {
        return entry.PriorityQueuePosition >= 0 ? Queue[entry.PriorityQueuePosition].priority : default;
    }

    public int QueueUsed()
    {
        return QueueEnd;
    }

    private void Refloat(T entry)
    {
        // Float upward.
        var spot = entry.PriorityQueuePosition;

        var tryDown = true;

        while (spot > 0 && Queue[spot].priority < Queue[(spot - 1) / 2].priority)
        {
            var newSpot = (spot - 1) / 2;

            var hold = Queue[spot];

            Queue[spot] = Queue[newSpot];
            Queue[spot].entry.PriorityQueuePosition = spot;

            Queue[newSpot] = hold;
            Queue[newSpot].entry.PriorityQueuePosition = newSpot;

            spot = newSpot;

            tryDown = false;
        }

        if (tryDown)
        {
            // If we didn't manage to float up at all, then we need to try floating down.
            while (true)
            {
                // Pick smallest child.
                var downSpot1 = spot * 2 + 1;

                if (downSpot1 >= QueueEnd)
                    break;

                var downSpot2 = spot * 2 + 2;

                if (downSpot2 >= QueueEnd || Queue[downSpot1].priority < Queue[downSpot2].priority)
                {
                    if (Queue[downSpot1].priority >= Queue[spot].priority)
                        break;

                    var hold = Queue[spot];

                    Queue[spot] = Queue[downSpot1];
                    Queue[spot].entry.PriorityQueuePosition = spot;

                    Queue[downSpot1] = hold;
                    Queue[downSpot1].entry.PriorityQueuePosition = downSpot1;

                    spot = downSpot1;
                }
                else
                {
                    if (Queue[downSpot2].priority >= Queue[spot].priority)
                        break;

                    var hold = Queue[spot];

                    Queue[spot] = Queue[downSpot2];
                    Queue[spot].entry.PriorityQueuePosition = spot;

                    Queue[downSpot2] = hold;
                    Queue[downSpot2].entry.PriorityQueuePosition = downSpot2;

                    spot = downSpot2;
                }
            }
        }
    }
}