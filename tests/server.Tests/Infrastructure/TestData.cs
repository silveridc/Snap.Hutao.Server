using server.Models.Admin;
using server.Models.Entity.Announcement;
using server.Models.Entity.Redeem;
using server.Models.GachaLog;
using server.Models.Metadata;
using server.Models.Redeem;
using server.Models.Upload;
using server.Services.Legacy;

namespace server.Tests.Infrastructure;

/// <summary>构造满足服务端校验规则的请求体。</summary>
public static class TestData
{
    /// <summary>
    /// <see cref="SimpleRecord.Validate"/> 的角色列表要求：不少于 9 个角色、恰好一个主角
    /// （10000005 或 10000007）、御三家（10000006/10000015/10000021）齐全。
    /// </summary>
    private static readonly int[] RequiredAvatars = [10000005, 10000006, 10000015, 10000021, 10000002, 10000003, 10000014, 10000016, 10000020];

    /// <summary>一份能通过 <see cref="SimpleRecord.Validate"/> 的深渊上传数据。</summary>
    public static SimpleRecord SimpleRecord(string uid = "100000001", string identity = UploaderIdentities.SnapHutao, string? reservedUserName = null)
    {
        List<SimpleAvatar> avatars = [.. RequiredAvatars.Select(id => new SimpleAvatar
        {
            AvatarId = id,
            WeaponId = 12345,
            ReliquarySetIds = [1500001, 1500001, 1500002, 1500002],
            ActivedConstellationNumber = 0,
        })];

        // 上下半不能重复，且必须都在持有列表中
        List<SimpleLevel> levels =
        [
            new()
            {
                Index = 1,
                Star = 3,
                Battles =
                [
                    new() { Index = 1, Avatars = [10000005, 10000006] },
                    new() { Index = 2, Avatars = [10000015, 10000021] },
                ],
            },
        ];

        return new()
        {
            Uid = uid,
            Identity = identity,
            ReservedUserName = reservedUserName,
            Avatars = avatars,
            SpiralAbyss = new()
            {
                ScheduleId = SpiralAbyssScheduleId.GetForNow(),
                TotalBattleTimes = 12,
                TotalWinTimes = 12,
                Damage = new() { AvatarId = 10000005, Value = 123456 },
                TakeDamage = new() { AvatarId = 10000006, Value = 65432 },
                Floors =
                [
                    new() { Index = 9, Star = 3, Levels = levels },
                    new() { Index = 10, Star = 3, Levels = levels },
                    new() { Index = 11, Star = 3, Levels = levels },
                    new() { Index = 12, Star = 3, Levels = levels },
                ],
            },
        };
    }

    /// <summary>一份能通过 <see cref="server.Models.RoleCombat.SimpleRoleCombatRecord.Validate"/> 的剧诗上传数据。</summary>
    public static server.Models.RoleCombat.SimpleRoleCombatRecord RoleCombatRecord(string uid = "100000002")
    {
        return new()
        {
            Version = 1,
            Uid = uid,
            Identity = UploaderIdentities.SnapHutao,
            ScheduleId = server.Services.RoleCombat.RoleCombatScheduleId.GetForNow(),
            BackupAvatars = [.. Enumerable.Range(0, 12).Select(i => (uint)(10000000 + i))],
        };
    }

    /// <summary>一批合法的祈愿记录。</summary>
    public static UidAndItems GachaItems(string uid = "100000003", int count = 3)
    {
        // 记录主键由客户端提供，随机化基址避免不同用例上传同一批 Id 时撞主键
        long idBase = Random.Shared.NextInt64(1_000_000_000_000_000_000L, long.MaxValue - count);

        return new()
        {
            Uid = uid,
            Items = [.. Enumerable.Range(1, count).Select(i => new SimpleGachaItem
            {
                GachaType = GachaConfigType.ActivityAvatar,
                QueryType = GachaConfigType.ActivityAvatar,
                ItemId = 10000002 + i,
                Time = DateTimeOffset.UtcNow.AddMinutes(-i),
                Id = idBase + i,
            })],
        };
    }

    /// <summary>深渊上传数据的 JSON 形式，供冒烟测试直接作为请求体。</summary>
    public static string SimpleRecordJson(string uid)
    {
        return JsonSerializer.Serialize(SimpleRecord(uid));
    }

