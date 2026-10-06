namespace server.Models.Redeem
{
    public enum RedeemStatus
    {
        Ok,
        Invalid,
        NotExists,
        AlreadyUsed,
        Expired,
        NotEnough,
        NoSuchUser,
        DbError,
    }
}