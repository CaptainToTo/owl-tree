using System;
using System.Collections.Generic;
using System.Linq;

namespace OwlTree
{
    /// <summary>
    /// Records packet sizes to produce stats on bandwidth usage.
    /// </summary>
    public class Bandwidth
    {

        private List<(int bytes, long time)> _outgoingRecords = new();
        private List<(int bytes, long time)> _incomingRecords = new();
        private int _max;
        private Action<Bandwidth> _report;

        private int _totalSent = 0;
        private int _totalRecv = 0;
        private long _startTime = 0;

        /// <summary>
        /// Provide a callback for anytime a new packet report is made.
        /// </summary>
        public Bandwidth(Action<Bandwidth> report, int max = 30)
        {
            _max = max;
            _report = report;
            _startTime = Timestamp.Now;
        }

        /// <summary>
        /// Record the size of the given packet as part of outgoing data.
        /// </summary>
        public void RecordOutgoing(Packet packet)
        {
            var size = packet.GetPacket().Length;
            _outgoingRecords.Add((size, Timestamp.Now));
            _totalSent += size;
            if (_outgoingRecords.Count > _max)
                _outgoingRecords.RemoveAt(0);
            _report.Invoke(this);
        }

        /// <summary>
        /// The size and timestamp of the last outgoing packet recorded.
        /// </summary>
        public (int bytes, long time) LastOutgoing() => _outgoingRecords.Count == 0 ? (0, 0) : _outgoingRecords.Last();

        /// <summary>
        /// Record the size of the given packet as part of incoming data.
        /// </summary>
        public void RecordIncoming(Packet packet)
        {
            var size = packet.GetPacket().Length;
            _incomingRecords.Add((size, Timestamp.Now));
            _totalRecv += size;
            if (_incomingRecords.Count > _max)
                _incomingRecords.RemoveAt(0);
            _report.Invoke(this);
        }

        /// <summary>
        /// The size and timestamp of the last incoming packet recorded.
        /// </summary>
        public (int bytes, long time) LastIncoming() => _incomingRecords.Count == 0 ? (0, 0) : _incomingRecords.Last();

        public float OutgoingBytesPerSecond()
        {
            if (_outgoingRecords.Count < 3)
                return 0;
            int sum = 0;
            for (int i = 0; i < _outgoingRecords.Count; i++)
                sum += _outgoingRecords[i].bytes;
            return sum / ((Timestamp.Now - _outgoingRecords[0].time) / 1000f);
        }

        public float OutgoingKbPerSecond() => OutgoingBytesPerSecond() / 1000f;

        public float IncomingBytesPerSecond()
        {
            if (_incomingRecords.Count < 3)
                return 0;
            int sum = 0;
            for (int i = 0; i < _incomingRecords.Count; i++)
                sum += _incomingRecords[i].bytes;
            return sum / ((Timestamp.Now - _incomingRecords[0].time) / 1000f);
        }

        public float IncomingKbPerSecond() => IncomingBytesPerSecond() / 1000f;

        public float TotalMbSent() => _totalSent / 1000000f;

        public float OutgoingMbPerHour() => TotalMbSent() / ((Timestamp.Now - _startTime) / 3600000f);

        public float TotalMbRecv() => _totalRecv / 1000000f;

        public float IncomingMbPerHour() => TotalMbRecv() / ((Timestamp.Now - _startTime) / 3600000f);
    }
}