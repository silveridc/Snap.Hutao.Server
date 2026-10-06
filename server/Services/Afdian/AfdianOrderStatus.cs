namespace server.Services.Afdian
{
    public enum AfdianOrderStatus
    {
        Ok,
        NoSkuDetails,
        SkuIdNotSupported,
        ValidationRequestFailed,
        ValidationResponseInvalid,
        ValidationResponseNoOrder,
        ValidationResponseNoSkuDetail,
        ValidationResponseSkuDetailNotMatch,
        GachaLogTermExtendNoSuchUser,
        GachaLogTermExtendDbError,
        CdnTermExtendNoSuchUser,
        CdnTermExtendDbError,
        InvalidUserName,
    }
}