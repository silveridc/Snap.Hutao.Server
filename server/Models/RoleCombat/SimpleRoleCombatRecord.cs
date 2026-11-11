namespace server.Models.RoleCombat;

public sealed class SimpleRoleCombatRecord
{
    public uint Version
    {
        get; set;
    }

    public string Uid { get; set; } = default!;

    public string Identity { get; set; } = default!;

    public List<uint> BackupAvatars { get; set; } = default!;

    public int ScheduleId
    {
        get; set;
    }

    public bool Validate()
    {
        if (Version != 1)
        {
            return false;
        }

        if (Uid is null || Uid.Length < 9)
        {
            return false;
        }

        if (BackupAvatars is not { Count: >= 8 })
        {
            return false;
        }

        return true;
    }
}
