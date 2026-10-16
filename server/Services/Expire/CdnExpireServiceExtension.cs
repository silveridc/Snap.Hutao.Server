namespace server.Services.Expire
{
    public static class CdnExpireServiceExtension
    {
        public static ValueTask<TermExtendResult> ExtendCdnTermForAfdianOrderAsync(this CdnExpireService cdnExpireService, AfdianOrderInformation info)
        {
            return cdnExpireService.ExtendTermForUserNameAsync(info.UserName, 30 * info.OrderCount);
        }
    }
}