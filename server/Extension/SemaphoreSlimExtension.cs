namespace server.Extension
{
    public static class SemaphoreSlimExtension
    {
        public static async Task<IDisposable> EnterAsync(this SemaphoreSlim semaphoreSlim)
        {
            await semaphoreSlim.WaitAsync().ConfigureAwait(false);
            return new SemaphoreSlimReleaser(semaphoreSlim);
        }

        private readonly struct SemaphoreSlimReleaser(SemaphoreSlim semaphoreSlim) : IDisposable
        {
            public void Dispose()
            {
                semaphoreSlim.Release();
            }
        }
    }
}