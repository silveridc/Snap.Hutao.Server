namespace server.Models.Afdian
{
    public class ListWrapper<T>
    {
        [JsonPropertyName("list")]
        public List<T> List { get; set; } = default!;
    }
}