using server.Services.Afdian;

namespace server.Services.Expire
{
    public static class GachaLogExpireServiceExtension
    {
        public static ValueTask<TermExtendResult> ExtendGachaLogTermForAfdianOrderAsync(this GachaLogExpireService gachaLogExpireService, AfdianOrderInformation info)
        {
            return gachaLogExpireService.ExtendTermForUserNameAsync(info.UserName, 30 * info.OrderCount);
        }
    }
}