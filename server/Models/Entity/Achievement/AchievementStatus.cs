namespace server.Models.Entity.Achievement
{
    public enum AchievementStatus /*: int*/
    {
        STATUS_UNRECOGNIZED = -1,
        STATUS_INVALID = 0,
        STATUS_UNFINISHED = 1,
        STATUS_FINISHED = 2,
        STATUS_REWARD_TAKEN = 3,
    }
}