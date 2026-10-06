namespace server.Services.GachaLog
{
    public sealed class GachaLogSaveResult(GachaLogSaveResultKind kind, int save = 0)
    {
        public GachaLogSaveResultKind Kind { get; } = kind;

        public int SaveCount { get; } = save;
    }
}