
namespace Program
{
    /// <summary>
    /// Thread-safe latest value mailbox.
    /// Single-writer friendly, multi-reader friendly.
    /// </summary>
    public sealed class LatestValue<T>
    {
        private int _stamp;
        private T _value = default!;

        public void Publish(T value)
        {
            // Seqlock-style write: odd stamp indicates write in progress.
            Interlocked.Increment(ref _stamp);
            _value = value;
            Interlocked.Increment(ref _stamp);
        }

        public T? TryRead()
        {
            while (true)
            {
                int before = Volatile.Read(ref _stamp);
                if (before == 0)
                    return default;

                if ((before & 1) != 0)
                {
                    Thread.Yield();
                    continue;
                }

                T snapshot = _value;
                int after = Volatile.Read(ref _stamp);
                if (before == after)
                    return snapshot;
            }
        }
    }
}
