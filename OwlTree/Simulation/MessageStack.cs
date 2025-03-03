using System.Collections.Generic;

namespace OwlTree
{
    public class MessageStack
    {
        private LinkedList<IncomingMessage> _stack = new();

        private LinkedListNode<IncomingMessage>[] _tickStarts;
        private Tick _newestTick = new Tick(0);
        private Tick _oldestTick = new Tick(0);

        public LinkedListNode<IncomingMessage> GetTickStart(Tick t)
        {
            var ind = (_tickStarts.Length - 1) - (_newestTick - t);
            if (ind < 0 || _tickStarts.Length <= ind)
                return null;
            return _tickStarts[ind];
        }

        public MessageStack(int capacity)
        {
            _tickStarts = new LinkedListNode<IncomingMessage>[capacity];
        }

        public void Push(IncomingMessage m)
        {
            var node = _stack.AddLast(m);
            if (m.tick > _newestTick)
                AddTickStart(node);
        }

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

        public void RemoveRange(LinkedListNode<IncomingMessage> startInclusive, LinkedListNode<IncomingMessage> endExclusive)
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