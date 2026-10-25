using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using server.Extension;
using server.Models.Context;
using server.Models.Entity.SpiralAbyss;
using server.Services.Legacy;
using server.Tests.Infrastructure;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace server.Tests;

/// <summary>
/// /Statistics/* 对「无 SecurityStamp 式历史毒行」的同类问题——非有限浮点字面量的守卫。
/// 写入侧修复（StatisticsTracker 空期按 0 计）之前，空期把 TimeAverage 算成 Infinity/NaN，
/// 以 AllowNamedFloatingPointLiterals 字面量落库；这样的行读回后 MVC 的 JSON 输出无法表达
/// 非有限值，接口会稳定 500。FiniteDouble/SingleJsonConverter 必须把这类值安全写出（归零），
/// 且不影响正常数字的往返；二次请求还要经过分布式缓存里的裸字面量往返。
/// </summary>
[Collection(HutaoServerCollection.Name)]
public sealed class StatisticsTests
{
    private readonly HutaoServerFactory factory;
    private readonly HttpClient client;

    public StatisticsTests(HutaoServerFactory factory)
    {
        this.factory = factory;
        client = factory.CreateApiClient();
    }

    /// <summary>
    /// 清掉这个统计键的缓存与旧行，再插入指定的毒行。多个用例共用同一个
    /// (Name, ScheduleId)，不清会让 SaveStatistics 的 SingleOrDefault 与缓存互相污染。
    /// </summary>
    private async Task PreparePoisonRowAsync(string name, string data)
    {
        int scheduleId = SpiralAbyssScheduleId.GetForNow() - 1;
        using IServiceScope scope = factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Statistics.Where(s => s.ScheduleId == scheduleId && s.Name == name).ExecuteDeleteAsync();
        db.Statistics.Add(new LegacyStatistics { Name = name, ScheduleId = scheduleId, Data = data });
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IDistributedCache>()
            .RemoveAsync($"{name}:{scheduleId}");
    }

    [Fact]
    public async Task Overview_ServesHistoricalInfinityRow_AsFiniteZero()
    {
        // 与真实毒行同形（生产 123 期实测）：TimeAverage 是写入侧修复前落库的 Infinity 字面量
        await PreparePoisonRowAsync(LegacyStatistics.Overview,
            """{"ScheduleId":123,"RecordTotal":0,"SpiralAbyssTotal":0,"SpiralAbyssFullStar":0,"SpiralAbyssPassed":0,"SpiralAbyssStarTotal":0,"SpiralAbyssBattleTotal":0,"Timestamp":1791194700562,"TimeTotal":434.831,"TimeAverage":"Infinity"}""");

        // ?Last=true 查的是上一期，正是生产 500 的触发参数
        JsonNode root = JsonNode.Parse(
            await (await client.GetAsync("/api/Statistics/Overview?Last=true")).Content.ReadAsStringAsync())!;
        Assert.Equal(0, (int)root["retcode"]!);
        JsonNode data = root["data"]!;
        Assert.Equal(0, (double)data["TimeAverage"]!);
        Assert.Equal(434.831, (double)data["TimeTotal"]!);
    }

    [Fact]
    public async Task Overview_ServesHistoricalNaNRow_AsFiniteZero()
    {
        // 同类毒行的其它字面量形态：NaN 与 -Infinity，字段位置也不同
        await PreparePoisonRowAsync(LegacyStatistics.Overview,
            """{"ScheduleId":123,"RecordTotal":0,"SpiralAbyssTotal":0,"SpiralAbyssFullStar":0,"SpiralAbyssPassed":0,"SpiralAbyssStarTotal":0,"SpiralAbyssBattleTotal":0,"Timestamp":1791194700562,"TimeTotal":"NaN","TimeAverage":"-Infinity"}""");

        JsonNode root = JsonNode.Parse(
            await (await client.GetAsync("/api/Statistics/Overview?Last=true")).Content.ReadAsStringAsync())!;
        Assert.Equal(0, (int)root["retcode"]!);
        Assert.Equal(0, (double)root["data"]!["TimeTotal"]!);
        Assert.Equal(0, (double)root["data"]!["TimeAverage"]!);
    }

    [Fact]
    public async Task Overview_SecondRequest_ServesTheCachedValueAsFiniteJson()
    {
        // 首次请求会把反序列化结果（真实 Infinity double）写进分布式缓存；
        // 二次请求走缓存，缓存 JSON 里是裸 Infinity 字面量，读回再由 MVC 写出，两跳都不能 500
        await PreparePoisonRowAsync(LegacyStatistics.Overview,
            """{"ScheduleId":123,"RecordTotal":0,"SpiralAbyssTotal":0,"SpiralAbyssFullStar":0,"SpiralAbyssPassed":0,"SpiralAbyssStarTotal":0,"SpiralAbyssBattleTotal":0,"Timestamp":1791194700562,"TimeTotal":434.831,"TimeAverage":"Infinity"}""");

        for (int i = 1; i <= 2; i++)
        {
            using HttpResponseMessage response = await client.GetAsync("/api/Statistics/Overview?Last=true");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            JsonNode root = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            Assert.True((int)root["retcode"]! == 0, $"第 {i} 次请求失败：{root.ToJsonString()}");
            Assert.Equal(0, (double)root["data"]!["TimeAverage"]!);
        }
    }

    [Fact]
    public async Task ListEndpoint_ServesPoisonedRates_AsFiniteZero()
    {
        // 转换器注册在 MVC 全局：列表型统计（ItemRate.Rate 是 double）同样被兜住。
        // AttendanceRate 的元素类型是基类 ItemRate（只有 Item 与 Rate），毒化字段按这个形状写。
        await PreparePoisonRowAsync(LegacyStatistics.AvatarAppearanceRank,
            """[{"Floor":9,"Ranks":[{"Item":10000002,"Rate":"NaN"}]}]""");

        using HttpResponseMessage response = await client.GetAsync("/api/Statistics/Avatar/AttendanceRate?Last=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonNode root = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(0, (int)root["retcode"]!);
        JsonNode rank = root["data"]![0]!["Ranks"]![0]!;
        Assert.Equal(0, (double)rank["Rate"]!);
        Assert.Equal(10000002, (int)rank["Item"]!);
    }

    /// <summary>转换器必须真的挂在 MVC 输出 options 上——只存在于程序集里救不了任何接口。</summary>
    [Fact]
    public void MvcOutputOptions_ContainTheFiniteNumberConverters()
    {
        JsonSerializerOptions json = factory
            .GetService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>()
            .Value.JsonSerializerOptions;

        Assert.Contains(json.Converters, converter => converter is FiniteDoubleJsonConverter);
        Assert.Contains(json.Converters, converter => converter is FiniteSingleJsonConverter);

        // 用宿主真实的输出 options 序列化非有限值，产出必须是合法 JSON
        string output = JsonSerializer.Serialize(
            new { Total = double.NegativeInfinity, Rate = float.NaN },
            json);
        Assert.Contains("\"Total\":0", output);
        Assert.Contains("\"Rate\":0", output);
    }
}
