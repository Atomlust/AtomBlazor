using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

using ToggleGroup = AtomBlazor.Primitives.ToggleGroup<string>;
using ToggleItem = AtomBlazor.Primitives.ToggleGroupItem<string>;
using PrimitiveToggleType = AtomBlazor.Primitives.ToggleGroupType;

namespace AtomBlazor.Tests.Rendering;

/// <summary>
/// 只在渲染产物里才显露的可访问性缺陷：<c>aria-describedby</c>
/// 指向无人写入的 id、状态属性没有值、按键被处理了两次。
/// </summary>
public class AriaAndKeyboardTests
{
    /// <summary>
    /// 每个 FormField* 控件都把 <c>aria-describedby</c> 指向辅助文本，而
    /// FieldDescription 收了 <c>Id</c> 却从不渲染它——引用于是落空，
    /// 读屏软件完全读不到描述。
    /// </summary>
    [Fact]
    public async Task HelperTextIdIsActuallyRendered()
    {
        var markup = await Render<FormFieldCheckbox>(new()
        {
            [nameof(FormFieldCheckbox.HelperText)] = "Pick one"
        });

        var described = Regex.Match(markup, @"aria-describedby=""(?<id>[^""]+)""");
        Assert.True(described.Success, $"No aria-describedby was rendered:\n{markup}");
        Assert.Contains($"id=\"{described.Groups["id"].Value}\"", markup, StringComparison.Ordinal);
    }

