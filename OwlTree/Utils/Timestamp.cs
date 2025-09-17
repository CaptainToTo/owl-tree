using System;

namespace OwlTree
{
    /// <summary>
    /// Utility for getting the current timestamp.
    /// </summary>
    public static class Timestamp
    {
        /// <summary>
        /// The current Unix millisecond timestamp.
        /// </summary>
        public static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>
        /// Returns a <c>year-month-day hour:minute:second.millisecond</c> formatted string of the current time.
        /// </summary>
        public static string NowString => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffff");

        /// <summary>
        /// Gets the millisecond component of the current timestamp.
        /// </summary>
        public static int Millisecond => DateTimeOffset.UtcNow.Millisecond;

        /// <summary>
        /// Returns the number milliseconds that have past since the given timestamp.
        /// If the timestamp is in the future, returns 0.
        /// </summary>
        public static int MillisecondsSince(long timestamp) => (int)Math.Max(Now - timestamp, 0);
    }
}