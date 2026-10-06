using server.Models.Entity.Redeem;
using server.Models.Redeem;
using server.Services.Afdian;

namespace server.Services.Redeem
{
    public static class RedeemServiceExtension
    {
        public static async ValueTask<string> GenerateRedeemCodeForPurchaseGachaLogServiceAsync(this RedeemService redeemService, AfdianOrderInformation info)
        {
            RedeemGenerateRequest req = new()
            {
                Count = 1U,
                Type = RedeemCodeType.TimeLimited | RedeemCodeType.TimesLimited,
                ServiceType = RedeemCodeTargetServiceType.GachaLog,
                Value = 30 * info.OrderCount,
                Description = "For unregistered user",
                ExpireTime = DateTimeOffset.UtcNow + TimeSpan.FromDays(3),
                Times = 1,
                Creator = "AfdianWebhookService",
            };

            RedeemGenerateResponse codes = await redeemService.GenerateRedeemCodesAsync(req).ConfigureAwait(false);
            return codes.Codes.Single();
        }

        public static async ValueTask<string> GenerateRedeemCodeForPurchaseCdnServiceAsync(this RedeemService redeemService, AfdianOrderInformation info)
        {
            RedeemGenerateRequest req = new()
            {
                Count = 1U,
                Type = RedeemCodeType.TimeLimited | RedeemCodeType.TimesLimited,
                ServiceType = RedeemCodeTargetServiceType.Cdn,
                Value = 30 * info.OrderCount,
                Description = "For unregistered user",
                ExpireTime = DateTimeOffset.UtcNow + TimeSpan.FromDays(3),
                Times = 1,
                Creator = "AfdianWebhookService",
            };

            RedeemGenerateResponse codes = await redeemService.GenerateRedeemCodesAsync(req).ConfigureAwait(false);
            return codes.Codes.Single();
        }
    }
}