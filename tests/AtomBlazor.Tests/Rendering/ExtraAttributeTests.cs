using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace AtomBlazor.Tests.Rendering;

/// <summary>
/// 每个公开组件都接受额外 HTML 属性，使用者有理由期望
/// <c>data-testid</c>、<c>title</c> 或 <c>id</c> 随处可用。有几个包装件把捕获的属性
/// splat 到了没有对应参数的基元根件上，而这类 splat 只要带一个条目，
/// Blazor 就立刻抛 <see cref="InvalidOperationException"/>——
/// 于是组件在所有 demo 里都能跑，第一次被加样式或被测试就崩。
/// </summary>
public class ExtraAttributeTests
{
    /// <summary>
    /// 只提供上下文的根件。它们不渲染自身元素，属性无处可落——
    /// 但收下它不得让渲染崩溃。
    /// </summary>
    [Theory]
    [InlineData(typeof(Dialog))]
    [InlineData(typeof(Sheet))]
    [InlineData(typeof(Popover))]
    [InlineData(typeof(HoverCard))]
    [InlineData(typeof(AlertDialog))]
    [InlineData(typeof(AtomBlazor.Primitives.AlertDialogPortal))]
    public async Task ContextOnlyRootsAcceptExtraAttributes(Type component)
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            // 经 Dialog 根件挂载，能为 Portal 供给它所需的上下文。
            RenderFragment child = builder =>
            {
                builder.OpenComponent(0, component);
                builder.AddAttribute(1, "data-testid", "extra");
                builder.CloseComponent();
            };

            await renderer.MountAsync<AtomBlazor.Primitives.DialogPrimitive>(new()
            {
                [nameof(AtomBlazor.Primitives.DialogPrimitive.ChildContent)] = child
            });
        });
    }

    /// <summary>
    /// 会渲染自身元素的组件必须把属性放上去，而不是丢掉。
    /// 这些各自是由弹层支撑的选择器，可见根件即其触发按钮。
    /// </summary>
    [Theory]
    [InlineData(typeof(ColorPicker))]
    [InlineData(typeof(DateRangePicker))]
    [InlineData(typeof(TimePicker))]
    [InlineData(typeof(ThemeSwitcher))]
    public async Task TriggerBackedComponentsRenderExtraAttributes(Type component)
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment child = builder =>
            {
                builder.OpenComponent(0, component);
                builder.AddAttribute(1, "data-testid", "extra");
                builder.CloseComponent();
            };

            await renderer.MountAsync<CascadingValue<string>>(new()
            {
                [nameof(CascadingValue<string>.Value)] = "unused",
                [nameof(CascadingValue<string>.ChildContent)] = child
            });

            Assert.Contains("data-testid=\"extra\"", renderer.Markup(), StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// 声明了参数却从不使用的组件。属性被收下又悄悄丢弃，
    /// 这比拒绝它更糟：使用者的测试钩子或 ARIA 属性
    /// 无声无息地不存在，也没有任何说明可寻。
    /// </summary>
    [Fact]
    public async Task TextareaRendersExtraAttributesInBothLayouts()
    {
        Assert.Equal("textarea", await ElementCarrying<Textarea>(new() { [nameof(Textarea.ShowCharacterCount)] = false }));
        Assert.Equal("textarea", await ElementCarrying<Textarea>(new() { [nameof(Textarea.ShowCharacterCount)] = true }));
    }

    [Fact]
    public async Task DatePickerRendersExtraAttributesOnItsTrigger()
    {
        Assert.Equal("button", await ElementCarrying<DatePicker>(new() { [nameof(DatePicker.Editable)] = false }));

        // 可编辑触发器是被带边框 div 包裹的 input；div 才是它的根件。
        Assert.Equal("div", await ElementCarrying<DatePicker>(new() { [nameof(DatePicker.Editable)] = true }));
    }

    [Fact]
    public async Task InputGroupButtonRendersExtraAttributes() =>
        Assert.Equal("button", await ElementCarrying<InputGroupButton>([]));

    /// <summary>
    /// 此前只有两个水平方向把属性带上了；垂直方向渲染出同一复选框的
    /// 第二份、未经 splat 的拷贝，并把它静默丢掉。
    /// </summary>
    [Theory]
    [InlineData(FieldOrientation.Vertical)]
    [InlineData(FieldOrientation.VerticalEnd)]
    [InlineData(FieldOrientation.Horizontal)]
    [InlineData(FieldOrientation.HorizontalEnd)]
    [InlineData(FieldOrientation.Responsive)]
    public async Task FormFieldCheckboxRendersExtraAttributesInEveryOrientation(FieldOrientation orientation) =>
        Assert.Equal("button", await ElementCarrying<FormFieldCheckbox>(new()
        {
            [nameof(FormFieldCheckbox.Orientation)] = orientation
        }));

    /// <summary>无论分节是否折叠，属性都必须存活。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FormSectionRendersExtraAttributesWhetherOrNotItCollapses(bool collapsible) =>
        Assert.Equal("div", await ElementCarrying<FormSection>(new()
        {
            [nameof(FormSection.Collapsible)] = collapsible
        }));

    /// <summary>
    /// 其余 FormField* 控件都 splat 到所包裹的控件上。时间选择器曾把它
    /// splat 到外层 Field 上，本该给 input 的属性落在了容器上。
    /// </summary>
    [Fact]
    public async Task FormFieldTimePickerRendersExtraAttributesOnThePicker() =>
        Assert.Equal("button", await ElementCarrying<FormFieldTimePicker>([]));

    /// <summary>
    /// 带一个额外属性渲染组件，并回报承载该属性的元素，
    /// 让测试不仅断言属性存活，还能断言它落在何处。
    /// </summary>
    private static async Task<string> ElementCarrying<T>(Dictionary<string, object?> parameters)
        where T : IComponent
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        var element = string.Empty;

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            parameters["data-testid"] = "extra";
            await renderer.MountAsync<T>(parameters);
            element = OwnerOf(renderer.Markup(), "data-testid=\"extra\"");
        });

        return element;
    }

    /// <summary>属性被写到哪个元素上的元素名，或未能写入的原因说明。</summary>
    private static string OwnerOf(string markup, string attribute)
    {
        var at = markup.IndexOf(attribute, StringComparison.Ordinal);

        if (at < 0)
        {
            return $"(not rendered at all) {markup}";
        }

        var open = markup.LastIndexOf('<', at);
        var name = markup[(open + 1)..].Split(' ', '>')[0];
        return name;
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
