using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace OwlTree
{
    /// <summary>
    /// Contains all state related to a client connection. Used by servers.
    /// </summary>
    internal struct ClientData
    {
        public ClientId id;
        public Packet tcpPacket;
        public Socket tpcSocket;
        public Packet udpPacket;
        public IPEndPoint udpEndPoint;

        /// <summary>
        /// Represents an empty client data instance.
        /// </summary>
        public static ClientData None = new ClientData() { id = ClientId.None };

        public static bool operator ==(ClientData a, ClientData b) => a.id == b.id;
        public static bool operator !=(ClientData a, ClientData b) => a.id != b.id;

        public override bool Equals(object obj)
        {
            return obj != null && obj.GetType() == typeof(ClientData) && ((ClientData)obj == this);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    /// <summary>
    /// Manages a list of ClientData.
    /// </summary>
    internal class ClientDataList : IEnumerable<ClientData>
    {
        private List<ClientData> _data = new();

        public int Count => _data.Count;

        public void Add(ClientData data) => _data.Add(data);

        public void Remove(ClientData data) => _data.Remove(data);

        public ClientData Find(Socket s)
        {
            foreach (var data in _data)
                if (data.tpcSocket == s) return data;
            return ClientData.None;
        }

        public ClientData Find(ClientId id)
        {
            foreach (var data in _data)
                if (data.id == id) return data;
            return ClientData.None;
        }

        public ClientData Find(IPEndPoint endPoint)
        {
            foreach (var data in _data)
                if (data.udpEndPoint.Address.Equals(endPoint.Address) && data.udpEndPoint.Port == endPoint.Port) return data;
            return ClientData.None;
        }

        public ClientId[] GetIds()
        {
            ClientId[] ids = new ClientId[_data.Count];
            for (int i = 0; i < _data.Count; i++)
                ids[i] = _data[i].id;
            return ids;
        }

        public IEnumerator<ClientData> GetEnumerator()
        {
            return _data.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}