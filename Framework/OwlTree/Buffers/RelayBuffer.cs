
using System.Net;

namespace OwlTree
{
    /// <summary>
    /// Manages passing packets between clients in a peer-to-peer session.
    /// </summary>
    public class RelayBuffer : NetworkBuffer
    {
        public RelayBuffer(Args args, int maxClients, IPAddress hostIp) : base(args)
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