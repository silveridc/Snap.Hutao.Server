namespace server.Core
{
    internal sealed class AsyncSemaphore(int initialCount, int maxCount = int.MaxValue)
    {
        private readonly Queue<TaskCompletionSource> waiters = [];
        private int currentCount = ValidateCount(initialCount);

        public int CurrentCount
        {
            get
            {
                lock (waiters)
                {
                    return currentCount;
                }
            }
        }

        public Task WaitAsync()
        {
            lock (waiters)
            {
                if (currentCount > 0)
                {
                    --currentCount;
                    return Task.CompletedTask;
                }

                TaskCompletionSource waiter = new();
                waiters.Enqueue(waiter);
                return waiter.Task;
            }
        }

        public void Release()
        {
            TaskCompletionSource? toRelease = default;
            lock (waiters)
            {
                if (waiters.Count > 0)
                {
                    toRelease = waiters.Dequeue();
                }
                else
                {
                    currentCount = Math.Min(currentCount + 1, maxCount);
                }
            }

            toRelease?.SetResult();
        }

        private static int ValidateCount(int initialCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(initialCount);
            return initialCount;
        }
    }
}