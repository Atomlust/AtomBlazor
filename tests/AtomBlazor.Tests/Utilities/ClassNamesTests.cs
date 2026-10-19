using AtomBlazor.Primitives;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Utilities;

/// <summary>
/// ClassNames.cn 的行为测试：TailwindMerge 冲突消解语义、空值处理、
/// 条件拼接，以及 <c>atom:</c> 库前缀的剥离-合并-还原流程（#496/#527 约定）。
/// </summary>
public class ClassNamesTests
{
    private static readonly string[] pair = ["a", "b"];

    // ------------------------------------------------------------------
    // TailwindMerge 冲突消解语义
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("px-4 px-2", "px-2")]                               // 同类冲突，后者胜出
    [InlineData("p-4 pr-10", "p-4 pr-10")]                          // 长写细化简写，两者保留
    [InlineData("pr-10 p-4", "p-4")]                                 // 在先长写被后来的简写覆盖
    [InlineData("md:p-4 md:p-8", "md:p-8")]                          // 冲突按变体隔离
    [InlineData("text-red-500 text-blue-500", "text-blue-500")]      // 调色板文字色互斥
    [InlineData("font-bold font-normal", "font-normal")]             // 字重互斥
    [InlineData("flex block", "block")]                              // display 组互斥
    [InlineData("btn active", "btn active")]                         // 未知类原样保留
    public void 合并应遵循Tailwind冲突消解语义(string input, string expected) =>
        Assert.Equal(expected, ClassNames.cn(input));

    [Theory]
    [InlineData("px-4", "px-2", "px-2")]
    [InlineData("btn", "px-4", "btn px-4")]
    public void 两个字符串参数的冲突消解(string first, string second, string expected) =>
        Assert.Equal(expected, ClassNames.cn(first, second));

    // ------------------------------------------------------------------
    // 库前缀剥离-合并-还原
    // ------------------------------------------------------------------

    [Fact]
    public void 使用者的裸类应替换库的带前缀同属性类() =>
        Assert.Equal("atom:rounded-md p-6", ClassNames.cn("atom:p-4 atom:rounded-md", "p-6"));

    [Fact]
    public void 带前缀类之间照常互相冲突消解并保持前缀() =>
        Assert.Equal("atom:p-8", ClassNames.cn("atom:p-4", "atom:p-8"));

    [Fact]
    public void 裸输入不带前缀时合并不引入前缀() =>
        Assert.Equal("px-2", ClassNames.cn("px-4", "px-2"));

    [Fact]
    public void 存活者按其最后一次出现的形态还原()
    {
        // 库先出 atom:text-lg，使用者用裸 text-sm 覆盖——存活的是使用者的形态。
        Assert.Equal("text-sm", ClassNames.cn("atom:text-lg", "text-sm"));
        // 反过来，最后一次出现是带前缀形态时，存活者保持带前缀。
        Assert.Equal("atom:text-sm", ClassNames.cn("text-lg", "atom:text-sm"));
    }

    // ------------------------------------------------------------------
    // group/peer 裸标记伴发
    // ------------------------------------------------------------------

    [Fact]
    public void 带前缀的group标记应伴发裸形态供使用者构建识别() =>
        Assert.Equal("group/row atom:group/row atom:p-4", ClassNames.cn("atom:group/row", "atom:p-4"));

    [Fact]
    public void 带前缀的peer标记应伴发裸形态() =>
        Assert.Equal("peer atom:peer", ClassNames.cn("atom:peer"));

    // ------------------------------------------------------------------
    // 空值与非法输入处理
    // ------------------------------------------------------------------

    [Fact]
    public void 无输入时返回空串() => Assert.Equal(string.Empty, ClassNames.cn());

    [Fact]
    public void null与空白字符串被忽略()
    {
        Assert.Equal("a b", ClassNames.cn("a", null, "  ", "b"));
        Assert.Equal(string.Empty, ClassNames.cn(null, null));
    }

    [Fact]
    public void 布尔值被忽略而条件假分支产生的false被丢弃()
    {
        // C# 没有 JS 的 `condition && "class"`；等价写法把 string 或 bool 装箱后传入，
        // 两种形态都要能被 cn 正确处理。
        const bool isActive = false;
        var onFalse = isActive ? (object)"b" : false;
        Assert.Equal("a c", ClassNames.cn("a", onFalse, "c"));

        const bool on = true;
        var onTrue = on ? (object)"active" : false;
        Assert.Equal("a active c", ClassNames.cn("a", onTrue, "c"));

        // 显式传入的布尔值本身不产生类名。
        Assert.Equal("a", ClassNames.cn("a", true));
    }

    [Fact]
    public void 数组与可枚举被递归展开()
    {
        Assert.Equal("a b c", ClassNames.cn(pair, "c"));
        Assert.Equal("a b", ClassNames.cn(new List<string> { "a", "b" }));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("expression(foo)")]
    [InlineData("url(evil)")]
    [InlineData("import('evil')")]
    [InlineData("class\"><script>")]
    public void 潜在注入形态的类名被拒绝(string malicious) =>
        Assert.Equal("safe", ClassNames.cn("safe", malicious));

    [Fact]
    public void 超长类名被拒绝()
    {
        var tooLong = new string('a', 201);
        Assert.Equal("ok", ClassNames.cn("ok", tooLong));
    }

    [Fact]
    public void 任意值与变体类被保留() =>
        Assert.Equal("w-1/2 data-[state=open]:block", ClassNames.cn("w-1/2", "data-[state=open]:block"));

    // ------------------------------------------------------------------
    // when 条件辅助
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(true, "btn-active")]
    [InlineData(false, null)]
    public void when按条件返回类或null(bool condition, string? expected) =>
        Assert.Equal(expected, ClassNames.when(condition, "btn-active"));

    [Fact]
    public void when可直接嵌入cn完成条件拼接()
    {
        Assert.Equal("btn active px-4", ClassNames.cn("btn", ClassNames.when(true, "active"), "px-4"));
        Assert.Equal("btn px-4", ClassNames.cn("btn", ClassNames.when(false, "active"), "px-4"));
    }

    [Fact]
    public void 多类字符串按空白拆分后参与合并() =>
        Assert.Equal("btn-hidden px-2", ClassNames.cn("btn-hidden px-4", "px-2"));
}
