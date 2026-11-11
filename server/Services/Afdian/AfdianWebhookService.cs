using MailKit.Net.Smtp;
using MimeKit;
using server.Models.Afdian;
using server.Services.Admin;
using server.Services.Mail;

namespace server.Services.Afdian;

// Scoped
public sealed class AfdianWebhookService(
    IDistributedCache cache,
    SystemConfigService config,
    GachaLogExpireService gachaLogExpireService,
    CdnExpireService cdnExpireService,
    MailService mailService,
    HttpClient httpClient,
    RedeemService redeemService)
{
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
            if (!await ValidateOrderInformationAsync(info, config.AfdianUserId, config.AfdianUserToken).ConfigureAwait(false))
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
                    string redeemCode = await redeemService.GenerateRedeemCodeForPurchaseGachaLogServiceAsync(info, config.RedeemCodeExpireDays).ConfigureAwait(false);
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
            if (!await ValidateOrderInformationAsync(info, config.Afdian2UserId, config.Afdian2UserToken).ConfigureAwait(false))
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
                    string redeemCode = await redeemService.GenerateRedeemCodeForPurchaseCdnServiceAsync(info, config.RedeemCodeExpireDays).ConfigureAwait(false);
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

    private async ValueTask<bool> ValidateOrderInformationAsync(AfdianOrderInformation info, string userId, string userToken)
    {
        QueryOrder query = QueryOrder.Create(userId, info.OrderNumber, userToken);

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
