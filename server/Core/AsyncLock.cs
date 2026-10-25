namespace server.Core;

internal sealed class AsyncLock
{
    private static readonly Func<Task, object?, Releaser> Continuation = RunContinuation;

    private readonly AsyncSemaphore _semaphore;
    private readonly Task<Releaser> _releaser;

    public AsyncLock()
    {
        _semaphore = new(1, 1);
        _releaser = Task.FromResult(new Releaser(this));
    }

    public Task<Releaser> LockAsync()
    {
        Task wait = _semaphore.WaitAsync();
        return wait.IsCompleted ? _releaser : wait.ContinueWith(Continuation, this, default, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private static Releaser RunContinuation(Task task, object? state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new((AsyncLock)state);
    }

    internal readonly struct Releaser(AsyncLock toRelease) : IDisposable
    {
        public readonly void Dispose()
        {
            toRelease._semaphore.Release();
        }
    }
}
