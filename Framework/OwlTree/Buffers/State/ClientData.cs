using System;
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
        public uint hash;
        public Packet tcpPacket;
        public Socket tcpSocket;
        public Packet udpPacket;
        public IPEndPoint udpEndPoint;

        public IPAddress Address => udpEndPoint.Address;

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
    /// Manages a list of ClientData. Used by servers to keep track of all data associated 
    /// with each client at the socket level.
    /// </summary>
    internal class ClientDataList : IEnumerable<ClientData>
    {
        private List<ClientData> _data = new();

        private uint _curId = ClientId.FirstClientId;
        private ClientId NextClientId()
        {
            var id = new ClientId(_curId);
            _curId++;
            return id;
        }

        private Random _rand;
        private uint NextHash()
        {
            uint nextHash = 0;
            do {
                nextHash = (uint)_rand.Next();
            } while (Find(nextHash) != ClientData.None);
            return nextHash;
        }

        private int _bufferSize;

        public ClientDataList(int bufferSize, int hashSeed)
        {
            _bufferSize = bufferSize;
            _rand = new Random(hashSeed);
        }

        public int Count => _data.Count;

        /// <summary>
        /// Creates new client data, including packet buffers, client id, and hash.
        /// </summary>
        public ClientData Add(Socket tcpSocket, IPEndPoint udpEndPoint)
        {
            var data = new ClientData() {
                id = NextClientId(),
                hash = NextHash(),
                tcpPacket = new Packet(_bufferSize),
                tcpSocket = tcpSocket,
                udpPacket = new Packet(_bufferSize, true),
                udpEndPoint = udpEndPoint
            };
            _data.Add(data);
            return data;
        }

        /// <summary>
        /// Remove client data from this list. Use when a client disconnects.
        /// </summary>
        public void Remove(ClientData data) 
        {
            for (int i = 0; i < _data.Count; i++)
            {
                if (_data[i].id == data.id)
                {
                    _data.RemoveAt(i);
                    return;
                }
            }
        }

        public ClientData Find(Socket s)
        {
            foreach (var data in _data)
                if (data.tcpSocket == s) return data;
            return ClientData.None;
        }

        public ClientData Find(ClientId id)
        {
            foreach (var data in _data)
                if (data.id == id) return data;
            return ClientData.None;
        }

        public ClientData Find(uint hash)
        {
            foreach (var data in _data)
                if (data.hash == hash) return data;
            return ClientData.None;
        }

        public ClientData Find(IPEndPoint endPoint)
        {
            foreach (var data in _data)
                if (data.udpEndPoint.Address.Equals(endPoint.Address) && data.udpEndPoint.Port == endPoint.Port) return data;
            return ClientData.None;
        }

        /// <summary>
        /// Returns an array of client ids currently in the list.
        /// </summary>
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