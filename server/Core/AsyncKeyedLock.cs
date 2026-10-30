using System.Collections.Concurrent;

namespace server.Core;

internal sealed class AsyncKeyedLock<TKey>(IEqualityComparer<TKey>? comparer = null)
    where TKey : notnull
{
    private static readonly Func<Task, object?, Releaser> Continuation = RunContinuation;

    private readonly ConcurrentDictionary<TKey, AsyncSemaphore> _semaphores = comparer is null ? [] : new(comparer);

    public Task<Releaser> LockAsync(TKey key)
    {
        Task wait;
        lock (_semaphores)
        {
            wait = _semaphores.GetOrAdd(key, _ => new(1, 1)).WaitAsync();
        }

        State stateObj = new(this, key);
        return wait.IsCompleted ? Task.FromResult<Releaser>(new(stateObj)) : wait.ContinueWith(Continuation, stateObj, default, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    public bool IsLocked(TKey key)
    {
        lock (_semaphores)
        {
            return _semaphores.TryGetValue(key, out _);
        }
    }

    private static Releaser RunContinuation(Task task, object? state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new((State)state);
    }

    internal readonly struct Releaser(State state) : IDisposable
    {
        public void Dispose()
        {
            lock (state.ToRelease._semaphores)
            {
                if (state.ToRelease._semaphores.TryGetValue(state.Key, out AsyncSemaphore? semaphore))
                {
                    semaphore.Release();
                    if (semaphore.CurrentCount is 1)
                    {
                        state.ToRelease._semaphores.TryRemove(state.Key, out _);
                    }
                }
            }
        }
    }

    internal sealed class State(AsyncKeyedLock<TKey> toRelease, TKey key)
    {
        public AsyncKeyedLock<TKey> ToRelease { get; } = toRelease;

        public TKey Key { get; } = key;
    }
}
