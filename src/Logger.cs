using System.Threading;

namespace OwlTree
{
    /// <summary>
    /// Thread safe logger that filters which type of outputs get written based on the 
    /// selected verbosity. Provide a Printer function that the logger can use when trying to write.
    /// </summary>
    public class Logger
    {
        /// <summary>
        /// Function signature for what the logger will call to write.
        /// Provide in the constructor.
        /// </summary>
        public delegate void Printer(string text);
        
        /// <summary>
        /// The verbosity a logger will use.
        /// </summary>
        public enum LogRule
        {
            /// <summary>
            /// Will not output anything.
            /// </summary>
            None,
            /// <summary>
            /// Only output client connection, and object spawning/despawning events.
            /// </summary>
            Events,
            /// <summary>
            /// Output info about events, and message buffers.
            /// </summary>
            Verbose
        }

        /// <summary>
        /// Create a new logger, that will use the provided Printer for writing logs,
        /// and will only log output that passes the given verbosity.
        /// </summary>
        public Logger(Printer printer, LogRule rule)
        {
            _printer = printer;
            Rule = rule;

            switch (Rule)
            {
                case LogRule.Verbose: _writer = WriteVerbose; break;
                case LogRule.None: _writer = WriteNone; break;
                default:
                case LogRule.Events: _writer = WriteEvents; break;
            }
        }

        private Printer _printer;
        private Action<LogRule, string> _writer;

        /// <summary>
        /// The verbosity of this logger.
        /// </summary>
        public LogRule Rule { get; private set; }

        private Mutex _lock = new Mutex();

        /// <summary>
        /// Write a log. This is thread safe, and will block if another thread is currently using the same logger.
        /// </summary>
        public void Write(LogRule outputType, string text)
        {
            _writer.Invoke(outputType, text);
        }

        private void WriteNone(LogRule outputType, string text) { }

        private void WriteEvents(LogRule outputType, string text)
        {
            if ((int)outputType > (int)LogRule.Events) return;
            _lock.WaitOne();
            _printer.Invoke(text);
            _lock.ReleaseMutex();
        }

        private void WriteVerbose(LogRule outputType, string text)
        {
            if ((int)outputType > (int)LogRule.Verbose) return;
            _lock.WaitOne();
            _printer.Invoke(text);
            _lock.ReleaseMutex();
        }
    }
}