    /// <summary>
    /// 把深渊数据改成「上一期」的 JSON。服务端拿到后会先按当前期查询记录，
    /// 直接改对象上的 ScheduleId 会让那次查询命中两行并抛异常，所以只改序列化后的文本。
    /// </summary>
    public static string OutdatedSpiralAbyssRecordJson(string uid)
    {
        return SimpleRecordJson(uid).Replace(
            $"\"ScheduleId\":{SpiralAbyssScheduleId.GetForNow()}",
            $"\"ScheduleId\":{SpiralAbyssScheduleId.GetForNow() - 1}",
            StringComparison.Ordinal);
    }

    /// <summary>剧诗上传数据的 JSON 形式。</summary>
    public static string RoleCombatRecordJson(string uid)
    {
        return JsonSerializer.Serialize(RoleCombatRecord(uid));
    }

    /// <summary>祈愿记录上传数据的 JSON 形式。</summary>
    public static string GachaItemsJson(string uid)
    {
        return JsonSerializer.Serialize(GachaItems(uid));
    }

    /// <summary>公告上传请求体。</summary>
    public static string AnnouncementJson()
    {
        return JsonSerializer.Serialize(new HutaoUploadAnnouncement
        {
            Title = "Test Announcement",
            Content = "Test content",
            Severity = AnnouncementSeverity.Informational,
            Link = "https://example.com",
        });
    }

    /// <summary>公告编辑请求体。</summary>
    public static string AnnouncementUpdateJson()
    {
        return JsonSerializer.Serialize(new AnnouncementUpdateRequest
        {
            Id = 1,
            Title = "Updated",
            Content = "Updated content",
        });
    }

    /// <summary>兑换码生成请求体。</summary>
    public static string RedeemGenerateJson()
    {
        return JsonSerializer.Serialize(new RedeemGenerateRequest
        {
            Count = 1,
            Type = RedeemCodeType.TimeLimited,
            ServiceType = RedeemCodeTargetServiceType.GachaLog,
            Value = 1,
            Description = "test",
            ExpireTime = DateTimeOffset.UtcNow.AddDays(1),
            Times = 1,
        });
    }

    /// <summary>兑换码编辑请求体。</summary>
    public static string RedeemUpdateJson()
    {
        return JsonSerializer.Serialize(new RedeemCodeUpdateRequest
        {
            Id = 1,
            Type = (uint)RedeemCodeType.TimeLimited,
            ServiceType = (uint)RedeemCodeTargetServiceType.GachaLog,
            Value = 1,
            Description = "test",
            ExpireTime = DateTimeOffset.UtcNow.AddDays(1),
            TimesAllowed = 1,
        });
    }

    /// <summary>客户端版本发布请求体。</summary>
    public static string ReleasePublishJson(string version = "1.17.2.0")
    {
        return JsonSerializer.Serialize(new HutaoReleasePublishRequest
        {
            Version = version,
            Validation = new string('a', 64),
            Mirrors = [new server.Models.Patch.HutaoPackageMirror { MirrorName = "test", Url = "https://example.com/hutao.zip" }],
        });
    }

    /// <summary>
    /// 爱发电订单回调（<c>POST /Webhook</c>）的请求体；服务端用 remark 里的邮箱定位用户。
    /// </summary>
    public static string AfdianOrderJson(string tradeNo, string skuId, int count, string remark)
    {
        return JsonSerializer.Serialize(new
        {
            type = "order",
            data = new
            {
                order = new
                {
                    out_trade_no = tradeNo,
                    remark,
                    sku_detail = new[] { new { sku_id = skuId, count, name = "test sku" } },
                },
            },
        });
    }

    /// <summary>
    /// 爱发电 <c>query-order</c> 的应答：回显同一笔订单，使 Webhook 的订单校验通过。
    /// </summary>
    public static string AfdianQueryOrderResponseJson(string tradeNo, string skuId, int count)
    {
        return JsonSerializer.Serialize(new
        {
            ec = 200,
            em = string.Empty,
            data = new
            {
                list = new[]
                {
                    new
                    {
                        out_trade_no = tradeNo,
                        remark = "whatever@example.com",
                        sku_detail = new[] { new { sku_id = skuId, count, name = "test sku" } },
                    },
                },
            },
        });
    }
}
