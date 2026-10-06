namespace server.Models.Upload
{
    public sealed class SimpleBattle
    {
        public int Index { get; set; }

        public List<int> Avatars { get; set; } = default!;
    }
}