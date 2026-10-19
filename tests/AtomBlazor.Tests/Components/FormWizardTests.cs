using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// FormWizard 测试：步骤注册与指示器结构快照、Next/Back 导航与
/// OnStepChanged/CurrentStepChanged 上报、可选步骤 Skip 与 Skipped 态（minus 图标）、
/// 末步 Complete 与 OnComplete、点击已访问步骤导航、AllowSkipAhead 跳步、
/// Validator 失败阻止前进、EditContext FieldNames 分步校验沿、
/// WizardStep 内容仅活动步渲染、受控 CurrentStep 同步、RetainStepState=false 清除前向状态、
/// 禁用步骤不可导航、WizardStep 脱离 FormWizard 抛错。
/// 指示器按钮与导航按钮均渲染 onclick，按渲染顺序选取 occurrence
/// （两步向导第 0 步：step0=0、step1=1、Back=2、Next=3；三步为 step0-2=0-2、Back=3、
/// Next=4；可选步激活时 Skip=4、Next=5；末步无 Next 而显示 Complete）。
/// 行为（保留）：GetStepState 对 currentStep 短路返回 Active
/// 故 Invalid 态（x 图标）对当前步不可达——校验失败只表现为停留原地。
/// </summary>
public class FormWizardTests
{
    /// <summary>构造带基元服务的渲染器（Button/LucideIcon 无 JS 依赖，服务面最小）。</summary>
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>排空渲染队列（步骤注册需两轮：子步 OnParametersSet 注册→OnAfterRender 补渲指示器）。</summary>
    private static async Task SettleAsync(ComponentTestRenderer renderer, int rounds = 2)
    {
        for (var i = 0; i < rounds; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    /// <summary>按序列号开一个 WizardStep（标题/内容/可选/禁用）。</summary>
    private static void AddStep(
        RenderTreeBuilder builder, int seq, string title, string content,
        bool isOptional = false, bool disabled = false, Func<bool>? validator = null)
    {
        builder.OpenComponent<WizardStep>(seq);
        builder.AddAttribute(seq + 1, nameof(WizardStep.Title), title);
        if (isOptional)
        {
            builder.AddAttribute(seq + 2, nameof(WizardStep.IsOptional), true);
        }
        if (disabled)
        {
            builder.AddAttribute(seq + 3, nameof(WizardStep.Disabled), true);
        }
        if (validator is not null)
        {
            builder.AddAttribute(seq + 4, nameof(WizardStep.Validator), validator);
        }
        builder.AddAttribute(seq + 5, nameof(WizardStep.ChildContent), (RenderFragment)(tb => tb.AddContent(0, content)));
        builder.CloseComponent();
    }

    /// <summary>两步基础子树：基本信息 → 确认提交。</summary>
    private static RenderFragment TwoSteps() => builder =>
    {
        AddStep(builder, 0, "基本信息", "姓名填写区");
        AddStep(builder, 10, "确认提交", "确认区");
    };

    /// <summary>三步子树（第二步行可选）。</summary>
    private static RenderFragment ThreeSteps(bool secondOptional = false) => builder =>
    {
        AddStep(builder, 0, "第一步", "内容一");
        AddStep(builder, 10, "第二步", "内容二", isOptional: secondOptional);
        AddStep(builder, 20, "第三步", "内容三");
    };

    private static async Task<(ComponentTestRenderer Renderer, FormWizard Wizard)> MountAsync(
        RenderFragment children, Dictionary<string, object?>? extra = null)
    {
        var renderer = CreateRenderer();
        var parameters = new Dictionary<string, object?> { [nameof(FormWizard.ChildContent)] = children };
        if (extra is not null)
        {
            foreach (var (key, value) in extra)
            {
                parameters[key] = value;
            }
        }

        var wizard = await renderer.MountAsync<FormWizard>(parameters);
        await SettleAsync(renderer);
        return (renderer, wizard);
    }

    /// <summary>直发第 occurrence 个 onclick（指示器按钮/导航按钮按渲染顺序编号）。</summary>
    private static async Task ClickAsync(ComponentTestRenderer renderer, int occurrence)
    {
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence));
        await SettleAsync(renderer);
    }

