namespace server.Models.Legacy
{
    public abstract class AvatarBuild(int avatarId)
    {
        public int AvatarId { get; set; } = avatarId;
    }
}