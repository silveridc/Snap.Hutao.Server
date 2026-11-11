namespace server.Models.Admin;

public sealed class AdminAccountBindRequest
{
    /// <summary>Gets or sets 主站账号的邮箱，与注册时填写的一致.</summary>
    public string? UserName
    {
        get; set;
    }
}
