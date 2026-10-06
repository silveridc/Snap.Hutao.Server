namespace server.Models.Redeem
{
    public sealed class RedeemGenerateResponse(List<string> codes)
    {
        public List<string> Codes { get; set; } = codes;
    }
}