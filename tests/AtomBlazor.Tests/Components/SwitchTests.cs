using AtomBlazor.Primitives;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Switch 家族测试：原语层 role=switch 与 aria-checked/data-state 的字符串播报、
/// thumb 随上级开关同步的状态、包装层的尺寸变体与位移类（含 rtl 取反）、
/// ThumbContent 的上下文渲染、禁用吞掉交互，以及 EditContext 内的校验沿。
/// </summary>
public class SwitchTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>排空渲染队列，确保 StateHasChanged 的后续批已落定。</summary>
    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    private static Dictionary<string, object?> Params(
        bool? @checked = null,
        EventCallback<bool> checkedChanged = default,
        bool? disabled = null,
        SwitchSize? size = null,
        string? @class = null,
        string? thumbClass = null,
        string? thumbCheckedClass = null,
        string? thumbUncheckedClass = null,
        RenderFragment<bool>? thumbContent = null,
        string? ariaLabel = null,
        string? id = null,
        string? ariaDescribedBy = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (@checked is not null)
        {
            parameters[nameof(Switch.Checked)] = @checked.Value;
        }
        if (checkedChanged.HasDelegate)
        {
            parameters[nameof(Switch.CheckedChanged)] = checkedChanged;
        }
        if (disabled is not null)
        {
            parameters[nameof(Switch.Disabled)] = disabled.Value;
        }
        if (size is not null)
        {
            parameters[nameof(Switch.Size)] = size.Value;
        }
        if (@class is not null)
        {
            parameters[nameof(Switch.Class)] = @class;
        }
        if (thumbClass is not null)
        {
            parameters[nameof(Switch.ThumbClass)] = thumbClass;
        }
        if (thumbCheckedClass is not null)
        {
            parameters[nameof(Switch.ThumbCheckedClass)] = thumbCheckedClass;
        }
        if (thumbUncheckedClass is not null)
        {
            parameters[nameof(Switch.ThumbUncheckedClass)] = thumbUncheckedClass;
        }
        if (thumbContent is not null)
        {
            parameters[nameof(Switch.ThumbContent)] = thumbContent;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(Switch.AriaLabel)] = ariaLabel;
        }
        if (id is not null)
        {
            parameters[nameof(Switch.Id)] = id;
        }
        if (ariaDescribedBy is not null)
        {
            parameters[nameof(Switch.AriaDescribedBy)] = ariaDescribedBy;
        }

        return parameters;
    }

    /// <summary>取开关轨道元素（第一个）的 class 值。</summary>
    private static string TrackClass(string markup) =>
        Regex.Match(markup, @"class=""([^""]*)""").Groups[1].Value;

    /// <summary>取 thumb（带 class 的第一个 span）的 class 值。</summary>
    private static string ThumbClass(string markup) =>
        Regex.Match(markup, @"<span[^>]*?class=""([^""]*)""").Groups[1].Value;

    // ------------------------------------------------------------------
    // 呈现快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 关闭态默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Switch>(Params());
        await SettleAsync(renderer);

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 打开态快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Switch>(Params(@checked: true, id: "notify-switch"));
        await SettleAsync(renderer);

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 开关应播报role为switch且状态取字符串而非布尔()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Switch>(Params());

        var markup = renderer.Markup();
        Assert.Contains("role=\"switch\"", markup);
        Assert.Contains("type=\"button\"", markup);
        Assert.Contains("aria-checked=\"false\"", markup);
        Assert.Contains("data-state=\"unchecked\"", markup);
        Assert.Contains("tabindex=\"0\"", markup);
    }

    [Fact]
    public async Task 打开态应把轨道与滑块及滑块自身都切到checked()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Switch>(Params(@checked: true));

        var markup = renderer.Markup();
        Assert.Contains("aria-checked=\"true\"", markup);
        Assert.Equal("checked", Regex.Match(markup, @"data-state=""([^""]*)""").Groups[1].Value);
        Assert.Contains("atom:bg-primary", TrackClass(markup));
        Assert.Contains("atom:translate-x-5", ThumbClass(markup));
    }

    [Fact]
    public async Task 滑块状态应跟随上级开关而非自身参数()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Switch>(Params(@checked: true));

        var thumbStates = Regex.Matches(renderer.Markup(), @"data-state=""([^""]*)""")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.Equal(["checked", "checked"], thumbStates);
    }

    // ------------------------------------------------------------------
    // 尺寸变体与位移
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(SwitchSize.Small, "atom:h-5 atom:w-9", "atom:h-4 atom:w-4", "atom:translate-x-4")]
    [InlineData(SwitchSize.Medium, "atom:h-6 atom:w-11", "atom:h-5 atom:w-5", "atom:translate-x-5")]
    [InlineData(SwitchSize.Large, "atom:h-7 atom:w-14", "atom:h-6 atom:w-6", "atom:translate-x-7")]
    public async Task 尺寸变体应同时决定轨道滑块与位移(
        SwitchSize size, string trackSize, string thumbSize, string translate)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Switch>(Params(@checked: true, size: size));

        var markup = renderer.Markup();
        Assert.Contains(trackSize, TrackClass(markup));
        Assert.Contains(thumbSize, ThumbClass(markup));
        Assert.Contains(translate, ThumbClass(markup));
    }

    [Fact]
    public async Task 打开态位移应带rtl取反而关闭态归零()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Switch>(Params(@checked: true));
        Assert.Contains("atom:rtl:-translate-x-5", ThumbClass(renderer.Markup()));

        var off = CreateRenderer();
        await off.MountAsync<Switch>(Params());
        var offThumb = ThumbClass(off.Markup());
        Assert.Contains("atom:translate-x-0", offThumb);
        Assert.DoesNotContain("atom:rtl:", offThumb);
    }

    // ------------------------------------------------------------------
    // ThumbContent
    // ------------------------------------------------------------------

    [Fact]
    public async Task ThumbContent应以当前状态为上下文渲染并让滑块改为居中弹性盒()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Switch>(Params(
            @checked: true,
            thumbContent: isChecked => builder =>
            {
                builder.OpenElement(0, "span");
                builder.AddContent(1, isChecked ? "开" : "关");
                builder.CloseElement();
            }));

        var markup = renderer.Markup();
        Assert.Contains(">开</span>", markup);
        Assert.Contains("atom:flex atom:items-center atom:justify-center", ThumbClass(markup));
    }

    [Fact]
    public async Task ThumbContent应随点击后的新状态重渲染()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Switch>(Params(
            @checked: false,
            checkedChanged: EventCallback.Factory.Create<bool>(new object(), _ => { }),
            thumbContent: isChecked => builder => builder.AddContent(0, isChecked ? "on" : "off")));

        Assert.Contains("off", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Contains("on", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 交互沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 点击应拨动状态并上报新值()
    {
        var reports = new List<bool>();
        var callback = EventCallback.Factory.Create<bool>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<Switch>(Params(checkedChanged: callback));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await SettleAsync(renderer);

        Assert.Equal([true], reports);
        Assert.Contains("aria-checked=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task 禁用应吞掉点击并把焦点移出tab序列()
    {
        var reports = new List<bool>();
        var callback = EventCallback.Factory.Create<bool>(new object(), value => reports.Add(value));
        var renderer = CreateRenderer();

        await renderer.MountAsync<Switch>(Params(disabled: true, checkedChanged: callback));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Empty(reports);
        var markup = renderer.Markup();
        Assert.Contains("aria-disabled=\"true\"", markup);
        Assert.Contains("tabindex=\"-1\"", markup);
        Assert.Contains("disabled", markup);
    }

    // ------------------------------------------------------------------
    // 自定义类与无障碍属性落点
    // ------------------------------------------------------------------

    [Fact]
    public async Task 自定义类应追加在基础类之后()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Switch>(Params(@class: "atom:my-switch"));

        Assert.EndsWith("atom:my-switch", TrackClass(renderer.Markup()));
        Assert.Contains("atom:inline-flex", TrackClass(renderer.Markup()));
    }

    [Fact]
    public async Task 滑块基础类应始终领先于状态类与自定义类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Switch>(Params(
            @checked: true,
            thumbClass: "atom:thumb-base",
            thumbCheckedClass: "atom:thumb-on"));

        var thumb = ThumbClass(renderer.Markup());
        Assert.Contains("atom:pointer-events-none", thumb);
        Assert.Contains("atom:thumb-base", thumb);
        Assert.Contains("atom:thumb-on", thumb);
        Assert.DoesNotContain("atom:thumb-off", thumb);
    }

    [Fact]
    public async Task 关闭态应使用ThumbUncheckedClass()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Switch>(Params(
            thumbClass: "atom:thumb-base",
            thumbUncheckedClass: "atom:thumb-off"));

        var thumb = ThumbClass(renderer.Markup());
        Assert.Contains("atom:thumb-off", thumb);
        Assert.DoesNotContain("atom:thumb-on", thumb);
    }

    [Fact]
    public async Task 无障碍属性应落在真正的按钮元素上()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Switch>(Params(
            ariaLabel: "启用通知",
            id: "notify-switch",
            ariaDescribedBy: "notify-help"));

        var button = Regex.Match(renderer.Markup(), @"<button[^>]*>").Value;
        // 组件→原语与 .razor 地写作 Id="@Id"；原语无该参数
        // 未匹配属性经 AdditionalAttributes 抵达按钮时属性名按源样张保留大写 "Id"
        // 故此处断言对大小写宽容（(?i) 内联组），不改组件。
        Assert.Matches("(?i)\\bid=\"notify-switch\"", button);
        Assert.Contains("aria-label=\"启用通知\"", button);
        Assert.Contains("aria-describedby=\"notify-help\"", button);
    }

    [Fact]
    public async Task 提供了CheckedExpression就应播报必填()
    {
        Expression<Func<bool>> expression = () => true;
        var renderer = CreateRenderer();

        await renderer.MountAsync<Switch>(new Dictionary<string, object?>
        {
            [nameof(Switch.Checked)] = true,
            [nameof(Switch.CheckedExpression)] = expression,
        });

        Assert.Contains("aria-required=\"true\"", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 原语层（无样式）
    // ------------------------------------------------------------------

    private static RenderFragment ThumbFragment => builder =>
    {
        builder.OpenElement(0, "span");
        builder.CloseElement();
    };

    [Fact]
    public async Task 原语应只呈现行为与无障碍属性而不带样式类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AtomBlazor.Primitives.SwitchPrimitive>(
            new Dictionary<string, object?>
            {
                [nameof(AtomBlazor.Primitives.SwitchPrimitive.Checked)] = true,
                [nameof(AtomBlazor.Primitives.SwitchPrimitive.ChildContent)] = (RenderFragment)(builder =>
                {
                    // 组件帧必须开合成对，未闭合的 Region 帧会让渲染器判定输出非法。
                    builder.OpenComponent<AtomBlazor.Primitives.SwitchThumb>(0);
                    builder.CloseComponent();
                }),
            });

        var markup = renderer.Markup();
        Assert.Contains("role=\"switch\"", markup);
        Assert.Contains("aria-checked=\"true\"", markup);
        Assert.DoesNotContain("class=", markup);
        Assert.Contains("<span", markup);
    }

    [Fact]
    public async Task 原语结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AtomBlazor.Primitives.SwitchPrimitive>(
            new Dictionary<string, object?>
            {
                // 原语没有 Id/AriaLabel 参数，它们与包装层一样经未匹配属性抵达按钮元素。
                [nameof(AtomBlazor.Primitives.SwitchPrimitive.AdditionalAttributes)] =
                    new Dictionary<string, object> { ["id"] = "headless-switch", ["aria-label"] = "启用通知" },
                [nameof(AtomBlazor.Primitives.SwitchPrimitive.ChildContent)] = (RenderFragment)(builder =>
                {
                    builder.OpenComponent<AtomBlazor.Primitives.SwitchThumb>(0);
                    builder.AddAttribute(1, "ChildContent", ThumbFragment);
                    builder.CloseComponent();
                }),
            });

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 滑块脱离开关时应拒绝渲染()
    {
        var renderer = CreateRenderer();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            renderer.MountAsync<AtomBlazor.Primitives.SwitchThumb>(new Dictionary<string, object?>()));
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿
    // ------------------------------------------------------------------

    /// <summary>校验沿测试模型：通知开关必须打开。</summary>
    internal sealed class Settings
    {
        public bool Notifications { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：语义与真实 DataAnnotationsValidator 一致——
    /// 未打开即写入消息；同时记录 OnFieldChanged 次数，供断言字段通知沿。
    /// </summary>
    private static (EditContext Context, List<int> FieldChanges) CreateValidatedContext(Settings settings)
    {
        var context = new EditContext(settings);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(settings, nameof(Settings.Notifications));
        var fieldChanges = new List<int>();

        void Validate()
        {
            messages.Clear();
            if (!settings.Notifications)
            {
                messages.Add(field, "请打开通知");
            }

            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) =>
        {
            fieldChanges.Add(1);
            Validate();
        };
        context.OnValidationRequested += (_, _) => Validate();

        return (context, fieldChanges);
    }

    private static RenderFragment SwitchFragment(Settings settings) => builder =>
    {
        Expression<Func<bool>> checkedExpression = () => settings.Notifications;

        builder.OpenComponent<Switch>(0);
        builder.AddAttribute(1, nameof(Switch.Checked), settings.Notifications);
        builder.AddAttribute(2, nameof(Switch.CheckedExpression), checkedExpression);
        builder.AddAttribute(3, nameof(Switch.CheckedChanged),
            EventCallback.Factory.Create<bool>(settings, value => settings.Notifications = value));
        builder.CloseComponent();
    };

    [Fact]
    public async Task 校验沿应播报无效态并在拨动后通知字段变化()
    {
        var settings = new Settings();
        var (context, fieldChanges) = CreateValidatedContext(settings);
        var renderer = CreateRenderer();

        // 初始播报依赖已存在的校验消息：真实 EditForm 里由首帧后的校验轮产生
        // 这里在挂载前显式请求一次（OnValidationRequested 与 DataAnnotationsValidator 语义相同）。
        await context.ValidateAsync();

        var hostParams = new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = SwitchFragment(settings),
        };
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(hostParams);
        await SettleAsync(renderer);

        Assert.Contains("aria-invalid=\"true\"", renderer.Markup());
        Assert.Contains("aria-required=\"true\"", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await SettleAsync(renderer);

        Assert.True(settings.Notifications);
        Assert.Single(fieldChanges);

        // Switch 包装层不订阅 OnValidationStateChanged（与 ）：
        // 真实场景里是 EditForm 在验证态变化后重渲染子树，这里显式重渲染根来模拟。
        await renderer.RerenderRootAsync(0, hostParams);
        await SettleAsync(renderer);

        Assert.DoesNotContain("aria-invalid", renderer.Markup());
    }

    [Fact]
    public async Task 无EditContext宿主时不应播报无效态()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Switch>(Params(@checked: true));

        Assert.DoesNotContain("aria-invalid", renderer.Markup());
    }
}
