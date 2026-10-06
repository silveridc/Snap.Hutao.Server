namespace server.Options
{
    public sealed class GenshinPizzaHelperOptions
    {
        public string UidSalt { get; set; } = default!;

        public string ApiSalt { get; set; } = default!;

        public GenshinPizzaHelperEndPointsOptions EndPoints { get; set; } = default!;
    }
}