namespace server.Models.Legacy
{
    public class RankValue(int item, int value, double rate, double rateOnAvatar) : ItemRate<int, double>(item, rate)
    {
        public int Value { get; set; } = value;

        public double RateOnAvatar { get; set; } = rateOnAvatar;
    }
}