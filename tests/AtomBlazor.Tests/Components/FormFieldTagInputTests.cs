using AtomBlazor.Primitives;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// FormFieldTagInput 组合字段测试：在可选 EditContext 宿主下挂载
/// FormFieldTagInput（根 0，底座无弹层，无需 PortalHost），验证标签徽章渲染、
/// 参数透传（Placeholder/Clearable/Disabled/Variant/TagTemplate）、InputClass+splat
/// 落点、ErrorText 手动覆写、键入提交经封装层两步沿回填（Tags+TagsChanged+
/// NotifyFieldChanged）并消解 EditContext 校验错误。未接线沿（ 保留）：
/// 封装层不传 For 与 Id，label 无 for 属性、控件无 aria-describedby——与
/// FormFieldSwitch 家族实通形态不同。键入提交走底座 [JSInvokable] JsTriggerAdd
/// （真实 Enter 沿在浏览器侧 tag-input.js，headless 先例 TagInputTests）。
/// 快照含 formfield GUID 与 taginput GUID，先经正则归一化。
/// </summary>
public class FormFieldTagInputTests
{
    /// <summary>单标签常量（供重渲染参数面复用，规避 CA1861 常量数组实参）。</summary>
    private static readonly string[] alphaTag = ["alpha"];

    /// <summary>归一化 ControlId 与 TagInput 实例 id 的随机 GUID，使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>"),
            "taginput-[0-9a-f]{32}", "taginput-<id>");

    /// <summary>构造带基元服务的渲染器（底座 TagInput 走 barrel interop，JS 用桩）。</summary>
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>排空渲染队列，确保 StateHasChanged 的后续批已落定。</summary>
    private static async Task SettleAsync(ComponentTestRenderer renderer, int rounds = 2)
    {
        for (var i = 0; i < rounds; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    /// <summary>校验沿测试模型：Tags 为空即无效（由假校验器执行）。</summary>
    internal sealed class Doc
    {
        public IReadOnlyList<string>? Tags { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：Tags 为空时向该字段写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Doc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(Doc.Tags));

        void Validate()
        {
            messages.Clear();
            if (doc.Tags is null || doc.Tags.Count == 0)
            {
                messages.Add(field, "至少输入一个标签");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();
        return context;
    }

    private static Dictionary<string, object?> FieldParams(
        IReadOnlyList<string>? tags = null,
        EventCallback<IReadOnlyList<string>?> tagsChanged = default,
        Expression<Func<IReadOnlyList<string>?>>? tagsExpression = null,
        string? placeholder = null,
        int? maxTags = null,
        int? maxTagLength = null,
        bool? allowDuplicates = null,
        TagInputTrigger? addTrigger = null,
        TagInputVariant? variant = null,
        bool? clearable = null,
        bool? disabled = null,
        RenderFragment<string>? tagTemplate = null,
        string? inputClass = null,
        string? label = null,
        string? helperText = null,
        string? errorText = null,
        string? ariaLabel = null,
        IReadOnlyDictionary<string, object?>? splat = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (tags is not null)
        {
            parameters[nameof(FormFieldTagInput.Tags)] = tags;
        }
        if (tagsChanged.HasDelegate)
        {
            parameters[nameof(FormFieldTagInput.TagsChanged)] = tagsChanged;
        }
        if (tagsExpression is not null)
        {
            parameters[nameof(FormFieldTagInput.TagsExpression)] = tagsExpression;
        }
        if (placeholder is not null)
        {
            parameters[nameof(FormFieldTagInput.Placeholder)] = placeholder;
        }
        if (maxTags is not null)
        {
            parameters[nameof(FormFieldTagInput.MaxTags)] = maxTags.Value;
        }
        if (maxTagLength is not null)
        {
            parameters[nameof(FormFieldTagInput.MaxTagLength)] = maxTagLength.Value;
        }
        if (allowDuplicates is not null)
        {
            parameters[nameof(FormFieldTagInput.AllowDuplicates)] = allowDuplicates.Value;
        }
        if (addTrigger is not null)
        {
            parameters[nameof(FormFieldTagInput.AddTrigger)] = addTrigger.Value;
        }
        if (variant is not null)
        {
            parameters[nameof(FormFieldTagInput.Variant)] = variant.Value;
        }
        if (clearable is not null)
        {
            parameters[nameof(FormFieldTagInput.Clearable)] = clearable.Value;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldTagInput.Disabled)] = disabled.Value;
        }
        if (tagTemplate is not null)
        {
            parameters[nameof(FormFieldTagInput.TagTemplate)] = tagTemplate;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldTagInput.InputClass)] = inputClass;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldTagInput.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldTagInput.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldTagInput.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldTagInput.AriaLabel)] = ariaLabel;
        }
        if (splat is not null)
        {
            parameters["AdditionalAttributes"] = new Dictionary<string, object?>(splat);
        }

        return parameters;
    }

    /// <summary>取标签输入框开标签（type="text" 指纹；Field 根为 data-slot="field" div）。</summary>
    private static string TextInputTag(string markup) =>
        Regex.Match(markup, """<input[^>]*type="text"[^>]*>""").Value;

    /// <summary>取 TagInput 根 div 开标签（class="atom:relative" 指纹）。</summary>
    private static string TagInputRootTag(string markup) =>
        Regex.Match(markup, """<div class="atom:relative"[^>]*>""").Value;

    /// <summary>取 Field 根 div 开标签。</summary>
    private static string FieldRootTag(string markup) =>
        Regex.Match(markup, """<div[^>]*data-slot="field"[^>]*>""").Value;

    /// <summary>模拟键入：向底座输入框派发 oninput。</summary>
    private static async Task TypeAsync(ComponentTestRenderer renderer, string text)
    {
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("oninput", new ChangeEventArgs { Value = text }));
        await SettleAsync(renderer);
    }

    /// <summary>直调底座 [JSInvokable] JsTriggerAdd（Enter 触发沿的 headless 对端）。</summary>
    private static async Task TriggerAddAsync(ComponentTestRenderer renderer, TagInput input)
    {
        await renderer.Dispatcher.InvokeAsync(async () => await input.JsTriggerAdd());
        await SettleAsync(renderer);
    }

    // ------------------------------------------------------------------
    // 快照与写入面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldTagInput>(FieldParams(
            tags: ["alpha", "beta"],
            label: "技术标签", helperText: "回车添加", ariaLabel: "标签录入"));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签与帮助文本应落Field结构且未接线for与describedby()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldTagInput>(FieldParams(
            tags: ["alpha"],
            label: "技术标签", helperText: "回车添加", ariaLabel: "标签录入"));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // FieldLabel 的 class 属性恒存在（怪癖），label 断言放宽到开标签内文本匹配。
        Assert.Matches("""<label[^>]*data-slot="field-label"[^>]*>技术标签</label>""", markup);
        // 未接线沿（ 保留）：label 无 for 属性、控件无 aria-describedby。
        Assert.DoesNotContain("for=", markup);
        Assert.DoesNotContain("aria-describedby", markup);
        // 描述元素仍带 description 后缀 id（仅元素自身）。
        Assert.Matches(@"id=""formfield-[0-9a-f]{32}-description""", markup);
        Assert.Contains("data-slot=\"field-description\"", markup);
        Assert.Contains("回车添加", markup);
        // 底座：容器与输入框同落 AriaLabel。
        Assert.Contains("aria-label=\"标签录入\"", markup);
        Assert.Contains(">alpha<", markup); // 标签徽章
    }

    [Fact]
    public async Task 标签徽章应渲染文本与移除按钮()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldTagInput>(FieldParams(tags: ["alpha", "beta"]));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains(">alpha<", markup);
        Assert.Contains(">beta<", markup);
        // 每枚徽章带移除按钮（本地化 Remove {0}）。
        Assert.Contains("aria-label=\"Remove alpha\"", markup);
        Assert.Contains("aria-label=\"Remove beta\"", markup);
    }

    [Fact]
    public async Task 键入提交应经封装层回填并上报()
    {
        var reports = new List<IReadOnlyList<string>?>();
        var renderer = CreateRenderer();

        var field = await renderer.MountAsync<FormFieldTagInput>(FieldParams(
            tagsChanged: EventCallback.Factory.Create<IReadOnlyList<string>?>(
                new object(), l => reports.Add(l))));
        await SettleAsync(renderer);

        var inner = renderer.FindComponent<TagInput>();
        await TypeAsync(renderer, "gamma");
        await TriggerAddAsync(renderer, inner);

        // 封装层两步沿：Tags 赋值 + TagsChanged 上报（底座 NotifyTagsChanged 的同一数组引用）。
        Assert.NotNull(field.Tags);
        Assert.Equal(["gamma"], field.Tags);
        var report = Assert.Single(reports);
        Assert.Equal(["gamma"], report);
        Assert.Same(field.Tags, report);
        // 徽章显形（底座自身重渲染）。
        Assert.Contains(">gamma<", renderer.Markup());
    }

    [Fact]
    public async Task EditContext校验应随键入提交消解()
    {
        var doc = new Doc();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(doc);

        Expression<Func<IReadOnlyList<string>?>> tagsExpression = () => doc.Tags;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                // Tags 须回写模型（FormFieldSwitchTests 先例）：校验器读的是 doc.Tags
                // 没有回写则再校验仍见空而错误不消解。
                builder.OpenComponent<FormFieldTagInput>(0);
                builder.AddAttribute(1, nameof(FormFieldTagInput.Tags), doc.Tags);
                builder.AddAttribute(2, nameof(FormFieldTagInput.TagsExpression), tagsExpression);
                builder.AddAttribute(3, nameof(FormFieldTagInput.TagsChanged),
                    EventCallback.Factory.Create<IReadOnlyList<string>?>(new object(), l => doc.Tags = l));
                builder.AddAttribute(4, nameof(FormFieldTagInput.Label), "技术标签");
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        // 初始 Tags 为空：广播校验后错误经 FieldError 呈现。
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);
        var markup = renderer.Markup();
        Assert.Contains("至少输入一个标签", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);

        // 键入提交：封装层 NotifyFieldChanged 触发再校验 → 错误消解。
        var inner = renderer.FindComponent<TagInput>();
        await TypeAsync(renderer, "gamma");
        await TriggerAddAsync(renderer, inner);

        markup = renderer.Markup();
        Assert.DoesNotContain("至少输入一个标签", markup);
        Assert.Contains(">gamma<", markup);
    }

    // ------------------------------------------------------------------
    // 参数透传
    // ------------------------------------------------------------------

    [Fact]
    public async Task 占位符与清除按钮与禁用应透传落底座()
    {
        var renderer = CreateRenderer();

        // 空列表挂载：占位符显形（底座 EffectivePlaceholder）、无清除按钮
        //（底座 Clearable && Count>0 && !Disabled）。
        await renderer.MountAsync<FormFieldTagInput>(FieldParams(
            placeholder: "输入标签", clearable: true));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("placeholder=\"输入标签\"", TextInputTag(markup));
        Assert.DoesNotContain("aria-label=\"Clear all tags\"", markup);

        // 参数沿写入标签（0→1 增长，底座 ShouldRender 放行）：徽章显形、
        // 占位符退场、清除按钮显形。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(FormFieldTagInput.Tags)] = alphaTag,
            [nameof(FormFieldTagInput.Placeholder)] = "输入标签",
        }));
        await SettleAsync(renderer);
        markup = renderer.Markup();
        Assert.Contains(">alpha<", markup);
        Assert.DoesNotContain("placeholder=", TextInputTag(markup));
        Assert.Contains("aria-label=\"Clear all tags\"", markup);

        // 禁用态：输入框 disabled + 容器 aria-disabled，无清除按钮
        // Disabled 参数沿可越过底座 ShouldRender（_lastDisabled 追踪变化）。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(FormFieldTagInput.Tags)] = alphaTag,
            [nameof(FormFieldTagInput.Clearable)] = true,
            [nameof(FormFieldTagInput.Disabled)] = true,
        }));
        await SettleAsync(renderer);
        markup = renderer.Markup();
        Assert.Contains(" disabled", TextInputTag(markup));
        Assert.Contains("aria-disabled=\"true\"", markup);
        Assert.DoesNotContain("aria-label=\"Clear all tags\"", markup);
        // 禁用态徽章不带移除按钮。
        Assert.DoesNotContain("aria-label=\"Remove alpha\"", markup);
    }

    /// <summary>
    /// 怪癖（ 保留； 的 ShouldRender 与 OnParametersSet ）：
    /// 底座 ShouldRender 只在自身追踪状态变化时放行重渲染，而 _lastTagCount 于首帧
    /// 不同步（ShouldRender 首帧不参与，挂载 1 枚标签后其仍为 0）。于是经参数沿把
    /// Tags 由非空清空为 null 时，OnParametersSet 已把 _currentTags 清空
    /// ShouldRender 却见计数与陈值相等（0 == 0）而抑制渲染——徽章留在 markup 上、
    /// 占位符不显形。增长方向（0→N）不受影响，故上一条测试按"先空后加"铺排。
    /// </summary>
    [Fact]
    public async Task 参数沿清空标签应因底座ShouldRender抑制而保留徽章()
    {
        var renderer = CreateRenderer();

        var field = await renderer.MountAsync<FormFieldTagInput>(FieldParams(
            tags: ["alpha"], placeholder: "输入标签", clearable: true));
        await SettleAsync(renderer);
        Assert.Contains(">alpha<", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(FormFieldTagInput.Tags)] = null,
            [nameof(FormFieldTagInput.Placeholder)] = "输入标签",
        }));
        await SettleAsync(renderer);

        // 封装层参数已更新，底座状态已同步，但渲染被抑制：徽章仍在、占位符不显形。
        Assert.Null(field.Tags);
        var markup = renderer.Markup();
        Assert.Contains(">alpha<", markup);
        Assert.DoesNotContain("placeholder=\"输入标签\"", markup);
    }

    [Fact]
    public async Task Outline变体应透传落徽章类面()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldTagInput>(FieldParams(tags: ["alpha"], variant: TagInputVariant.Outline));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // Badge 无 data-slot，以基础类串（rounded-full + border + px-2.5）定位徽章开标签。
        var badge = Regex.Match(markup, """<div class="atom:inline-flex[^"]*atom:rounded-full atom:border atom:px-2.5[^"]*">""").Value;
        Assert.Contains("atom:text-foreground", badge); // BadgeVariant.Outline 指纹
        Assert.DoesNotContain("atom:bg-secondary", badge);
    }

    [Fact]
    public async Task TagTemplate应替换默认徽章渲染()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldTagInput>(FieldParams(
            tags: ["alpha"],
            tagTemplate: tag => builder =>
            {
                builder.OpenElement(0, "em");
                builder.AddContent(1, $"#{tag}");
                builder.CloseElement();
            }));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("<em>#alpha</em>", markup);
        // 默认徽章整体让位（徽章基础类串不再出现）。
        Assert.DoesNotContain("atom:rounded-full atom:border atom:px-2.5", markup);
    }

    [Fact]
    public async Task InputClass与透传应分别落底座容器与根元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldTagInput>(FieldParams(
            inputClass: "atom:custom-tags",
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-tags" }));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("atom:custom-tags", markup);                          // InputClass → 容器
        Assert.Contains("data-testid=\"field-tags\"", TagInputRootTag(markup)); // splat → 底座根 div
        // Field 根不吃 splat（splat 经 @attributes 只进底座根元素）。
        Assert.DoesNotContain("data-testid", FieldRootTag(markup));
    }

    [Fact]
    public async Task 错误文本手动覆写应落FieldError且隐藏帮助文本()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<FormFieldTagInput>(FieldParams(
            tags: ["alpha"], label: "技术标签", helperText: "回车添加",
            errorText: "服务端驳回：标签含敏感词"));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：标签含敏感词", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", FieldRootTag(markup));
        // 标记已过 Scrub 归一化：GUID 段为 <id>。
        Assert.Contains("id=\"formfield-<id>-error\"", markup);
        // 错误态下帮助文本隐藏。
        Assert.DoesNotContain("回车添加", markup);
    }

    // ------------------------------------------------------------------
    // 缺省参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var renderer = CreateRenderer();

        // 缺省：MaxTags=int.MaxValue、MaxTagLength=50、AllowDuplicates=false、
        // AddTrigger=Enter|Comma、SuggestionDebounceMs=300、Variant=Default、
        // Clearable=false、Disabled=false、引用面全 null。
        var field = await renderer.MountAsync<FormFieldTagInput>(new Dictionary<string, object?>());
        await SettleAsync(renderer);

        Assert.Null(field.Tags);
        Assert.Null(field.TagsExpression);
        Assert.Null(field.Placeholder);
        Assert.Equal(int.MaxValue, field.MaxTags);
        Assert.Equal(50, field.MaxTagLength);
        Assert.False(field.AllowDuplicates);
        Assert.Equal(TagInputTrigger.Enter | TagInputTrigger.Comma, field.AddTrigger);
        Assert.Equal(300, field.SuggestionDebounceMs);
        Assert.Equal(TagInputVariant.Default, field.Variant);
        Assert.False(field.Clearable);
        Assert.False(field.Disabled);
        Assert.Null(field.InputClass);
        Assert.Null(field.Label);
        Assert.Null(field.HelperText);
        Assert.Null(field.ErrorText);
        Assert.Null(field.TagTemplate);
        Assert.False(field.TagsChanged.HasDelegate);
    }
}
