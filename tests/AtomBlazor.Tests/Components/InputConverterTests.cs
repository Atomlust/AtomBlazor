using AtomBlazor.Primitives;
using System.Globalization;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// InputConverter 三级解析链测试：内置类型 round-trip（string/int/long/double/decimal/bool/
/// DateTime/DateOnly/TimeOnly/Guid 及可空代表）、解析失败异常类型（FormatException 与
/// 未注册类型的 InvalidOperationException）、实例函数优先于全局函数、空串回退默认值、
/// GlobalCulture 与实例 Culture 的解析顺序、SetWithFormat 的格式化与回退语义。
/// 全部静态全局状态在 try/finally 中复位；全局链仅使用测试专用类型与未被其他家族
/// 并行使用的类型，保证测试并行安全。
/// </summary>
public class InputConverterTests
{
    /// <summary>
    /// 测试专用目标类型：没有内置转换器，用于隔离地验证全局注册链，
    /// 避免与并行运行的组件测试争用内建类型上的静态状态。
    /// </summary>
    internal sealed record MeterReading(int Value);

    /// <summary>固定区域文化的实例转换器，消除宿主机文化对断言的影响。</summary>
    private static InputConverter<T> Invariant<T>() => new() { Culture = CultureInfo.InvariantCulture };

    // ------------------------------------------------------------------
    // 内置类型 round-trip
    // ------------------------------------------------------------------

    [Fact]
    public void Int与可空Int应完成往返并处理空串()
    {
        var converter = Invariant<int>();
        Assert.Equal("42", converter.Set(42));
        Assert.Equal(42, converter.Get("42"));
        // 空串回退默认值：非空目标类型回退 0。
        Assert.Equal(0, converter.Get(""));

        var nullable = Invariant<int?>();
        Assert.Null(nullable.Set(null));
        Assert.Equal(42, nullable.Get("42"));
        // 可空目标类型回退 null。
        Assert.Null(nullable.Get(""));
    }

    [Fact]
    public void LongDouble与Decimal应完成往返()
    {
        var longConverter = Invariant<long>();
        Assert.Equal("9000000000", longConverter.Set(9000000000L));
        Assert.Equal(9000000000L, longConverter.Get("9000000000"));

        var doubleConverter = Invariant<double?>();
        Assert.Equal("1.5", doubleConverter.Set(1.5));
        Assert.Equal(1.5, doubleConverter.Get("1.5"));

        var decimalConverter = Invariant<decimal>();
        Assert.Equal("1.25", decimalConverter.Set(1.25m));
        Assert.Equal(1.25m, decimalConverter.Get("1.25"));
    }

    [Fact]
    public void Bool应完成往返并拒绝非布尔文本()
    {
        var converter = Invariant<bool>();
        Assert.Equal("True", converter.Set(true));
        Assert.True(converter.Get("true"));
        Assert.False(converter.Get("False"));
        Assert.Throws<FormatException>(() => converter.Get("yes"));
    }

    [Fact]
    public void 日期时间家族应经Set输出再Get还原()
    {
        var dateTime = Invariant<DateTime>();
        var dt = new DateTime(2026, 3, 4, 12, 30, 45);
        Assert.Equal(dt, dateTime.Get(dateTime.Set(dt)!));

        var dateOnly = Invariant<DateOnly>();
        var d = new DateOnly(2026, 3, 4);
        Assert.Equal(d, dateOnly.Get(dateOnly.Set(d)!));

        var timeOnly = Invariant<TimeOnly>();
        var t = new TimeOnly(14, 30);
        Assert.Equal(t, timeOnly.Get(timeOnly.Set(t)!));
    }

    [Fact]
    public void Guid应完成往返()
    {
        var converter = Invariant<Guid?>();
        var guid = Guid.Parse("8f1fb3a4-2c6d-4e1f-9a7b-0d3c5e7f9a11");
        Assert.Equal(guid.ToString(), converter.Set(guid));
        Assert.Equal(guid, converter.Get(guid.ToString()));
    }

