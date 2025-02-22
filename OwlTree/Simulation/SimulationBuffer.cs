using System;
using Priority_Queue;

namespace OwlTree
{
    public abstract class SimulationBuffer
    {
        protected SimplePriorityQueue<Message, uint> buffer = new();

        public Tick CurTick { get; internal set; } = new Tick(0);

        public void NextTick() => CurTick.Next();

        public abstract void AddMessage(Message m);

        public abstract bool GetNextMessage(out Message m);

        // RPCs ================

        public static int TickMessageLength => 16;

        public static void EncodeNextTick(Span<byte> bytes, Tick nextTick)
        {
            new RpcId(RpcId.NextTickId).InsertBytes(bytes);
            nextTick.InsertBytes(bytes.Slice(RpcId.MaxByteLength));
            BitConverter.TryWriteBytes(bytes.Slice(Tick.MaxByteLength + RpcId.MaxByteLength), DateTimeOffset.Now.ToUnixTimeMilliseconds());
        }

        public static void EncodeCurTick(Span<byte> bytes, Tick curTick)
        {
            new RpcId(RpcId.CurTickId).InsertBytes(bytes);
            curTick.InsertBytes(bytes.Slice(RpcId.MaxByteLength));
            BitConverter.TryWriteBytes(bytes.Slice(Tick.MaxByteLength + RpcId.MaxByteLength), DateTimeOffset.Now.ToUnixTimeMilliseconds());
        }

        public static void EncodeEndTick(Span<byte> bytes, Tick prevTick)
        {
            new RpcId(RpcId.EndTickId).InsertBytes(bytes);
            prevTick.InsertBytes(bytes.Slice(RpcId.MaxByteLength));
            BitConverter.TryWriteBytes(bytes.Slice(Tick.MaxByteLength + RpcId.MaxByteLength), DateTimeOffset.Now.ToUnixTimeMilliseconds());
        }

        public static bool TryDecodeTickMessage(ReadOnlySpan<byte> bytes, out RpcId rpc, out Tick tick, out long timestamp)
        {
            rpc = new RpcId(bytes);
            switch(rpc.Id)
            {
                case RpcId.NextTickId:
                case RpcId.CurTickId:
                case RpcId.EndTickId:
                    tick = new Tick(bytes.Slice(rpc.ByteLength()));
                    timestamp = BitConverter.ToInt64(bytes.Slice(rpc.ByteLength() + tick.ByteLength()));
                    return true;

                default:
                    tick = new Tick(0);
                    timestamp = 0;
                    return false;
            }
        }
    }
}