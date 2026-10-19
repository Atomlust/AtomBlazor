using AtomBlazor.Primitives;
using AtomBlazor.Primitives.Utilities;
using Xunit;

namespace AtomBlazor.Tests.Utilities;

/// <summary>
/// InlineStyleMerge 的行为测试：组件计算样式与消费者样式的合并次序，
/// 以及供 @attributes 展开用的去 style 副本。
/// （内部类型，经 InternalsVisibleTo 对测试工程可见。）
/// </summary>
public class InlineStyleMergeTests
{
    // ------------------------------------------------------------------
    // Merge
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("position: fixed", "color: red", "position: fixed; color: red")]
    [InlineData("position: fixed", "color: red;", "position: fixed; color: red;")]
    public void 两侧都有样式时组件样式在前消费者样式在后(string componentStyle, string consumerStyle, string expected)
    {
        var attributes = new Dictionary<string, object> { ["style"] = consumerStyle };

        Assert.Equal(expected, InlineStyleMerge.Merge(componentStyle, attributes));
    }

    [Fact]
    public void 只有组件样式时原样返回组件样式()
    {
        Assert.Equal("top: 0", InlineStyleMerge.Merge("top: 0", null));
        Assert.Equal("top: 0", InlineStyleMerge.Merge("top: 0", new Dictionary<string, object>()));
    }

    [Fact]
    public void 只有消费者样式时原样返回消费者样式()
    {
        var attributes = new Dictionary<string, object> { ["style"] = "color: red" };

        Assert.Equal("color: red", InlineStyleMerge.Merge(null, attributes));
        Assert.Equal("color: red", InlineStyleMerge.Merge(string.Empty, attributes));
    }

    [Fact]
    public void 两侧皆空时返回null()
    {
        Assert.Null(InlineStyleMerge.Merge(null, null));
        Assert.Null(InlineStyleMerge.Merge("", new Dictionary<string, object>()));
        Assert.Null(InlineStyleMerge.Merge(null, new Dictionary<string, object> { ["style"] = "" }));
    }

    [Fact]
    public void 消费者style值为null时视为缺省()
    {
        var attributes = new Dictionary<string, object> { ["style"] = null! };

        Assert.Equal("top: 0", InlineStyleMerge.Merge("top: 0", attributes));
    }

    [Fact]
    public void 非字符串的style值经ToString参与合并()
    {
        // Merge 只取 style?.ToString()：任何装箱值（如字符串）都按文本处理。
        object consumer = "z-index: 5";
        var attributes = new Dictionary<string, object> { ["style"] = consumer };

        Assert.Equal("opacity: 1; z-index: 5", InlineStyleMerge.Merge("opacity: 1", attributes));
    }

    // ------------------------------------------------------------------
    // WithoutStyle
    // ------------------------------------------------------------------

    [Fact]
    public void WithoutStyle应返回移除了style的副本并保留其余键()
    {
        var attributes = new Dictionary<string, object>
        {
            ["class"] = "card",
            ["style"] = "color: red",
            ["id"] = "a1",
        };

        var filtered = InlineStyleMerge.WithoutStyle(attributes);

        Assert.NotNull(filtered);
        Assert.False(filtered!.ContainsKey("style"));
        Assert.Equal("card", filtered["class"]);
        Assert.Equal("a1", filtered["id"]);
        // 不得改动原字典
        Assert.True(attributes.ContainsKey("style"));
    }

    [Fact]
    public void WithoutStyle在不含style时应返回原引用()
    {
        var attributes = new Dictionary<string, object> { ["class"] = "card" };

        Assert.Same(attributes, InlineStyleMerge.WithoutStyle(attributes));
    }

    [Fact]
    public void WithoutStyle在仅剩style时应返回null()
    {
        var attributes = new Dictionary<string, object> { ["style"] = "color: red" };

        Assert.Null(InlineStyleMerge.WithoutStyle(attributes));
    }

    [Fact]
    public void WithoutStyle对null输入返回null() =>
        Assert.Null(InlineStyleMerge.WithoutStyle(null));

    // ------------------------------------------------------------------
    // PrimitiveBase 集成（受保护成员即上面两个操作的实例形态）
    // ------------------------------------------------------------------

    [Fact]
    public void PrimitiveBase实例成员应与静态实现同义()
    {
        var component = new StyleProbe();
        component.SetAttributes(new Dictionary<string, object> { ["style"] = "color: red", ["class"] = "probe" });

        Assert.Equal("top: 0; color: red", component.CallMergeStyles("top: 0"));

        var filtered = component.CallFilteredAttributes;
        Assert.NotNull(filtered);
        Assert.False(filtered!.ContainsKey("style"));
        Assert.Equal("probe", filtered["class"]);
    }

    /// <summary>暴露 PrimitiveBase 受保护成员的探针子类。</summary>
    private sealed class StyleProbe : PrimitiveBase
    {
        // AdditionalAttributes 是 [Parameter]，组件外部直接赋值会触发 BL0005；
        // 在子类内部赋值既合法也如实模拟渲染器写入参数的时序。
        public void SetAttributes(Dictionary<string, object> attributes) => AdditionalAttributes = attributes;

        public string? CallMergeStyles(string? computedStyle) => MergeStyles(computedStyle);

        public Dictionary<string, object>? CallFilteredAttributes => FilteredAttributes;
    }
}