    /// <summary>截取指示器 nav 片段（步骤按钮与连接线，不含导航按钮区）。</summary>
    private static string NavSection(string markup)
    {
        var start = markup.IndexOf("<nav", StringComparison.Ordinal);
        var end = markup.IndexOf("</nav>", StringComparison.Ordinal);
        return markup[start..end];
    }

    /// <summary>取第 index 个步骤按钮开标签（nav 内前 steps.Count 个 button）。</summary>
    private static string StepButtonTag(string nav, int index)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(nav, """<button[^>]*>""");
        return matches[index].Value;
    }

    // ------------------------------------------------------------------
    // 结构与快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var (renderer, _) = await MountAsync(TwoSteps());

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"form-wizard\"", markup);
        Assert.Contains("aria-label=\"Wizard progress\"", markup);
        Assert.Contains("基本信息", markup);
        Assert.Contains("确认提交", markup);
        Assert.Contains("姓名填写区", markup);
        Assert.DoesNotContain("确认区", markup); // 非活动步内容不渲染

        await Verify(markup);
    }

    [Fact]
    public async Task 纵向布局应渲染描述列与垂直连接线()
    {
        // 垂直连接线只在 i < steps.Count-1 时渲染，故需两步才能断言到它。
        var (renderer, _) = await MountAsync(
            builder =>
            {
                builder.OpenComponent<WizardStep>(0);
                builder.AddAttribute(1, nameof(WizardStep.Title), "带描述步骤");
                builder.AddAttribute(2, nameof(WizardStep.Description), "步骤说明");
                builder.AddAttribute(3, nameof(WizardStep.ChildContent), (RenderFragment)(tb => tb.AddContent(0, "内容")));
                builder.CloseComponent();

                AddStep(builder, 10, "第二步", "内容二");
            },
            new Dictionary<string, object?> { [nameof(FormWizard.Layout)] = WizardLayout.Vertical });

        var markup = renderer.Markup();
        Assert.Contains("步骤说明", markup);
        Assert.Contains("atom:flex-col atom:shrink-0", markup); // 纵向指示器类
        Assert.Contains("atom:ms-[15px]", markup);              // 垂直连接线类
    }

    // ------------------------------------------------------------------
    // 导航沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task Next前进应切换活动步内容并上报步骤变化()
    {
        var stepReports = new List<int>();
        var currentReports = new List<int>();
        var (renderer, _) = await MountAsync(TwoSteps(), new Dictionary<string, object?>
        {
            [nameof(FormWizard.OnStepChanged)] =
                EventCallback.Factory.Create<int>(new object(), v => stepReports.Add(v)),
            [nameof(FormWizard.CurrentStepChanged)] =
                EventCallback.Factory.Create<int>(new object(), v => currentReports.Add(v)),
        });

        // 第 0 步：Next 为 occurrence 3（step0=0、step1=1、Back=2、Next=3）。
        await ClickAsync(renderer, 3);

        Assert.Equal([1], stepReports);
        Assert.Equal([1], currentReports);
        var markup = renderer.Markup();
        Assert.Contains("确认区", markup);
        Assert.DoesNotContain("姓名填写区", markup);
        // 活动步按钮落 aria-current="step"。
        Assert.Contains("aria-current=\"step\"", StepButtonTag(NavSection(markup), 1));
        Assert.DoesNotContain("aria-current", StepButtonTag(NavSection(markup), 0));
        // 第 0 步通过校验落 Completed 态（check 图标）。
        Assert.Contains("M20 6L9 17l-5-5", markup);
        // 末步显示 Complete 而非 Next。
        Assert.Contains(">Complete<", markup);
        Assert.DoesNotContain(">Next<", markup);
    }

    [Fact]
    public async Task Back回退应不经验校且首步按钮隐藏()
    {
        var (renderer, _) = await MountAsync(TwoSteps());

        // 首步：Back 落 atom:invisible 且禁用。
        var backFirst = System.Text.RegularExpressions.Regex.Matches(renderer.Markup(), """<button[^>]*>""")[2].Value;
        Assert.Contains("atom:invisible", backFirst);
        Assert.Contains(" disabled", backFirst);

        await ClickAsync(renderer, 3); // Next → 第 1 步
        await ClickAsync(renderer, 2); // Back → 第 0 步

        var markup = renderer.Markup();
        Assert.Contains("姓名填写区", markup);
        // 回退后 Back 重新隐藏禁用。
        var backAgain = System.Text.RegularExpressions.Regex.Matches(markup, """<button[^>]*>""")[2].Value;
        Assert.Contains("atom:invisible", backAgain);
    }

    [Fact]
    public async Task 点击已访问步骤应直接导航而拒绝未访问步骤()
    {
        var (renderer, _) = await MountAsync(ThreeSteps());

        // 第 0 步点击未访问的第 2 步按钮（occurrence 2）：拒绝，仍在第 0 步。
        await ClickAsync(renderer, 2);
        Assert.Contains("内容一", renderer.Markup());
        Assert.DoesNotContain("内容三", renderer.Markup());

        // 前进到第 1 步后，第 0 步已访问：点击回跳（第 0 步 Next=4）。
        await ClickAsync(renderer, 4); // Next → 第 1 步
        Assert.Contains("内容二", renderer.Markup());
        await ClickAsync(renderer, 0); // 点第 0 步按钮
        Assert.Contains("内容一", renderer.Markup());
    }

    [Fact]
    public async Task AllowSkipAhead应允许点击未访问步骤()
    {
        var (renderer, _) = await MountAsync(
            ThreeSteps(),
            new Dictionary<string, object?> { [nameof(FormWizard.AllowSkipAhead)] = true });

        await ClickAsync(renderer, 2); // 未访问的第 2 步按钮
        Assert.Contains("内容三", renderer.Markup());
        Assert.Contains("aria-current=\"step\"", StepButtonTag(NavSection(renderer.Markup()), 2));
    }

    [Fact]
    public async Task 自定义导航模板应替换默认按钮且隐藏内置导航()
    {
        var (renderer, _) = await MountAsync(
            TwoSteps(),
            new Dictionary<string, object?>
            {
                [nameof(FormWizard.NavigationTemplate)] = (RenderFragment)(builder =>
                    builder.AddMarkupContent(0, "<button type=\"button\">自定义上一步</button><button type=\"button\">自定义下一步</button>")),
            });

        var markup = renderer.Markup();
        Assert.Contains("自定义下一步", markup);
        Assert.DoesNotContain(">Next<", markup);
        Assert.DoesNotContain(">Back<", markup);
    }

    [Fact]
    public async Task ShowNavigation关闭应整体隐藏导航区()
    {
        var (renderer, _) = await MountAsync(
            TwoSteps(),
            new Dictionary<string, object?> { [nameof(FormWizard.ShowNavigation)] = false });

        var markup = renderer.Markup();
        Assert.DoesNotContain("data-slot=\"wizard-navigation\"", markup);
        Assert.DoesNotContain(">Next<", markup);
        Assert.Contains("姓名填写区", markup); // 内容区不受影响
    }

    // ------------------------------------------------------------------
    // 可选步骤与完成沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 可选步骤Skip应从当前可选步前进并标记minus图标()
    {
        var (renderer, _) = await MountAsync(ThreeSteps(secondOptional: true));

        // 第 0 步（非可选，无 Skip）：指示器三按钮 0-2、Back=3、Next=4。
        await ClickAsync(renderer, 4); // Next → 第 1 步（可选步）

        // 第 1 步（可选非末步）：Back=3、Skip=4、Next=5。点 Skip（occurrence 4）。
        await ClickAsync(renderer, 4); // Skip → 第 2 步

        var markup = renderer.Markup();
        Assert.Contains("内容三", markup);
        // 第 1 步落 Skipped 态：圆圈显示 minus 图标。
        Assert.Contains("M5 12h14", markup);
        // 末步无 Skip 按钮。
        Assert.DoesNotContain(">Skip<", markup);
    }

    [Fact]
    public async Task 末步Complete应上报OnComplete()
    {
        var completed = 0;
        var (renderer, _) = await MountAsync(TwoSteps(), new Dictionary<string, object?>
        {
            [nameof(FormWizard.OnComplete)] = EventCallback.Factory.Create(new object(), () => completed++),
        });

        await ClickAsync(renderer, 3); // Next → 末步
        await ClickAsync(renderer, 3); // Complete（末步 occurrence：Back=2、Complete=3）

        Assert.Equal(1, completed);
        Assert.Contains("确认区", renderer.Markup());
    }

    [Fact]
    public async Task 末步Complete校验失败应阻止完成()
    {
        var completed = 0;
        var (renderer, _) = await MountAsync(
            builder => AddStep(builder, 0, "唯一步", "内容", validator: () => false),
            new Dictionary<string, object?>
            {
                [nameof(FormWizard.OnComplete)] = EventCallback.Factory.Create(new object(), () => completed++),
            });

        // 单步即末步：指示器一按钮 0、Back=1、Complete=2。
        await ClickAsync(renderer, 2);

        Assert.Equal(0, completed);
        var markup = renderer.Markup();
        // 校验失败：停留当前步。Invalid 态（x 图标）对当前步不可达——
        // GetStepState 对 index==currentStep 短路返回 Active（同构）
        // 故此处只能断言"没走成"而非错误态图标。
        Assert.Contains("内容", markup);
        Assert.Contains(">Complete<", markup);
        Assert.Contains("aria-current=\"step\"", StepButtonTag(NavSection(markup), 0));
    }

    // ------------------------------------------------------------------
    // 校验沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task Validator失败应阻止Next前进()
    {
        var stepReports = new List<int>();
        // 单步即末步，无 Next 按钮（IsLastStep → Complete），故铺两步以获得 Next（occurrence 3）。
        var (renderer, _) = await MountAsync(
            builder =>
            {
                AddStep(builder, 0, "受阻步", "受阻内容", validator: () => false);
                AddStep(builder, 10, "后续步", "后续内容");
            },
            new Dictionary<string, object?>
            {
                [nameof(FormWizard.OnStepChanged)] =
                    EventCallback.Factory.Create<int>(new object(), v => stepReports.Add(v)),
            });

        await ClickAsync(renderer, 3); // Next 被校验吞下

        Assert.Empty(stepReports);
        var markup = renderer.Markup();
        Assert.Contains("受阻内容", markup);
        Assert.DoesNotContain("后续内容", markup);
        // Invalid 态对当前步不可达（GetStepState 短路）：圆圈仍为活动态而无 x 图标。
        Assert.Contains("aria-current=\"step\"", StepButtonTag(NavSection(markup), 0));
        Assert.DoesNotContain("M18 6L6 18M6 6l12 12", markup);
    }

    [Fact]
    public async Task EditContext字段校验失败应阻止Next且通过后放行()
    {
        var doc = new WizardDoc();
        var renderer = CreateRenderer();
        var context = CreateValidatedContext(doc);

        await renderer.MountAsync<WizardEditContextHost>(new Dictionary<string, object?>
        {
            [nameof(WizardEditContextHost.EditContext)] = context,
            [nameof(WizardEditContextHost.FieldNames)] = new[] { nameof(WizardDoc.Name) },
        });
        await SettleAsync(renderer);

        // 初始 Name 为空：Next 被 EditContext 校验吞下，停留第 0 步
        //（Invalid 态对当前步不可达，故断言停留 + aria-current 而非 x 图标）。
        await ClickAsync(renderer, 3);
        var markup = renderer.Markup();
        Assert.Contains("账户填写区", markup);
        Assert.Contains("aria-current=\"step\"", StepButtonTag(NavSection(markup), 0));

        // 填入合法值后放行。
        doc.Name = "张三";
        await ClickAsync(renderer, 3);
        Assert.Contains("完成区", renderer.Markup());
    }

    /// <summary>分步校验模型：Name 为空即无效（假校验器执行）。</summary>
    internal sealed class WizardDoc
    {
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// 构造挂接假校验器的 EditContext：Name 为空时向该字段写入消息。
    /// </summary>
    private static EditContext CreateValidatedContext(WizardDoc doc)
    {
        var context = new EditContext(doc);
        var messages = new ValidationMessageStore(context);
        var field = new FieldIdentifier(doc, nameof(WizardDoc.Name));

        void Validate()
        {
            messages.Clear();
            if (string.IsNullOrEmpty(doc.Name))
            {
                messages.Add(field, "必须填写名称");
            }
            context.NotifyValidationStateChanged();
        }

        context.OnFieldChanged += (_, _) => Validate();
        context.OnValidationRequested += (_, _) => Validate();
        return context;
    }

    /// <summary>
    /// 承载 EditContext 与向导子树的宿主：第 0 步带 FieldNames 分步校验，第 1 步普通步。
    /// </summary>
    internal sealed class WizardEditContextHost : ComponentBase
    {
        [Parameter] public EditContext? EditContext { get; set; }
        [Parameter] public string[]? FieldNames { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingValue<EditContext>>(0);
            builder.AddAttribute(1, nameof(CascadingValue<EditContext>.Value), EditContext);
            builder.AddAttribute(2, nameof(CascadingValue<EditContext>.ChildContent), (RenderFragment)(tb =>
            {
                tb.OpenComponent<FormWizard>(10);
                tb.AddAttribute(11, nameof(FormWizard.ChildContent), (RenderFragment)(sb =>
                {
                    sb.OpenComponent<WizardStep>(20);
                    sb.AddAttribute(21, nameof(WizardStep.Title), "账户");
                    sb.AddAttribute(22, nameof(WizardStep.FieldNames), FieldNames);
                    sb.AddAttribute(23, nameof(WizardStep.ChildContent), (RenderFragment)(cb => cb.AddContent(0, "账户填写区")));
                    sb.CloseComponent();

                    sb.OpenComponent<WizardStep>(30);
                    sb.AddAttribute(31, nameof(WizardStep.Title), "完成");
                    sb.AddAttribute(32, nameof(WizardStep.ChildContent), (RenderFragment)(cb => cb.AddContent(0, "完成区")));
                    sb.CloseComponent();
                }));
                tb.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    // ------------------------------------------------------------------
    // 受控与状态保留
    // ------------------------------------------------------------------

    [Fact]
    public async Task 受控CurrentStep应同步活动步()
    {
        var (renderer, _) = await MountAsync(TwoSteps(), new Dictionary<string, object?>
        {
            [nameof(FormWizard.CurrentStep)] = 0,
            [nameof(FormWizard.CurrentStepChanged)] =
                EventCallback.Factory.Create<int>(new object(), _ => { }),
        });

        // 首帧第 0 步。
        Assert.Contains("姓名填写区", renderer.Markup());

        // 父级推进到 1：步骤已注册，受控值生效（OnParametersSet 同步需带
        // CurrentStepChanged 委托，即 @bind-CurrentStep 语义）。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(FormWizard.CurrentStep)] = 1,
        }));
        await SettleAsync(renderer);
        var markup = renderer.Markup();
        Assert.Contains("确认区", markup);
        Assert.Contains("aria-current=\"step\"", StepButtonTag(NavSection(markup), 1));

        // 父级拉回 0。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(FormWizard.CurrentStep)] = 0,
        }));
        await SettleAsync(renderer);
        Assert.Contains("姓名填写区", renderer.Markup());
    }

    /// <summary>
    /// 行为（保留）：受控初值于首帧无效——OnParametersSet 同步发生在
    /// 子步注册之前，steps.Count 仍为 0，钳位表达式回落 0；需父级二次参数设置
    /// 才生效（见上一条测试）。
    /// </summary>
    [Fact]
    public async Task 受控CurrentStep首帧应因步骤未注册而钳位到首步()
    {
        var (renderer, wizard) = await MountAsync(TwoSteps(), new Dictionary<string, object?>
        {
            [nameof(FormWizard.CurrentStep)] = 1,
            [nameof(FormWizard.CurrentStepChanged)] =
                EventCallback.Factory.Create<int>(new object(), _ => { }),
        });

        // 参数面保留父级传入的 1，但内部 currentStep 已钳位为 0。
        Assert.Equal(1, wizard.CurrentStep);
        Assert.Contains("姓名填写区", renderer.Markup());
        Assert.DoesNotContain("确认区", renderer.Markup());
    }

    [Fact]
    public async Task RetainStepState关闭时后退应清除前向访问状态()
    {
        var (renderer, _) = await MountAsync(
            ThreeSteps(),
            new Dictionary<string, object?> { [nameof(FormWizard.RetainStepState)] = false });

        await ClickAsync(renderer, 4); // Next → 第 1 步（三步向导第 0 步 Next=4）
        await ClickAsync(renderer, 0); // 回点第 0 步

        // 前向状态已清：第 1 步按钮重新落 disabled（未访问不可点）。
        Assert.Contains(" disabled", StepButtonTag(NavSection(renderer.Markup()), 1));
    }

    [Fact]
    public async Task 默认RetainStepState应保留前向访问状态()
    {
        var (renderer, _) = await MountAsync(ThreeSteps());

        await ClickAsync(renderer, 4); // Next → 第 1 步（三步向导第 0 步 Next=4）
        await ClickAsync(renderer, 0); // 回点第 0 步

        // 默认保留：第 1 步仍可点击（无 disabled）。
        Assert.DoesNotContain(" disabled", StepButtonTag(NavSection(renderer.Markup()), 1));
    }

    // ------------------------------------------------------------------
    // 禁用步骤与越界使用
    // ------------------------------------------------------------------

    [Fact]
    public async Task 禁用步骤应不可导航且前进时被跳过()
    {
        var (renderer, _) = await MountAsync(builder =>
        {
            AddStep(builder, 0, "可用步", "可用内容");
            AddStep(builder, 10, "禁用步", "禁用内容", disabled: true);
        });

        var markup = renderer.Markup();
        // 禁用步按钮落 disabled 且无 aria-current。
        Assert.Contains(" disabled", StepButtonTag(NavSection(markup), 1));

        // 唯一前向步被禁用：第 0 步即末步，显示 Complete。
        Assert.Contains(">Complete<", markup);
        Assert.DoesNotContain(">Next<", markup);

        // 点击禁用步按钮（occurrence 1）：无变化。
        await ClickAsync(renderer, 1);
        Assert.Contains("可用内容", renderer.Markup());
    }

    [Fact]
    public async Task WizardStep脱离FormWizard应抛InvalidOperationException()
    {
        var renderer = CreateRenderer();
        var mount = renderer.MountAsync<WizardStep>(new Dictionary<string, object?>
        {
            [nameof(WizardStep.Title)] = "孤立步",
        });
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await mount);
    }

    // ------------------------------------------------------------------
    // 缺省参数面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 缺省参数面应与一致()
    {
        var (_, wizard) = await MountAsync(TwoSteps());

        Assert.Equal(WizardLayout.Horizontal, wizard.Layout);
        Assert.True(wizard.ShowNavigation);
        Assert.False(wizard.AllowSkipAhead);
        Assert.True(wizard.RetainStepState);
        Assert.Equal(0, wizard.CurrentStep);
        Assert.Null(wizard.NextLabel);
        Assert.Null(wizard.BackLabel);
        Assert.Null(wizard.CompleteLabel);
        Assert.Null(wizard.NavigationTemplate);
        Assert.False(wizard.CurrentStepChanged.HasDelegate);
        Assert.False(wizard.OnComplete.HasDelegate);
        Assert.False(wizard.OnStepChanged.HasDelegate);
    }

    [Fact]
    public async Task 自定义标签应替换默认导航文案()
    {
        var (renderer, _) = await MountAsync(TwoSteps(), new Dictionary<string, object?>
        {
            [nameof(FormWizard.BackLabel)] = "上一步",
            [nameof(FormWizard.NextLabel)] = "下一步",
            [nameof(FormWizard.CompleteLabel)] = "提交",
        });

        var markup = renderer.Markup();
        Assert.Contains(">上一步<", markup);
        Assert.Contains(">下一步<", markup);
        Assert.DoesNotContain(">Back<", markup);

        await ClickAsync(renderer, 3); // 下一步 → 末步
        Assert.Contains(">提交<", renderer.Markup());
    }
}
