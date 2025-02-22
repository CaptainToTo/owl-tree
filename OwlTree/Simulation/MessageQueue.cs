namespace OwlTree
{
    public class MessageQueue : SimulationBuffer
    {
        public override void AddMessage(Message m)
        {
            buffer.Enqueue(m, 0);
        }

        public override bool GetNextMessage(out Message m)
        {
            return buffer.TryDequeue(out m);
        }
    }
}