using System;
using Priority_Queue;

namespace OwlTree
{
    public abstract class SimulationBuffer
    {
        protected SimplePriorityQueue<Message, uint> buffer = new();

        public Tick CurTick { get; internal set; } = new Tick(0);

        public void NextTick(ClientId source)
        {
            CurTick.Next();
            var tickMessage = new OutgoingMessage{
                caller = source,
                callee = ClientId.None,
                rpcId = new RpcId(RpcId.NextTickId),
                target = NetworkId.None,
                protocol = Protocol.Tcp,
                perms = RpcPerms.AnyToAll,
                bytes = new byte[TickMessageLength]
            };
            EncodeNextTick(tickMessage.bytes, source, CurTick);
            AddOutgoing(tickMessage);
        }

        public abstract void AddOutgoing(OutgoingMessage m);

        public abstract bool TryGetNextOutgoing(out OutgoingMessage m);

        public abstract void AddIncoming(IncomingMessage m);

        public abstract bool TryGetNextIncoming(out IncomingMessage m);

        // RPCs ================

        public static int TickMessageLength => RpcId.MaxByteLength + ClientId.MaxByteLength + Tick.MaxByteLength + 8;

        public static void EncodeNextTick(Span<byte> bytes, ClientId source, Tick nextTick)
        {
            new RpcId(RpcId.NextTickId).InsertBytes(bytes);
            source.InsertBytes(bytes.Slice(RpcId.MaxByteLength));
            nextTick.InsertBytes(bytes.Slice(RpcId.MaxByteLength + ClientId.MaxByteLength));
            BitConverter.TryWriteBytes(bytes.Slice(Tick.MaxByteLength + RpcId.MaxByteLength + ClientId.MaxByteLength), 
                DateTimeOffset.Now.ToUnixTimeMilliseconds());
        }

        public static void EncodeCurTick(Span<byte> bytes, ClientId source, Tick curTick)
        {
            new RpcId(RpcId.CurTickId).InsertBytes(bytes);
            source.InsertBytes(bytes.Slice(RpcId.MaxByteLength));
            curTick.InsertBytes(bytes.Slice(RpcId.MaxByteLength + ClientId.MaxByteLength));
            BitConverter.TryWriteBytes(bytes.Slice(Tick.MaxByteLength + RpcId.MaxByteLength + ClientId.MaxByteLength), 
                DateTimeOffset.Now.ToUnixTimeMilliseconds());
        }

        public static void EncodeEndTick(Span<byte> bytes, ClientId source, Tick prevTick)
        {
            new RpcId(RpcId.EndTickId).InsertBytes(bytes);
            source.InsertBytes(bytes.Slice(RpcId.MaxByteLength));
            prevTick.InsertBytes(bytes.Slice(RpcId.MaxByteLength + ClientId.MaxByteLength));
            BitConverter.TryWriteBytes(bytes.Slice(Tick.MaxByteLength + RpcId.MaxByteLength + ClientId.MaxByteLength), 
                DateTimeOffset.Now.ToUnixTimeMilliseconds());
        }

        public static bool TryDecodeTickMessage(ReadOnlySpan<byte> bytes, out RpcId rpc, out ClientId source, out Tick tick, out long timestamp)
        {
            rpc = new RpcId(bytes);
            switch(rpc.Id)
            {
                case RpcId.NextTickId:
                case RpcId.CurTickId:
                case RpcId.EndTickId:
                    source = new ClientId(bytes.Slice(rpc.ByteLength()));
                    tick = new Tick(bytes.Slice(rpc.ByteLength() + source.ByteLength()));
                    timestamp = BitConverter.ToInt64(bytes.Slice(rpc.ByteLength() + source.ByteLength() + tick.ByteLength()));
                    return true;

                default:
                    source = ClientId.None;
                    tick = new Tick(0);
                    timestamp = 0;
                    return false;
            }
        }
    }
}