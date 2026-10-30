namespace server.Models.Entity;

[Table("request_statistics")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public class RequestStatistics
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequestStatistics"/> class.
    /// EF用.
    /// </summary>
    public RequestStatistics()
    {
    }

    public RequestStatistics(ActionExecutingContext context)
    {
        HttpRequest request = context.HttpContext.Request;

        UserAgent = request.Headers.UserAgent.ToString();
        Path = request.Path;
        Count = 0;
    }

    /// <summary>
    /// Gets or sets 主键.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long PrimaryId
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets uA头.
    /// </summary>
    public string UserAgent { get; set; } = default!;

    /// <summary>
    /// Gets or sets 请求路径.
    /// </summary>
    public string Path { get; set; } = default!;

    /// <summary>
    /// Gets or sets 请求次数.
    /// </summary>
    public long Count
    {
        get; set;
    }

    public static RequestStatistics CreateFromContext(ActionExecutingContext context)
    {
        HttpRequest request = context.HttpContext.Request;
        return new()
        {
            UserAgent = request.Headers.UserAgent.ToString(),
            Path = request.Path,
            Count = 0,
        };
    }
}
