using server.Models.Entity.Passport;

namespace server.Services.OAuth
{
    public sealed class OAuthBindStatus
    {
        public bool IsBinded { get; set; }

        [MemberNotNullWhen(true, nameof(IsBinded))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DisplayName { get; set; } = default!;

        [MemberNotNullWhen(true, nameof(IsBinded))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? CreateTime { get; set; }

        public static OAuthBindStatus Binded(OAuthBindIdentity identity)
        {
            return new()
            {
                IsBinded = true,
                DisplayName = identity.DisplayName,
                CreateTime = identity.CreatedAt,
            };
        }

        public static OAuthBindStatus NotBinded()
        {
            return new()
            {
                IsBinded = false,
            };
        }
    }
}