    [Fact]
    public void String应直通且空串回退null()
    {
        var converter = Invariant<string>();
        Assert.Equal("abc", converter.Get("abc"));
        Assert.Equal("abc", converter.Set("abc"));
        // string 目标类型的 Value 语义上可空：以 null! 越过可空性标注。
        Assert.Null(converter.Set(null!));
        Assert.Null(converter.Get(""));
    }

    // ------------------------------------------------------------------
    // 失败路径
    // ------------------------------------------------------------------

    [Fact]
    public void 非法文本应抛出FormatException()
    {
        Assert.Throws<FormatException>(() => Invariant<int>().Get("abc"));
        Assert.Throws<FormatException>(() => Invariant<decimal?>().Get("1.2.3"));
        Assert.Throws<FormatException>(() => Invariant<Guid>().Get("not-a-guid"));
    }

    [Fact]
    public void 未注册类型应抛出InvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Invariant<MeterReading>().Get("x"));
        Assert.Contains("No converter registered", ex.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // 解析链优先级：实例 → 全局 → 内置
    // ------------------------------------------------------------------

    [Fact]
    public void 实例函数应优先于全局函数而全局应优先于内置()
    {
        try
        {
            InputConverter<MeterReading>.GlobalGetFunc = _ => new MeterReading(-1);
            InputConverter<MeterReading>.GlobalSetFunc = _ => "global";

            // 测试专用类型没有内置转换器：全局注册即接管。
            Assert.Equal(new MeterReading(-1), Invariant<MeterReading>().Get("x"));
            Assert.Equal("global", Invariant<MeterReading>().Set(new MeterReading(1)));

            // 实例级覆盖全局级。
            var instance = new InputConverter<MeterReading>
            {
                GetFunc = _ => new MeterReading(1),
                SetFunc = _ => "instance",
            };
            Assert.Equal(new MeterReading(1), instance.Get("x"));
            Assert.Equal("instance", instance.Set(new MeterReading(1)));
        }
        finally
        {
            InputConverter<MeterReading>.GlobalGetFunc = null;
            InputConverter<MeterReading>.GlobalSetFunc = null;
        }

        // 复位后回到内置链：测试专用类型重新不可解析。
        Assert.Throws<InvalidOperationException>(() => Invariant<MeterReading>().Get("x"));
    }

    [Fact]
    public void 内建类型的实例函数应优先于内置默认()
    {
        var converter = new InputConverter<int> { GetFunc = s => s.Length, SetFunc = v => $"#{v}" };
        Assert.Equal(3, converter.Get("abc"));
        Assert.Equal("#42", converter.Set(42));
    }

    [Fact]
    public void 区域文化解析顺序应为实例优先于全局()
    {
        try
        {
            InputConverter<float>.GlobalCulture = new CultureInfo("de-DE");

            // 无实例 Culture 时采用全局文化：de-DE 用逗号作小数点。
            Assert.Equal(1234.5f, new InputConverter<float>().Get("1234,5"));

            // 实例 Culture 覆盖全局文化。
            var instance = new InputConverter<float> { Culture = CultureInfo.InvariantCulture };
            Assert.Equal(1234.5f, instance.Get("1234.5"));
        }
        finally
        {
            InputConverter<float>.GlobalCulture = null;
        }
    }

    // ------------------------------------------------------------------
    // SetWithFormat
    // ------------------------------------------------------------------

    [Fact]
    public void SetWithFormat应按格式串格式化并回退Set()
    {
        Assert.Equal("1,234.50", Invariant<decimal>().SetWithFormat(1234.5m, "N2"));
        Assert.Equal("2026-01-02", Invariant<DateTime>().SetWithFormat(new DateTime(2026, 1, 2), "yyyy-MM-dd"));

        // null 值直接返回 null，不进入格式化。
        Assert.Null(Invariant<int?>().SetWithFormat(null, "N2"));

        // 未实现 IFormattable 的类型回退到 Set：string 即直通。
        Assert.Equal("abc", Invariant<string>().SetWithFormat("abc", "N2"));
    }
}
