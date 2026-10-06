namespace server.Core
{
    public readonly struct ValueStopwatch(long startTimestamp)
    {
        private static readonly double TimestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

        public bool IsActive => startTimestamp != 0;

        public static ValueStopwatch StartNew()
        {
            return new(Stopwatch.GetTimestamp());
        }

        public TimeSpan GetElapsedTime()
        {
            long end = Stopwatch.GetTimestamp();
            long timestampDelta = end - startTimestamp;
            long ticks = (long)(TimestampToTicks * timestampDelta);

            return new TimeSpan(ticks);
        }
    }
}