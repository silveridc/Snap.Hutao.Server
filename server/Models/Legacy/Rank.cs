namespace server.Models.Legacy
{
    public class Rank(RankValue? damage, RankValue? takeDamage)
    {
        public RankValue? Damage { get; set; } = damage;

        public RankValue? TakeDamage { get; set; } = takeDamage;
    }
}