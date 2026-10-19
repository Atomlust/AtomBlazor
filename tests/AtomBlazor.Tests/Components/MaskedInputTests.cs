using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// MaskedInput 掩码输入测试：掩码预设目录（电话/社保号/信用卡等九项的模式、占位、
/// 输入方式）表驱动、MaskProcessor 规格化与清洗（短输入补占位、超长截断、输入内字面量跳过、
/// 字母/数字/混合位校验、去掩码回读、可编辑位置导航、ProcessInput 光标）、组件渲染快照与
/// 预设属性沿、键入清洗经 maskedInput.setInputValue 原子回写与光标、聚焦铺空掩码、失焦
/// 按 ShowMask 清理、barrel 导入与 compositionGuard.attach/detach、MaskedValue/UnmaskedValue
/// 公开读取。快照经正则归一化随机 id。
/// headless 限制先例：ElementReference.FocusAsync() 抛 InvalidOperationException，
/// 不测纯焦点沿（本组件焦点沿经事件派发与 JS 调用断言，不经真实聚焦）。
/// </summary>
public class MaskedInputTests
{
    /// <summary>
    /// 归一化组件自生成的 masked-xxxxxxxx 随机短 id，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "masked-[0-9a-f]{8}", "masked-<id>");

    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js);
    }

    /// <summary>让调度器空转两轮，冲刷 OnAfterRender/StateHasChanged 队列。</summary>
    private static async Task FlushAsync(ComponentTestRenderer renderer)
    {
        for (var i = 0; i < 2; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, MaskedInput Input)> MountAsync(
        Dictionary<string, object?> parameters)
    {
        var (renderer, js) = CreateRenderer();
        var component = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.MountTypeAsync(typeof(MaskedInput), parameters));
        await FlushAsync(renderer);
        return (renderer, js, (MaskedInput)component);
    }

    /// <summary>在渲染树里按序派发一个 DOM 事件。</summary>
    private static Task DispatchAsync(
        ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync(attributeName, args, occurrence));

    /// <summary>反射读取 interop 记录里匿名对象的属性。</summary>
    private static object? Prop(object? target, string name) =>
        target?.GetType().GetProperty(name)?.GetValue(target);

    // ---------------- 预设目录（表驱动） ----------------

    [Theory]
    [InlineData(MaskPreset.Phone, "(999) 999-9999", "(___) ___-____", "tel")]
    [InlineData(MaskPreset.SSN, "999-99-9999", "___-__-____", "numeric")]
    [InlineData(MaskPreset.CreditCard, "9999 9999 9999 9999", "____ ____ ____ ____", "numeric")]
    [InlineData(MaskPreset.Date, "99/99/9999", "__/__/____", "numeric")]
    [InlineData(MaskPreset.Time, "99:99", "__:__", "numeric")]
    [InlineData(MaskPreset.Time24, "99:99:99", "__:__:__", "numeric")]
    [InlineData(MaskPreset.ZipCode, "99999-9999", "_____-____", "numeric")]
    [InlineData(MaskPreset.EIN, "99-9999999", "__-_______", "numeric")]
    [InlineData(MaskPreset.Custom, "", "", "text")]
    public void 预设目录应给出模式占位与输入方式(MaskPreset preset, string pattern, string placeholder, string inputMode)
    {
        Assert.Equal(pattern, MaskDefinitions.GetPattern(preset));
        Assert.Equal(placeholder, MaskDefinitions.GetPlaceholder(preset));
        Assert.Equal(inputMode, MaskDefinitions.GetInputMode(preset));
    }

    // ---------------- MaskProcessor 规格化/清洗/边界 ----------------

    [Theory]
    [InlineData("8005551212", "(800) 555-1212")] // 满位
    [InlineData("800", "(800) ___-____")] // 短输入补占位
    [InlineData("", "(___) ___-____")] // 空输入即空掩码
    [InlineData("80055512129999", "(800) 555-1212")] // 超长截断
    [InlineData("(800) 5", "(800) 5__-____")] // 输入内字面量被跳过
    public void 电话掩码应规格化短输入超长输入与内嵌字面量(string raw, string expected)
    {
        var processor = new MaskProcessor(MaskDefinitions.GetPattern(MaskPreset.Phone));
        Assert.Equal(expected, processor.ApplyMask(raw));
    }

    [Theory]
    [InlineData("AAA-999", "abc-12", "abc-12_")] // 字母位吃满、数字段短输入补占位
    [InlineData("AAA-999", "12ab", "ab_-___")] // 数字被字母位跳过
    [InlineData("9A", "1a", "1a")] // 数字位与字母位各归其位
    [InlineData("**-99", "a1-23", "a1-23")] // 混合位吃字母数字，输入内字面量被跳过
    public void 字母数字与混合位应逐位匹配(string mask, string raw, string expected)
    {
        var processor = new MaskProcessor(mask);
        Assert.Equal(expected, processor.ApplyMask(raw));
    }

    [Fact]
    public void 去掩码应回读原始字符并忽略字面量与占位()
    {
        var processor = new MaskProcessor("(999) 999-9999");
        Assert.Equal("8005551212", processor.GetUnmaskedValue("(800) 555-1212"));
        Assert.Equal("800", processor.GetUnmaskedValue("(800) ___-____"));
        Assert.Equal("8005551212", processor.GetUnmaskedValue("(800) 555-12129999")); // 超出掩码长度部分被丢弃
        Assert.Equal(string.Empty, processor.GetUnmaskedValue(null));
        Assert.Equal("(___) ___-____", processor.GetEmptyMask());
        Assert.Equal("(999) 999-9999", processor.Mask);
        Assert.Equal('_', processor.PlaceholderChar);
    }

    [Theory]
    [InlineData('9', true)]
    [InlineData('A', true)]
    [InlineData('*', true)]
    [InlineData('-', false)]
    [InlineData('(', false)]
    [InlineData('z', false)]
    public void 掩码字符判定应只认九与A与星号(char c, bool expected) =>
        Assert.Equal(expected, MaskProcessor.IsMaskChar(c));

    [Fact]
    public void 位置校验与字面量判定应覆盖越界回退()
    {
        var processor = new MaskProcessor("99-99");
        Assert.True(processor.IsValidChar('7', 0));
        Assert.False(processor.IsValidChar('a', 0));
        Assert.True(processor.IsValidChar('-', 2)); // 字面量精确匹配
        Assert.False(processor.IsValidChar('x', 2));
        Assert.False(processor.IsValidChar('7', -1)); // 越界
        Assert.False(processor.IsValidChar('7', 5));
        Assert.True(processor.IsLiteral(2));
        Assert.False(processor.IsLiteral(0));
        Assert.False(processor.IsLiteral(99)); // 越界非字面量
        Assert.Equal(0, processor.GetNextEditablePosition(0));
        Assert.Equal(3, processor.GetNextEditablePosition(2));
        Assert.Equal(4, processor.GetNextEditablePosition(4));
        Assert.Equal(-1, processor.GetNextEditablePosition(5));
        Assert.Equal(1, processor.GetPrevEditablePosition(2));
        Assert.Equal(-1, processor.GetPrevEditablePosition(0));
    }

    [Fact]
    public void 自定义占位字符应贯穿空掩码与回读()
    {
        var processor = new MaskProcessor("(999) 999-9999", '#');
        Assert.Equal("(###) ###-####", processor.GetEmptyMask());
        Assert.Equal("800", processor.GetUnmaskedValue("(800) ###-####"));
        Assert.Equal("(800) ###-####", processor.ApplyMask("800"));
    }

    [Fact]
    public void ProcessInput应返回掩码值并给出光标落点()
    {
        var processor = new MaskProcessor("(999) 999-9999");

        var (masked, cursor) = processor.ProcessInput(string.Empty, "8005551212", 0);
        Assert.Equal("(800) 555-1212", masked);
        Assert.Equal(14, cursor); // 满位后停在掩码末端

        (masked, cursor) = processor.ProcessInput(string.Empty, "12", 0);
        Assert.Equal("(12_) ___-____", masked);
        Assert.Equal(3, cursor);
    }

    // ---------------- 组件渲染沿 ----------------

    [Fact]
    public async Task 默认自定义掩码渲染快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MaskedInput.Mask)] = "AAA-9999",
            [nameof(MaskedInput.AriaLabel)] = "序列号",
        });

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 电话预设应带tel输入方式与占位并格式化初值()
    {
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MaskedInput.Preset)] = MaskPreset.Phone,
            [nameof(MaskedInput.Value)] = "8005551212",
            [nameof(MaskedInput.Name)] = "phone",
            [nameof(MaskedInput.Disabled)] = true,
            [nameof(MaskedInput.Required)] = true,
        });

        var markup = renderer.Markup();
        Assert.Contains("inputmode=\"tel\"", markup);
        Assert.Contains("placeholder=\"(___) ___-____\"", markup);
        Assert.Contains("value=\"(800) 555-1212\"", markup);
        Assert.Contains("name=\"phone\"", markup);
        Assert.Contains("disabled", markup);
        Assert.Contains("required", markup);
        Assert.Contains("atom:font-mono", markup);
        Assert.Equal("(800) 555-1212", input.MaskedValue);
        Assert.Equal("8005551212", input.UnmaskedValue);
    }

    [Fact]
    public async Task 显式占位文本应覆盖预设占位()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MaskedInput.Preset)] = MaskPreset.CreditCard,
            [nameof(MaskedInput.Placeholder)] = "卡号",
        });

        Assert.Contains("placeholder=\"卡号\"", renderer.Markup());
        Assert.Contains("inputmode=\"numeric\"", renderer.Markup());
    }

    [Fact]
    public async Task 自定义掩码空值应以空掩码作占位()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MaskedInput.Mask)] = "99-9A",
        });

        // 'A' 也是掩码字符：99-9A 的空掩码为 __-__。
        Assert.Contains("placeholder=\"__-__\"", renderer.Markup());
        Assert.Contains("inputmode=\"text\"", renderer.Markup());
    }

    [Fact]
    public async Task 键入应清洗非法字符回写掩码并上报未掩码值()
    {
        var reports = new List<string>();
        var onChanged = EventCallback.Factory.Create<string>(new object(), v => reports.Add(v));

        var (renderer, js, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MaskedInput.Mask)] = "999-9999",
            [nameof(MaskedInput.ValueChanged)] = onChanged,
        });

        // 'a' 不是数字位、'-' 非字母数字被跳过：净得 "123"。
        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "1a2-3" });
        await FlushAsync(renderer);

        Assert.Equal(["123"], reports);
        Assert.Equal("123", input.Value);
        Assert.Equal("123-____", input.MaskedValue);

        var setInput = Assert.Single(js.Invocations, i => i.Identifier == "maskedInput.setInputValue");
        Assert.Equal("123-____", setInput.Args![1]);
        Assert.Equal(4, (int)setInput.Args![2]!); // 越过连停到可编辑位

        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "1234567" });
        await FlushAsync(renderer);
        Assert.Equal("123-4567", input.MaskedValue);
    }

    [Fact]
    public async Task 电话预设满位粘贴式输入应截断到掩码()
    {
        var reports = new List<string>();
        var onChanged = EventCallback.Factory.Create<string>(new object(), v => reports.Add(v));

        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MaskedInput.Preset)] = MaskPreset.Phone,
            [nameof(MaskedInput.ValueChanged)] = onChanged,
        });

        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "(800) 555-1212999" });
        await FlushAsync(renderer);

        // 只采集字母数字且止于 10 个可编辑位。
        Assert.Equal(["8005551212"], reports);
        Assert.Equal("(800) 555-1212", input.MaskedValue);
    }

    [Fact]
    public async Task 非法字符全部被拒时不应改动绑定值()
    {
        var reports = new List<string>();
        var onChanged = EventCallback.Factory.Create<string>(new object(), v => reports.Add(v));

        var (renderer, js, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MaskedInput.Mask)] = "999-9999",
            [nameof(MaskedInput.ValueChanged)] = onChanged,
        });
        js.Invocations.Clear();

        await DispatchAsync(renderer, "oninput", new ChangeEventArgs { Value = "abc" });
        await FlushAsync(renderer);

        Assert.Empty(reports);
        Assert.Equal(string.Empty, input.Value);

        // 回写仍发生：值回到空掩码、光标归零。
        var setInput = Assert.Single(js.Invocations, i => i.Identifier == "maskedInput.setInputValue");
        Assert.Equal("___-____", setInput.Args![1]);
        Assert.Equal(0, (int)setInput.Args![2]!);
    }

    [Fact]
    public async Task 聚焦空值应铺设空掩码并定位首个可编辑位()
    {
        var (renderer, js, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MaskedInput.Preset)] = MaskPreset.Phone,
            [nameof(MaskedInput.Id)] = "masked-fixed",
        });
        js.Invocations.Clear();

        await DispatchAsync(renderer, "onfocus", new FocusEventArgs());
        await FlushAsync(renderer);

        Assert.Equal("(___) ___-____", input.MaskedValue);
        var setInput = Assert.Single(js.Invocations, i => i.Identifier == "maskedInput.setInputValue");
        Assert.Equal("(___) ___-____", setInput.Args![1]);
        Assert.Equal(1, (int)setInput.Args![2]!); // 跳过首括号落在首个 9 上
    }

    [Fact]
    public async Task 失焦在关闭ShowMask时应清空显示()
    {
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MaskedInput.Preset)] = MaskPreset.Phone,
            [nameof(MaskedInput.ShowMask)] = false,
        });

        await DispatchAsync(renderer, "onblur", new FocusEventArgs());
        await FlushAsync(renderer);

        Assert.Equal(string.Empty, input.MaskedValue);
        Assert.Contains("value=\"\"", renderer.Markup());
    }

    [Fact]
    public async Task 挂载应经共享core包一次导入并挂组合守卫()
    {
        var (_, js, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MaskedInput.Preset)] = MaskPreset.SSN,
        });

        var import = Assert.Single(js.Invocations, i => i.Identifier == "import");
        Assert.Contains("components-core.js", (string)import.Args![0]!);

        var attach = Assert.Single(js.Invocations, i => i.Identifier == "compositionGuard.attach");
        var guardId = Assert.IsType<string>(attach.Args![1]);
        Assert.Equal(32, guardId.Length);
        Assert.Equal(["input"], (string[])Prop(attach.Args![2], "suppress")!);
    }

    [Fact]
    public async Task 释放应摘除组合守卫()
    {
        var (renderer, js, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(MaskedInput.Mask)] = "999",
        });

        var guardId = js.Invocations.Single(i => i.Identifier == "compositionGuard.attach").Args![1];

        await renderer.Dispatcher.InvokeAsync(input.DisposeAsync);
        await FlushAsync(renderer);

        var detach = Assert.Single(js.Invocations, i => i.Identifier == "compositionGuard.detach");
        Assert.Equal(guardId, detach.Args![0]);
    }
}
