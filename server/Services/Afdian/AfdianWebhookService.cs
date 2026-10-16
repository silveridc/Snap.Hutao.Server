using MailKit.Net.Smtp;
using MimeKit;
using server.Extension;
using server.Models.Afdian;
using server.Services.Admin;

namespace server.Services.Afdian
{
    // Scoped（依赖 Scoped 的 RedeemService，注册成 Singleton 会无法解析）
    public sealed class AfdianWebhookService(IServiceProvider serviceProvider)
    {
        private readonly IDistributedCache cache = serviceProvider.GetRequiredService<IDistributedCache>();
        private readonly AfdianOptions afdianOptions = serviceProvider.GetRequiredService<AppOptions>().Afdian;
        private readonly Afdian2Options afdian2Options = serviceProvider.GetRequiredService<AppOptions>().Afdian2;
        private readonly SystemConfigService config = serviceProvider.GetRequiredService<SystemConfigService>();
        private readonly GachaLogExpireService gachaLogExpireService = serviceProvider.GetRequiredService<GachaLogExpireService>();
        private readonly CdnExpireService cdnExpireService = serviceProvider.GetRequiredService<CdnExpireService>();
        private readonly MailService mailService = serviceProvider.GetRequiredService<MailService>();
        private readonly HttpClient httpClient = serviceProvider.GetRequiredService<HttpClient>();
        private readonly RedeemService redeemService = serviceProvider.GetRequiredService<RedeemService>();

        public async ValueTask ProcessIncomingOrderAsync(Order order)
        {
            string orderNumber = order.OutTradeNo;
            if (cache.HasFlag(orderNumber))
            {
                return;
            }

            // prevent multiple requests in 10 minutes
            cache.SetFlag(orderNumber, TimeSpan.FromMinutes(10));

            _ = await ProcessIncomingOrderCoreAsync(order).ConfigureAwait(false);
        }

        private async ValueTask<AfdianOrderInformation> ProcessIncomingOrderCoreAsync(Order order)
        {
            AfdianOrderInformation info = new()
            {
                OrderNumber = order.OutTradeNo,
                UserName = order.Remark, // We use remark to get the userName(email) of the user
            };

            if (order is not { SkuDetail: [{ } skuDetail, ..] })
            {
                info.Status = AfdianOrderStatus.NoSkuDetails;
                return info;
            }

            info.SkuId = skuDetail.SkuId;
            info.OrderCount = skuDetail.Count;

            // GachaLog Upload
            if (skuDetail.SkuId == config.AfdianSkuGachaLog)
            {
                if (!await ValidateOrderInformationAsync(info, afdianOptions).ConfigureAwait(false))
                {
                    // info's status will be set in ValidateOrderInformationAsync
                    return info;
                }

                TermExtendResult result = await gachaLogExpireService.ExtendGachaLogTermForAfdianOrderAsync(info).ConfigureAwait(false);
                switch (result.Kind)
                {
                    case TermExtendResultKind.Ok:
                        break;
                    case TermExtendResultKind.NoSuchUser:
                        string redeemCode = await redeemService.GenerateRedeemCodeForPurchaseGachaLogServiceAsync(info).ConfigureAwait(false);
                        try
                        {
                            await mailService.SendPurchaseGachaLogStorageServiceNoSuchUserAsync(info.UserName, redeemCode, info.OrderNumber).ConfigureAwait(false);
                            info.Status = AfdianOrderStatus.GachaLogTermExtendNoSuchUser;
                        }
                        catch (SmtpCommandException)
                        {
                            info.Status = AfdianOrderStatus.InvalidUserName;
                        }
                        catch (ParseException)
                        {
                            info.Status = AfdianOrderStatus.InvalidUserName;
                        }

                        break;
                    case TermExtendResultKind.DbError:
                        info.Status = AfdianOrderStatus.GachaLogTermExtendDbError;
                        break;
                }
            }
            else if (skuDetail.SkuId == config.Afdian2SkuCdn)
            {
                if (!await ValidateOrderInformationAsync(info, afdian2Options).ConfigureAwait(false))
                {
                    // info's status will be set in ValidateOrderInformationAsync
                    return info;
                }

                TermExtendResult result = await cdnExpireService.ExtendCdnTermForAfdianOrderAsync(info).ConfigureAwait(false);
                switch (result.Kind)
                {
                    case TermExtendResultKind.Ok:
                        break;
                    case TermExtendResultKind.NoSuchUser:
                        string redeemCode = await redeemService.GenerateRedeemCodeForPurchaseCdnServiceAsync(info).ConfigureAwait(false);
                        try
                        {
                            await mailService.SendPurchaseCdnServiceNoSuchUserAsync(info.UserName, redeemCode, info.OrderNumber).ConfigureAwait(false);
                            info.Status = AfdianOrderStatus.CdnTermExtendNoSuchUser;
                        }
                        catch (SmtpCommandException)
                        {
                            info.Status = AfdianOrderStatus.InvalidUserName;
                        }
                        catch (ParseException)
                        {
                            info.Status = AfdianOrderStatus.InvalidUserName;
                        }

                        break;
                    case TermExtendResultKind.DbError:
                        info.Status = AfdianOrderStatus.CdnTermExtendDbError;
                        break;
                }
            }
            else
            {
                info.Status = AfdianOrderStatus.SkuIdNotSupported;
            }

            return info;
        }

        private async ValueTask<bool> ValidateOrderInformationAsync(AfdianOrderInformation info, AfdianAuthorizationOptions afdianOptions)
        {
            QueryOrder query = QueryOrder.Create(afdianOptions.UserId, info.OrderNumber, afdianOptions.UserToken);

            HttpResponseMessage? response = default;

            int requestCount = 0;
            while (++requestCount <= 10)
            {
                response = await httpClient.PostAsJsonAsync("https://afdian.com/api/open/query-order", query).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    break;
                }

                response.Dispose();
                await Task.Delay(3000).ConfigureAwait(false);
            }

            AfdianResponse<ListWrapper<Order>>? resp;
            using (response)
            {
                if (response is not { IsSuccessStatusCode: true })
                {
                    info.Status = AfdianOrderStatus.ValidationRequestFailed;
                    return false;
                }

                resp = await response.Content.ReadFromJsonAsync<AfdianResponse<ListWrapper<Order>>>().ConfigureAwait(false);
                if (resp is not { ErrorCode: 200 })
                {
                    info.Status = AfdianOrderStatus.ValidationResponseInvalid;
                    return false;
                }
            }

            if (resp is not { Data.List: [{ } order, ..] })
            {
                info.Status = AfdianOrderStatus.ValidationResponseNoOrder;
                return false;
            }

            if (order is not { SkuDetail: [{ } skuDetail, ..] })
            {
                info.Status = AfdianOrderStatus.ValidationResponseNoSkuDetail;
                return false;
            }

            if (order.OutTradeNo != info.OrderNumber || skuDetail.SkuId != info.SkuId || skuDetail.Count != info.OrderCount)
            {
                info.Status = AfdianOrderStatus.ValidationResponseSkuDetailNotMatch;
                return false;
            }

            return true;
        }
    }
}