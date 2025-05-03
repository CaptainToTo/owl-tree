using System;
using System.Security.Cryptography;


namespace OwlTree
{
    public class PacketSigner
    {
        private byte[] _localPrivateKey;
        private byte[] _localPublicKey;
        private byte[] _remotePublicKey;

        private RSA _rsa;

        public PacketSigner(int keySize = 64)
        {
            _rsa = RSA.Create();
            _rsa.KeySize = keySize;
            _localPublicKey = _rsa.ExportRSAPublicKey();
            _localPrivateKey = _rsa.ExportRSAPrivateKey();
        }

        private uint _hash;
        private uint _incrementor;

        public void Initialize(uint hash, uint incrementor, string remotePublicKey)
        {
            _hash = hash;
            _incrementor = incrementor;
            _remotePublicKey = Convert.FromBase64String(remotePublicKey);
        }

        public string GetPublicKey() => Convert.ToBase64String(_localPublicKey);

        public void SignPacket(Packet packet)
        {
            uint curHash = _hash + (_incrementor * packet.header.packetNum);

            _rsa.ImportRSAPrivateKey(_localPrivateKey, out _);
        }

        public void VerifyPacket(Packet packet)
        {

        }
    }
}