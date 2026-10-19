using AtomBlazor.Primitives;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// FormFieldMultiSelect 组合字段测试：在 PortalHost（根 0）与可选 EditContext 宿主下挂载
/// FormFieldMultiSelect&lt;string&gt;（根 1），验证多选集合绑定双态（有值徽标态/清空占位态）、
/// 选项行与复选框点击回填剔除/追加列表、徽标删除与清除按钮沿、SelectAll 勾选/取消与自定义标签、
/// 标签不带 for 且 ControlId 不落控件（ quirk，触发器锚点 popover-&lt;n&gt;-trigger）、
/// ErrorText 手动覆写优先链、EditContext 校验消息随集合消长（ValuesExpression For 路径）、
/// MaxDisplayTags 溢出徽标、SearchQueryChanged 直通沿（无 HasDelegate 门控，与 Combobox 家族不对称）、
/// Required/Disabled/AriaLabel/InputClass/splat 透传面与缺省参数面。
/// 弹层开合与 onclick occurrence 次序沿用 MultiSelectTests 先例（门户内容先渲染，触发器排后）。
/// </summary>
public class FormFieldMultiSelectTests
{
    /// <summary>
    /// 归一化 ControlId 的随机 GUID（describedby 通道）、popover-N 自增短 id 与
    /// multiselect GUID 稳定 id，使快照与断言可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(
                Regex.Replace(markup, "formfield-[0-9a-f]{32}", "formfield-<id>"),
                "popover-[0-9]+", "popover-<n>"),
            "multiselect-[0-9a-f]{32}", "multiselect-<id>");

    /// <summary>测试复用选项数组（CA1861 先例：常量数组参数首选 static readonly 字段）。</summary>
    private static readonly SelectOption<string>[] fruitOptions = [new("a", "苹果"), new("b", "香蕉")];

    /// <summary>三选项数组（SelectAll 全选断言复用）。</summary>
    private static readonly SelectOption<string>[] tripleOptions =
    [
        new("a", "苹果"), new("b", "香蕉"), new("c", "橙子"),
    ];

    /// <summary>四选项数组（溢出徽标复用）。</summary>
    private static readonly SelectOption<string>[] fourOptions =
    [
        new("a", "苹果"), new("b", "香蕉"), new("c", "橙子"), new("d", "葡萄"),
    ];

    /// <summary>常用受控值集合。</summary>
    private static readonly string[] twoSelected = ["a", "b"];
    private static readonly string[] fourSelected = ["a", "b", "c", "d"];
    private static readonly string[] oneSelected = ["a"];

    /// <summary>
    /// 构造带全套基元服务的渲染器（门户/定位/焦点/本地化器 + NoopJavaScript）
    /// 沿 MultiSelectTests 先例。
    /// </summary>
    private static ComponentTestRenderer CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection().AddAtomBlazorTestDefaults();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance));
        services.AddSingleton<IPositioningService>(new PositioningService(js));
        services.AddSingleton<IFocusManager>(new FocusManager(js));
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>排空渲染队列，确保 StateHasChanged 的后续批已落定。</summary>
    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    /// <summary>强制重渲染门户宿主（根 0），让弹层内容确定性地进入渲染树。</summary>
    private static async Task RerenderPortalAsync(ComponentTestRenderer renderer)
    {
        await SettleAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);
    }

    /// <summary>点击触发器打开弹层，并照 MultiSelectTests 沿再渲染根 1 让 setup 落定。</summary>
    private static async Task OpenAsync(ComponentTestRenderer renderer, Dictionary<string, object?> dict)
    {
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await RerenderPortalAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await RerenderPortalAsync(renderer);
    }

    /// <summary>校验沿测试模型：Fruits 为空即无效（由假校验器执行）。</summary>
    internal sealed class Bag
    {
        public IEnumerable<string>? Fruits { get; set; }
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：Fruits 为 null 或空时写入字段消息。
    /// </summary>
    private static EditContext CreateValidatedContext(Bag bag)
    {
        var context = new EditContext(bag);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(bag, nameof(Bag.Fruits));

        void Validate()
        {
            messages.Clear();
            if (bag.Fruits is null || !bag.Fruits.Any())
            {
                messages.Add(field, "至少选择一种水果");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();
        return context;
    }

    private static Dictionary<string, object?> FieldParams(
        IEnumerable<string>? values = null,
        EventCallback<IEnumerable<string>?> valuesChanged = default,
        Expression<Func<IEnumerable<string>?>>? valuesExpression = null,
        bool? disabled = null,
        bool? required = null,
        string? placeholder = null,
        string? selectAllLabel = null,
        bool? showSelectAll = null,
        int? maxDisplayTags = null,
        string? inputClass = null,
        IEnumerable<SelectOption<string>>? options = null,
        RenderFragment? childContent = null,
        EventCallback<string> searchQueryChanged = default,
        bool? autoClose = null,
        bool? matchTriggerWidth = null,
        string? label = null,
        string? helperText = null,
        string? errorText = null,
        string? ariaLabel = null,
        FieldOrientation? orientation = null,
        IReadOnlyDictionary<string, object?>? splat = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (values is not null)
        {
            parameters[nameof(FormFieldMultiSelect<string>.Values)] = values;
        }
        if (valuesChanged.HasDelegate)
        {
            parameters[nameof(FormFieldMultiSelect<string>.ValuesChanged)] = valuesChanged;
        }
        if (valuesExpression is not null)
        {
            parameters[nameof(FormFieldMultiSelect<string>.ValuesExpression)] = valuesExpression;
        }
        if (disabled is not null)
        {
            parameters[nameof(FormFieldMultiSelect<string>.Disabled)] = disabled.Value;
        }
        if (required is not null)
        {
            parameters[nameof(FormFieldMultiSelect<string>.Required)] = required.Value;
        }
        if (placeholder is not null)
        {
            parameters[nameof(FormFieldMultiSelect<string>.Placeholder)] = placeholder;
        }
        if (selectAllLabel is not null)
        {
            parameters[nameof(FormFieldMultiSelect<string>.SelectAllLabel)] = selectAllLabel;
        }
        if (showSelectAll is not null)
        {
            parameters[nameof(FormFieldMultiSelect<string>.ShowSelectAll)] = showSelectAll.Value;
        }
        if (maxDisplayTags is not null)
        {
            parameters[nameof(FormFieldMultiSelect<string>.MaxDisplayTags)] = maxDisplayTags.Value;
        }
        if (inputClass is not null)
        {
            parameters[nameof(FormFieldMultiSelect<string>.InputClass)] = inputClass;
        }
        if (options is not null)
        {
            parameters[nameof(FormFieldMultiSelect<string>.Options)] = options;
        }
        if (childContent is not null)
        {
            parameters[nameof(FormFieldMultiSelect<string>.ChildContent)] = childContent;
        }
        if (searchQueryChanged.HasDelegate)
        {
            parameters[nameof(FormFieldMultiSelect<string>.SearchQueryChanged)] = searchQueryChanged;
        }
        if (autoClose is not null)
        {
            parameters[nameof(FormFieldMultiSelect<string>.AutoClose)] = autoClose.Value;
        }
        if (matchTriggerWidth is not null)
        {
            parameters[nameof(FormFieldMultiSelect<string>.MatchTriggerWidth)] = matchTriggerWidth.Value;
        }
        if (label is not null)
        {
            parameters[nameof(FormFieldMultiSelect<string>.Label)] = label;
        }
        if (helperText is not null)
        {
            parameters[nameof(FormFieldMultiSelect<string>.HelperText)] = helperText;
        }
        if (errorText is not null)
        {
            parameters[nameof(FormFieldMultiSelect<string>.ErrorText)] = errorText;
        }
        if (ariaLabel is not null)
        {
            parameters[nameof(FormFieldMultiSelect<string>.AriaLabel)] = ariaLabel;
        }
        if (orientation is not null)
        {
            parameters[nameof(FormFieldMultiSelect<string>.Orientation)] = orientation.Value;
        }
        if (splat is not null)
        {
            parameters["AdditionalAttributes"] = new Dictionary<string, object?>(splat);
        }

        return parameters;
    }

    /// <summary>单独一枚诱饵条目（芋头），用于断言 Options 优先时 ChildContent 被忽略。</summary>
    private static RenderFragment DecoyItem => builder =>
    {
        builder.OpenComponent<MultiSelectItem<string>>(0);
        builder.AddAttribute(1, nameof(MultiSelectItem<string>.Value), "z");
        builder.AddAttribute(2, nameof(MultiSelectItem<string>.Text), "芋头");
        builder.CloseComponent();
    };

    // ------------------------------------------------------------------
    // 快照与接线
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldMultiSelect<string>>(FieldParams(
            label: "水果", helperText: "可多选", placeholder: "请选择",
            values: twoSelected, options: fruitOptions));
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 标签应不带for且ControlId不落控件触发器锚点为popover触发id()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldMultiSelect<string>>(FieldParams(
            label: "水果", helperText: "可多选", options: fruitOptions));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        // quirk：MultiSelect 家族 FieldLabel 不带 For，包装层不向控件传任何 id——
        // ControlId 从不落控件 id 通道，触发器锚点是基元自造的 popover-<n>-trigger。
        Assert.Matches(@"id=""popover-[0-9]+-trigger""", markup);
        Assert.Equal(0, Regex.Count(markup, @"id=""formfield-[0-9a-f]{32}""")); // 裸 ControlId 不落任何元素（-description 后缀档除外）
        Assert.DoesNotContain("for=", markup);
        Assert.Matches(@"aria-describedby=""formfield-[0-9a-f]{32}-description""", markup);
        // 多选语义面沿内部控件：combobox 角色 + aria-multiselectable。
        Assert.Contains("role=\"combobox\"", markup);
        Assert.Contains("aria-multiselectable=\"true\"", markup);
        Assert.Contains("aria-expanded=\"false\"", markup);
    }

    // ------------------------------------------------------------------
    // 集合绑定双态与选择沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 点击选项应追加回填且集合清空应回到占位态()
    {
        var reports = new List<IEnumerable<string>?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = FieldParams(
            placeholder: "选一个",
            options: fruitOptions,
            showSelectAll: false,
            values: oneSelected,
            valuesChanged: EventCallback.Factory.Create<IEnumerable<string>?>(new object(), v => reports.Add(v)));
        await renderer.MountAsync<FormFieldMultiSelect<string>>(dict);
        await SettleAsync(renderer);

        // 双态之一：有值态——徽标显形、占位符让位。
        var markup = renderer.Markup();
        Assert.Contains("苹果", markup);
        Assert.DoesNotContain("选一个", markup);

        await OpenAsync(renderer, dict);

        // 打开态 onclick 次序（MultiSelectTests 先例）：选项 苹果(0)、其复选框(1)、
        // 香蕉(2)、其复选框(3)、页脚 Clear(4)、页脚 Close(5)、触发器(6+)。点香蕉行。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 2));
        await SettleAsync(renderer);

        Assert.Equal(["a,b"], reports.Select(r => string.Join(",", r ?? Enumerable.Empty<string>())));
        Assert.Contains("aria-expanded=\"true\"", renderer.Markup()); // 多选保持开层（无 AutoClose 逐击关闭沿）
    }

    [Fact]
    public async Task 徽标删除与清除按钮应分别回填剔除列表与null并回到占位态()
    {
        var reports = new List<IEnumerable<string>?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldMultiSelect<string>>(FieldParams(
            placeholder: "选一个",
            options: fruitOptions,
            values: twoSelected,
            valuesChanged: EventCallback.Factory.Create<IEnumerable<string>?>(new object(), v => reports.Add(v))));
        await SettleAsync(renderer);

        // 关闭态 onclick 次序：触发器(0)、徽标 苹果 删除(1)、徽标 香蕉 删除(2)、清除(3)。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));
        await SettleAsync(renderer);

        Assert.Equal(["b"], reports.Select(r => string.Join(",", r ?? Enumerable.Empty<string>())));

        // 清除按钮：回填 null → 双态之二（清空占位态，经根 1 重渲染把 null 值推回）。
        // 首击删除后根 1 已重渲染，封闭态载体面收缩为：触发器(0)、徽标 香蕉 删除(1)、清除(2)。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 2));
        await SettleAsync(renderer);

        Assert.Equal(2, reports.Count);
        Assert.Null(reports[1]);

        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, FieldParams(
            placeholder: "选一个", options: fruitOptions,
            valuesChanged: EventCallback.Factory.Create<IEnumerable<string>?>(new object(), _ => { }))));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("选一个", markup);   // 占位符复位
        Assert.DoesNotContain("苹果", markup); // 徽标全撤
    }

    [Fact]
    public async Task SelectAll应整体勾选与取消且支持自定义标签()
    {
        var reports = new List<IEnumerable<string>?>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = FieldParams(
            options: tripleOptions,
            selectAllLabel: "全选所有",
            valuesChanged: EventCallback.Factory.Create<IEnumerable<string>?>(new object(), v => reports.Add(v)));
        await renderer.MountAsync<FormFieldMultiSelect<string>>(dict);
        await SettleAsync(renderer);

        await OpenAsync(renderer, dict);
        Assert.Contains("全选所有", renderer.Markup()); // 标签经包装层覆写 Localizer 缺省

        // occurrence 0 = Select All 行（门户内容最先渲染，其后才是根 1 触发器）。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 0));
        await SettleAsync(renderer);

        var all = Assert.Single(reports);
        Assert.Equal(["a", "b", "c"], all);

        // 再击：全部取消 → 回填 null。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 0));
        await SettleAsync(renderer);

        Assert.Equal(2, reports.Count);
        Assert.Null(reports[1]);
    }

    [Fact]
    public async Task Options模式应优先于ChildContent诱饵不入渲染树()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = FieldParams(
            options: fruitOptions,
            childContent: DecoyItem,
            values: oneSelected);
        await renderer.MountAsync<FormFieldMultiSelect<string>>(dict);
        await SettleAsync(renderer);

        Assert.DoesNotContain("芋头", renderer.Markup());

        await OpenAsync(renderer, dict);
        var markup = renderer.Markup();
        Assert.Contains("苹果", markup);
        Assert.Contains("香蕉", markup);
        Assert.DoesNotContain("芋头", markup);
    }

    // ------------------------------------------------------------------
    // EditContext 校验沿（For 路径）
    // ------------------------------------------------------------------

    [Fact]
    public async Task 校验消息应随集合消长并经ValuesExpression路径归属()
    {
        var bag = new Bag { Fruits = null };
        var reports = new List<IEnumerable<string>?>();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(bag);
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        Expression<Func<IEnumerable<string>?>> valuesExpression = () => bag.Fruits;
        await renderer.MountAsync<FormFieldBaseTests.EditContextHost>(new Dictionary<string, object?>
        {
            [nameof(FormFieldBaseTests.EditContextHost.EditContext)] = context,
            [nameof(FormFieldBaseTests.EditContextHost.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<FormFieldMultiSelect<string>>(0);
                builder.AddAttribute(1, nameof(FormFieldMultiSelect<string>.ValuesExpression), valuesExpression);
                builder.AddAttribute(2, nameof(FormFieldMultiSelect<string>.ValuesChanged),
                    EventCallback.Factory.Create<IEnumerable<string>?>(bag, value =>
                    {
                        bag.Fruits = value;
                        reports.Add(value);
                    }));
                builder.AddAttribute(3, nameof(FormFieldMultiSelect<string>.Label), (string?)"水果");
                builder.AddAttribute(4, nameof(FormFieldMultiSelect<string>.Options), fruitOptions);
                builder.AddAttribute(5, nameof(FormFieldMultiSelect<string>.ShowSelectAll), false);
                builder.CloseComponent();
            }),
        });
        await SettleAsync(renderer);

        // 空集合：广播整体校验后错误经 FieldError 呈现，describedby 指向错误元素。
        await renderer.Dispatcher.InvokeAsync(async () => await context.ValidateAsync());
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("至少选择一种水果", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.Matches(@"aria-describedby=""formfield-[0-9a-f]{32}-error""", markup);

        // 打开并点"苹果"行（门户内容先渲染：苹果行 occurrence 0）→ 外层三步沿 → 校验消解。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await RerenderPortalAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 0));
        await SettleAsync(renderer);

        Assert.Equal(["a"], reports.Select(r => string.Join(",", r ?? Enumerable.Empty<string>())));
        Assert.Equal("a", Assert.Single(bag.Fruits!));
        markup = renderer.Markup();
        Assert.DoesNotContain("至少选择一种水果", markup);
        Assert.Contains("苹果", markup); // 徽标回填
    }

    // ------------------------------------------------------------------
    // 错误覆写与参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 错误文本手动覆写应显形并切换describedby()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldMultiSelect<string>>(FieldParams(
            label: "水果", helperText: "可多选", errorText: "服务端驳回：至少选择一种水果",
            options: fruitOptions));
        await SettleAsync(renderer);

        var markup = Scrub(renderer.Markup());
        Assert.Contains("服务端驳回：至少选择一种水果", markup);
        Assert.Contains("data-slot=\"field-error\"", markup);
        Assert.Contains("data-invalid=\"true\"", markup);
        Assert.DoesNotContain("可多选", markup);
        Assert.Contains("aria-describedby=\"formfield-<id>-error\"", markup);
        // quirk：包装层无 aria-invalid 透传面（与 Textarea 家族显式透传档不同）。
        Assert.DoesNotContain("aria-invalid", markup);
    }

    [Fact]
    public async Task MaxDisplayTags应折叠溢出徽标为Nmore()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldMultiSelect<string>>(FieldParams(
            options: fourOptions, values: fourSelected, maxDisplayTags: 2));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("+2 more", markup); // 四值仅显前两枚
        Assert.Contains("苹果", markup);
        Assert.Contains("香蕉", markup);
        Assert.DoesNotContain("Remove 橙子", markup); // 溢出项不带删除徽标
    }

    [Fact]
    public async Task 必填禁用名称与透传应落透传参数面()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        await renderer.MountAsync<FormFieldMultiSelect<string>>(FieldParams(
            required: true, disabled: true, ariaLabel: "选择水果",
            inputClass: "atom:custom-multi", options: fruitOptions,
            splat: new Dictionary<string, object?> { ["data-testid"] = "field-multiselect" }));
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("aria-required=\"true\"", markup); // 触发器 combobox
        Assert.Contains("disabled", markup);               // 原生 disabled 属性（本家族禁用面形态）
        Assert.Contains("aria-label=\"选择水果\"", markup);
        Assert.Contains("atom:custom-multi", markup);
        Assert.Contains("data-testid=\"field-multiselect\"", markup);
    }

    [Fact]
    public async Task SearchQueryChanged应直通MultiSelect而不设HasDelegate门控()
    {
        var queries = new List<string>();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = FieldParams(
            options: fruitOptions,
            searchQueryChanged: EventCallback.Factory.Create<string>(new object(), q => queries.Add(q)));
        await renderer.MountAsync<FormFieldMultiSelect<string>>(dict);
        await SettleAsync(renderer);

        await OpenAsync(renderer, dict);
        // 搜索框是打开态唯一 oninput 携带者（MultiSelectTests 先例）。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("oninput", new ChangeEventArgs { Value = "香" }));
        await SettleAsync(renderer);

        // quirk：本家族把 SearchQueryChanged 原样直传（无 Combobox 家族的
        // HasDelegate 门控与 SearchQuery 受控面），回调即消费者回调。
        Assert.Contains("香", queries);
    }

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var field = await renderer.MountAsync<FormFieldMultiSelect<string>>(FieldParams());
        await SettleAsync(renderer);

        // 缺省：Orientation=Vertical（FormFieldBase 底座）、消息与按钮标签硬码英文、
        // ShowSelectAll/AutoClose 缺省开、MaxDisplayTags=3、宽度类已按改名惯例落 atom:。
        Assert.Equal(FieldOrientation.Vertical, field.Orientation);
        Assert.Null(field.Options);
        Assert.Null(field.ChildContent);
        Assert.Null(field.Values);
        Assert.Equal("Select items...", field.Placeholder);
        Assert.Equal("Search...", field.SearchPlaceholder);
        Assert.Equal("No results found.", field.EmptyMessage);
        Assert.Equal("Select All", field.SelectAllLabel);
        Assert.True(field.ShowSelectAll);
        Assert.Equal("Clear", field.ClearLabel);
        Assert.Equal("Close", field.CloseLabel);
        Assert.Equal(3, field.MaxDisplayTags);
        Assert.Equal("atom:w-[300px]", field.PopoverWidth);
        Assert.False(field.MatchTriggerWidth);
        Assert.True(field.AutoClose);
        Assert.False(field.Disabled);
        Assert.False(field.Required);
        Assert.Equal("atom:bg-accent atom:text-accent-foreground", field.ActiveClass);
        Assert.Contains("Select items...", renderer.Markup()); // 缺省占位直落触发器
        Assert.Contains("aria-expanded=\"false\"", renderer.Markup());
        Assert.DoesNotContain("aria-required=\"true\"", renderer.Markup());
    }
}
