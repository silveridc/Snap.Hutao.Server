using server.Extension;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace server.Tests;

public sealed class FiniteNumberJsonConverterTests
{
    /// <summary>与 SpiralAbyssStatisticsService/DistributedCacheExtension 一致的存储侧 options，
    /// 模拟真实缓存往返环境：非有限值以带引号的字面量形态落库/落缓存。</summary>
    private static readonly JsonSerializerOptions RoundTripOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new() { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
        options.Converters.Add(new FiniteDoubleJsonConverter());
        options.Converters.Add(new FiniteSingleJsonConverter());
        return options;
    }

    // ---------- 写侧：非有限值归零 ----------

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.NaN)]
    public void Write_NonFiniteDouble_BecomesZero(double value)
    {
        Assert.Equal("0", JsonSerializer.Serialize(value, RoundTripOptions));
    }

    [Theory]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.NaN)]
    public void Write_NonFiniteSingle_BecomesZero(float value)
    {
        Assert.Equal("0", JsonSerializer.Serialize(value, RoundTripOptions));
    }

    // ---------- 写侧：有限值逐位不变 ----------

    [Theory]
    [InlineData(0d)]
    [InlineData(-1.5d)]
    [InlineData(434.831d)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    [InlineData(double.Epsilon)]
    public void Write_FiniteDouble_PassesThrough(double value)
    {
        Assert.Equal(value, JsonSerializer.Deserialize<double>(JsonSerializer.Serialize(value, RoundTripOptions), RoundTripOptions));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1.5f)]
    [InlineData(float.MaxValue)]
    [InlineData(float.Epsilon)]
    public void Write_FiniteSingle_PassesThrough(float value)
    {
        Assert.Equal(value, JsonSerializer.Deserialize<float>(JsonSerializer.Serialize(value, RoundTripOptions), RoundTripOptions));
    }

    // ---------- 读侧：库里的历史字面量形态 ----------

    [Theory]
    [InlineData("Infinity", double.PositiveInfinity)]
    [InlineData("-Infinity", double.NegativeInfinity)]
    [InlineData("NaN", double.NaN)]
    [InlineData("434.831", 434.831d)]
    [InlineData("1e300", 1e300d)]
    public void Read_QuotedDoubleLiterals_AreAccepted(string literal, double expected)
    {
        double actual = JsonSerializer.Deserialize<double>($"\"{literal}\"", RoundTripOptions);
        Assert.Equal(expected, actual);
        Assert.Equal(double.IsNaN(expected), double.IsNaN(actual));
    }

    [Fact]
    public void StorageOptions_QuoteNamedLiterals_OnWriteAndRead()
    {
        // 存储侧 options（AllowNamedFloatingPointLiterals）把非有限值写成带引号的字面量，
        // 读回也只接受带引号形态——库行与缓存里的实际形态如此；裸 Infinity token 对 reader 从来不合法。
        // 这条契约钉死「毒值长什么样」，免得以后有人按裸 token 的想象改坏读写两侧。
        JsonSerializerOptions storage = new() { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };

        Assert.Equal("\"Infinity\"", JsonSerializer.Serialize(double.PositiveInfinity, storage));
        Assert.Equal("\"NaN\"", JsonSerializer.Serialize(double.NaN, storage));
        Assert.Equal(double.PositiveInfinity, JsonSerializer.Deserialize<double>("\"Infinity\"", storage));
        Assert.True(double.IsNaN(JsonSerializer.Deserialize<double>("\"NaN\"", storage)));

        // 缓存里对象属性位上的真实形态：写入引号字面量，读回成真实非有限值，converter 再兜住写出
        Payload cached = JsonSerializer.Deserialize<Payload>("""{"Total":434.831,"Average":"Infinity","Rate":"-Infinity"}""", RoundTripOptions)!;
        Assert.Equal(434.831, cached.Total);
        Assert.Equal(double.PositiveInfinity, cached.Average);
        Assert.Equal(float.NegativeInfinity, cached.Rate);
    }

    [Fact]
    public void Read_SingleLiterals_AreAccepted()
    {
        Assert.Equal(float.PositiveInfinity, JsonSerializer.Deserialize<float>("\"Infinity\"", RoundTripOptions));
        Assert.True(float.IsNaN(JsonSerializer.Deserialize<float>("\"NaN\"", RoundTripOptions)));
        Assert.Equal(1.5f, JsonSerializer.Deserialize<float>("\"1.5\"", RoundTripOptions));
    }

    // ---------- 端到端：毒值从读到写的完整往返 ----------

    private sealed class Payload
    {
        public double Total { get; set; }

        public double Average { get; set; }

        public float Rate { get; set; }
    }

    [Fact]
    public void RoundTrip_PoisonedPayload_IsServedAsFiniteJson()
    {
        // 与生产毒行同形：字符串字面量落库，读回成真实非有限值，再由 MVC options 写出
        Payload poisoned = JsonSerializer.Deserialize<Payload>(
            """{"Total":"NaN","Average":"Infinity","Rate":"-Infinity"}""",
            RoundTripOptions)!;
        Assert.True(double.IsNaN(poisoned.Total));
        Assert.Equal(double.PositiveInfinity, poisoned.Average);
        Assert.Equal(float.NegativeInfinity, poisoned.Rate);

        string output = JsonSerializer.Serialize(poisoned, RoundTripOptions);
        Assert.Equal("""{"Total":0,"Average":0,"Rate":0}""", output);
    }

    [Fact]
    public void RoundTrip_FinitePayload_IsUnchanged()
    {
        const string json = """{"Total":434.831,"Average":0.25,"Rate":1.5}""";
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<Payload>(json, RoundTripOptions), RoundTripOptions));
    }

    [Fact]
    public void Read_PlainNumbers_AreUnchanged()
    {
        Assert.Equal(434.831d, JsonSerializer.Deserialize<double>("434.831", RoundTripOptions));
        Assert.Equal(-2.5f, JsonSerializer.Deserialize<float>("-2.5", RoundTripOptions));
        Assert.Equal(1e300d, JsonSerializer.Deserialize<double>("1e300", RoundTripOptions));
    }

    [Fact]
    public void Write_PreservesMaximumPrecision()
    {
        // 归零分支之外不能引入任何精度损失：最刁钻的有限值也要逐位往返
        double[] tricky =
        [
            0.1 + 0.2,
            -double.Epsilon,
            123456789.123456789,
            -987654321.987654321,
        ];

        foreach (double value in tricky)
        {
            string json = JsonSerializer.Serialize(value, RoundTripOptions);
            Assert.Equal(value, double.Parse(json, NumberStyles.Float, CultureInfo.InvariantCulture));
        }
    }
}
