
using System;

namespace OwlTree
{
    /// <summary>
    /// Tracks the progress of a ping request. Similar to a promise.
    /// </summary>
    public class PingRequest : IEncodable
    {
        /// <summary>
        /// General purpose PingRequest delegate.
        /// </summary>
        public delegate void Delegate(PingRequest ping);

        /// <summary>
        /// The client who originally sent the ping.
        /// </summary>
        public ClientId Source { get; private set; }

        /// <summary>
        /// The client who is targeted by the ping.
        /// </summary>
        public ClientId Target { get; private set; }

        /// <summary>
        /// The millisecond timestamp the ping was sent at.
        /// </summary>
        public long SendTime { get; private set; } = 0;

        /// <summary>
        /// The millisecond timestamp the ping was received at by the target.
        /// </summary>
        public long ReceiveTime { get; private set; } = 0;

        /// <summary>
        /// True if the target has received the ping request.
        /// </summary>
        public bool Received => ReceiveTime != 0;

        /// <summary>
        /// The millisecond timestamp the ping returned back to the source client.
        /// </summary>
        public long ResponseTime { get; private set; } = 0;

        /// <summary>
        /// The millisecond time for a round trip from the source to the target, and back to the source.
        /// </summary>
        public int Ping => Resolved ? (int)(ResponseTime - SendTime) : 0;

        /// <summary>
        /// Whether or not the ping request has resolved. This may be in failure.
        /// </summary>
        public bool Resolved { get; private set; } = false;

        /// <summary>
        /// Whether or not the ping request has expired, and failed.
        /// </summary>
        public bool Failed { get; private set; } = false;

        public override string ToString()
        {
            var resolution = "sent at " + SendTime.ToString();
            if (Resolved && Failed)
                resolution = "failed";
            else if (Resolved && !Failed)
                resolution = Ping + " ms";
            return $"<PingRequest by {Source} to {Target}: {resolution}>";
        }

        /// <summary>
        /// Invoked on the source connection when the ping request has resolved, whether that's successfully or in failure.
        /// </summary>
        public event Delegate OnResolved;

        internal PingRequest()
        {
            Source = ClientId.None;
            Target = ClientId.None;
        }

        internal PingRequest(ReadOnlySpan<byte> bytes)
        {
            FromBytes(bytes);
        }
        
        /// <summary>
        /// Start a ping request.
        /// </summary>
        public PingRequest(ClientId source, ClientId target)
        {
            Source = source;
            Target = target;
            SendTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Called by target to signify that they have received the ping request.
        /// </summary>
        internal void PingReceived()
        {
            ReceiveTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Called by source connection to complete the ping request.
        /// </summary>
        internal void PingResponded()
        {
            ResponseTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Called by source connection if the ping expires.
        /// </summary>
        internal void PingFailed()
        {
            Failed = true;
        }

        /// <summary>
        /// Called by source connection to notify that the ping request has been resolved.
        /// </summary>
        internal void PingResolved()
        {
            Resolved = true;
            OnResolved?.Invoke(this);
        }

        public int ByteLength()
        {
            return Source.ByteLength() + Target.ByteLength() + 8 + 8 + 8;
        }

        public static int MaxLength()
        {
            return ClientId.MaxLength() + ClientId.MaxLength() + 8 + 8 + 8;
        }

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            int ind = 0;
            Source = new ClientId(bytes);
            ind += Source.ByteLength();
            Target = new ClientId(bytes.Slice(ind));
            ind += Target.ByteLength();
            SendTime = BitConverter.ToInt64(bytes.Slice(ind));
            ind += 8;
            ReceiveTime = BitConverter.ToInt64(bytes.Slice(ind));
            ind += 8;
            ResponseTime = BitConverter.ToInt64(bytes.Slice(ind));
        }

        public void InsertBytes(Span<byte> bytes)
        {
            int ind = 0;
            Source.InsertBytes(bytes);
            ind += Source.ByteLength();
            Target.InsertBytes(bytes.Slice(ind));
            ind += Target.ByteLength();
            BitConverter.TryWriteBytes(bytes.Slice(ind), SendTime);
            ind += 8;
            BitConverter.TryWriteBytes(bytes.Slice(ind), ReceiveTime);
            ind += 8;
            BitConverter.TryWriteBytes(bytes.Slice(ind), ResponseTime);
        }
    }
}