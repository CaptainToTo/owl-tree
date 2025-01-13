using System;
using System.Collections.Generic;
using System.Linq;

namespace OwlTree
{
    public class Bandwidth
    {

        private List<(int bytes, long time)> _outgoingRecords = new();
        private List<(int bytes, long time)> _incomingRecords = new();
        private int _max;
        private Action<Bandwidth> _report;

        public Bandwidth(Action<Bandwidth> report, int max = 30)
        {
            _max = max;
            _report = report;
        }

        public void RecordOutgoing(Packet packet)
        {
            _outgoingRecords.Add((packet.GetPacket().Length, DateTimeOffset.Now.ToUnixTimeMilliseconds()));
            if (_outgoingRecords.Count > _max)
                _outgoingRecords.RemoveAt(0);
            _report.Invoke(this);
        }

        public void RecordIncoming(Packet packet)
        {
            _incomingRecords.Add((packet.GetPacket().Length, DateTimeOffset.Now.ToUnixTimeMilliseconds()));
            if (_incomingRecords.Count > _max)
                _incomingRecords.RemoveAt(0);
            _report.Invoke(this);
        }

        public float OutgoingBytesPerSecond()
        {
            if (_outgoingRecords.Count < 3)
                return 0;
            int sum = 0;
            for (int i = 0; i < _outgoingRecords.Count; i++)
                sum += _outgoingRecords[i].bytes;
            return sum / ((DateTimeOffset.Now.ToUnixTimeMilliseconds() - _outgoingRecords[0].time) / 1000f);
        }

        public float OutgoingKbPerSecond()
        {
            return OutgoingBytesPerSecond() / 1000f;
        }

        public float IncomingBytesPerSecond()
        {
            if (_incomingRecords.Count < 3)
                return 0;
            int sum = 0;
            for (int i = 0; i < _incomingRecords.Count; i++)
                sum += _incomingRecords[i].bytes;
            return sum / ((DateTimeOffset.Now.ToUnixTimeMilliseconds() - _incomingRecords[0].time) / 1000f);
        }

        public float IncomingKbPerSecond()
        {
            return IncomingBytesPerSecond() / 1000f;
        }
    }
}