    /// <summary>错误文本同理，它用的是自己的 id。</summary>
    [Fact]
    public async Task ErrorTextIdIsActuallyRendered()
    {
        var markup = await Render<FormFieldCheckbox>(new()
        {
            [nameof(FormFieldCheckbox.ErrorText)] = "Required"
        });

        var described = Regex.Match(markup, @"aria-describedby=""(?<id>[^""]+)""");
        Assert.True(described.Success, $"No aria-describedby was rendered:\n{markup}");
        Assert.Contains($"id=\"{described.Groups["id"].Value}\"", markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// 状态属性取字符串 "true" 与 "false"。绑到 bool 上时，Blazor 会在
    /// true 时渲染空值属性、false 时整个丢掉。
    /// </summary>
    [Fact]
    public async Task CollapsibleReportsItsExpandedStateAsAValue()
    {
        var closed = await Render<AtomBlazor.Primitives.CollapsiblePrimitive>(new()
        {
            [nameof(AtomBlazor.Primitives.CollapsiblePrimitive.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<AtomBlazor.Primitives.CollapsibleTrigger>(0);
                builder.CloseComponent();
                builder.OpenComponent<AtomBlazor.Primitives.CollapsibleContent>(1);
                builder.AddAttribute(2, "ForceMount", true);
                builder.CloseComponent();
            })
        });

        Assert.Contains("aria-expanded=\"false\"", closed, StringComparison.Ordinal);
        Assert.Contains("aria-hidden=\"true\"", closed, StringComparison.Ordinal);
    }

    /// <summary>
    /// 只许单选的切换组本质是单选按钮组，其条目应带
    /// <c>aria-checked</c>；原先带的 <c>aria-pressed</c> 属于按钮。
    /// </summary>
    [Fact]
    public async Task SingleSelectToggleGroupIsARadioGroup()
    {
        var markup = await Render<ToggleGroup>(new()
        {
            [nameof(ToggleGroup.Type)] = PrimitiveToggleType.Single,
            [nameof(ToggleGroup.DefaultValue)] = "a",
            [nameof(ToggleGroup.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<ToggleItem>(0);
                builder.AddAttribute(1, nameof(ToggleItem.Value), "a");
                builder.CloseComponent();
            })
        });

        Assert.Contains("role=\"radiogroup\"", markup, StringComparison.Ordinal);
        Assert.Contains("role=\"radio\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-checked=\"true\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-pressed", markup, StringComparison.Ordinal);
    }

    /// <summary>多选组保留按钮角色，连带保留 aria-pressed。</summary>
    [Fact]
    public async Task MultiSelectToggleGroupKeepsTheButtonRole()
    {
        var markup = await Render<ToggleGroup>(new()
        {
            [nameof(ToggleGroup.Type)] = PrimitiveToggleType.Multiple,
            [nameof(ToggleGroup.Values)] = new List<string> { "a" },
            [nameof(ToggleGroup.ValuesChanged)] = EventCallback.Factory.Create<List<string>>(this, _ => { }),
            [nameof(ToggleGroup.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<ToggleItem>(0);
                builder.AddAttribute(1, nameof(ToggleItem.Value), "a");
                builder.CloseComponent();
            })
        });

        Assert.Contains("role=\"group\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-pressed=\"true\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-checked", markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// 单选按钮组曾抑制所有按键的默认行为，Tab 因此无法把焦点移出去。
    /// 只有它接管的箭头键可以被抑制，且只在按住期间。
    /// </summary>
    [Fact]
    public async Task RadioGroupDoesNotSwallowEveryKey()
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<RadioGroup<string>>([]);
            Assert.DoesNotContain(preventDefault, renderer.Markup(), StringComparison.Ordinal);

            // 没有可移动条目时，箭头键同样不得开始抑制 Tab。
            await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = "Tab" });
            Assert.DoesNotContain(preventDefault, renderer.Markup(), StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// 开关是原生按钮，按空格时浏览器会自行补发一次 click。若同时
    /// 也处理按键，切换就会发生两次，一次按压结束后恰好在起点。
    /// </summary>
    [Fact]
    public async Task SpaceOnTheSwitchTogglesOnce()
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var toggles = 0;
            var isChecked = false;

            await renderer.MountAsync<AtomBlazor.Primitives.SwitchPrimitive>(new()
            {
                [nameof(AtomBlazor.Primitives.SwitchPrimitive.Checked)] = isChecked,
                [nameof(AtomBlazor.Primitives.SwitchPrimitive.CheckedChanged)] =
                    EventCallback.Factory.Create<bool>(this, value =>
                    {
                        isChecked = value;
                        toggles++;
                    })
            });

            // 聚焦按钮上按一次空格浏览器会做什么：先 keydown，
            // 再于抬起时补发它自己的 click。
            await DispatchIfPresent(renderer, "onkeydown", new KeyboardEventArgs { Key = " " });
            await renderer.DispatchAsync("onclick", new MouseEventArgs());

            Assert.Equal(1, toggles);
            Assert.True(isChecked);
        });
    }

    /// <summary>
    /// 悬浮卡会写入弹出的方位，滑入动画据此匹配。它此前写的是
    /// 枚举名 "Bottom"，而每个选择器找的都是 "bottom"。
    /// </summary>
    [Fact]
    public async Task HoverCardWritesItsSideInLowerCase()
    {
        var markup = await Render<HoverCard>(new()
        {
            [nameof(HoverCard.DefaultOpen)] = true,
            [nameof(HoverCard.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<HoverCardContent>(0);
                builder.CloseComponent();
            })
        });

        Assert.DoesNotContain("data-side=\"Bottom\"", markup, StringComparison.Ordinal);
    }

    /// <summary>渲染期对 key-down 设 <c>preventDefault</c> 时 Blazor 发出的属性名。</summary>
    private const string preventDefault = "__internal_preventDefault_onkeydown";

    private static async Task DispatchIfPresent(ComponentTestRenderer renderer, string attribute, EventArgs args)
    {
        try
        {
            await renderer.DispatchAsync(attribute, args);
        }
        catch (InvalidOperationException)
        {
            // 已没有任何处理器监听这个键——这正是本次修复的目的。
        }
    }

    private static async Task<string> Render<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        var markup = string.Empty;

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<T>(parameters);
            markup = renderer.Markup();
        });

        return markup;
    }

    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddAtomBlazorComponents();
        return services;
    }
}
