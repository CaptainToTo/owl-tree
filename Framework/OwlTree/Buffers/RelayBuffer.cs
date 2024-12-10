
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

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


        /// <summary>
        /// The maximum number of clients allowed to be connected at once in this session.
        /// </summary>
        public int MaxClients { get; private set; }

        // server state
        private Socket _tcpRelay;
        private Socket _udpRelay;
        private List<Socket> _readList = new();
        private ClientDataList _clientData = new();
        private List<IPEndPoint> _connectionRequests = new();

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