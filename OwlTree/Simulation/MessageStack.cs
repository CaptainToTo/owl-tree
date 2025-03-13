using System.Collections.Generic;

namespace OwlTree
{
    /// <summary>
    /// Groups a stack of incoming messages by tick.
    /// Used for rollback simulation.
    /// </summary>
    public class MessageStack
    {
        private LinkedList<IncomingMessage> _stack = new();

        private LinkedListNode<IncomingMessage>[] _tickStarts;
        private Tick _newestTick = new Tick(0);
        private Tick _oldestTick = new Tick(0);

        /// <summary>
        /// Gets the first incoming message of the given tick. Returns null if the tick doesn't exist.
        /// </summary>
        public LinkedListNode<IncomingMessage> GetTickStart(Tick t)
        {
            var ind = (_tickStarts.Length - 1) - (_newestTick - t);
            if (ind < 0 || _tickStarts.Length <= ind)
                return null;
            return _tickStarts[ind];
        }

        /// <summary>
        /// Make a new message stack that can contain at most capacity ticks. There can be any number of messages per tick.
        /// </summary>
        public MessageStack(int capacity)
        {
            _tickStarts = new LinkedListNode<IncomingMessage>[capacity];
        }

        /// <summary>
        /// Add a new message to the stack. If the message belongs to a new tick, and the stack is at capacity,
        /// then the oldest tick and all of its messages will be removed from the bottom of the stack.
        /// </summary>
        public void Push(IncomingMessage m)
        {
            var node = _stack.AddLast(m);
            if (m.tick > _newestTick)
                AddTickStart(node);
        }

        // removes the old tick, and add the new message as the start of the new tick
        private void AddTickStart(LinkedListNode<IncomingMessage> node)
        {
            RemoveRange(_tickStarts[0], _tickStarts[1]);
            for (int i = _tickStarts.Length - 1; i > 0; i--)
                _tickStarts[i - 1] = _tickStarts[i];
            _tickStarts[_tickStarts.Length - 1] = node;
            if (_newestTick == 0)
                _oldestTick = node.Value.tick;
            else
                _oldestTick = _oldestTick.Next();
            _newestTick = node.Value.tick;
        }

        private void RemoveRange(LinkedListNode<IncomingMessage> startInclusive, LinkedListNode<IncomingMessage> endExclusive)
        {
            LinkedListNode<IncomingMessage> current = startInclusive;
            LinkedListNode<IncomingMessage> next;

            while (current != null && current != endExclusive)
            {
                next = current.Next;
                _stack.Remove(current);
                current = next;
            }
        }

        /// <summary>
        /// Gets an iterable of all messages starting from the given tick to the present.
        /// Pops those messages from the stack. Used for rewinding during resimulation.
        /// </summary>
        public IEnumerable<IncomingMessage> RewindFrom(Tick t)
        {
            var node = GetTickStart(t);
            if (node is null)
                yield break;
            
            LinkedListNode<IncomingMessage> next;
            
            while (node != null)
            {
                next = node.Next;
                yield return node.Value;
                _stack.Remove(node);
                node = next;
            }
        }
    }
}