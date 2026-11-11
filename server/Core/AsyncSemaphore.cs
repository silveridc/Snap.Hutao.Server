namespace server.Core;

internal sealed class AsyncSemaphore(int initialCount, int maxCount = int.MaxValue)
{
    private readonly Queue<TaskCompletionSource> _waiters = [];
    private int _currentCount = ValidateCount(initialCount);

    public int CurrentCount
    {
        get
        {
            lock (_waiters)
            {
                return _currentCount;
            }
        }
    }

    public Task WaitAsync()
    {
        lock (_waiters)
        {
            if (_currentCount > 0)
            {
                --_currentCount;
                return Task.CompletedTask;
            }

            TaskCompletionSource waiter = new();
            _waiters.Enqueue(waiter);
            return waiter.Task;
        }
    }

    public void Release()
    {
        TaskCompletionSource? toRelease = default;
        lock (_waiters)
        {
            if (_waiters.Count > 0)
            {
                toRelease = _waiters.Dequeue();
            }
            else
            {
                _currentCount = Math.Min(_currentCount + 1, maxCount);
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
