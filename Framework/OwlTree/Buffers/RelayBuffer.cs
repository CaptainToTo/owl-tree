
namespace OwlTree
{
    public class RelayBuffer : NetworkBuffer
    {
        public RelayBuffer(Args args) : base(args)
        {
        }

        public override void Disconnect()
        {
            throw new System.NotImplementedException();
        }

        public override void Disconnect(ClientId id)
        {
            throw new System.NotImplementedException();
        }

        public override void Read()
        {
            throw new System.NotImplementedException();
        }

        public override void Send()
        {
            throw new System.NotImplementedException();
        }
    }
}