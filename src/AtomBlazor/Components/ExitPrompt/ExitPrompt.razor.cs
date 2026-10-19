using AtomBlazor.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace AtomBlazor;

/// <summary>
/// 在有未保存工作时拦下导航，放行之前先问一句。
/// </summary>
/// <remarks>
/// <para>
/// 表单变脏时置 <see cref="Enabled"/>，工作保存后清掉。在它置位期间，覆盖两种离开：
/// </para>
/// <list type="bullet">
/// <item>
/// 应用内部的导航被拦下，并由本组件自己的对话框问询，于是文案与按钮都和操作界面的
/// 其余部分保持一致。
/// </item>
/// <item>
/// 关闭标签页、重新加载、或点出站外链接时，武装浏览器自带的提示。浏览器在这里会
/// 刻意忽略自定义消息并展示它自己的文案；脚本改不了这一点——这恰恰是应用内情形
/// 要另行处理的原因。
/// </item>
/// </list>
/// <para>
/// 在导航真的被拦下之前，本组件什么都不渲染。把它放在所守护的表单附近；一个页面上
/// 放多个，各自守护各自的工作，第一个被启用的负责问询。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;ExitPrompt Enabled="@(editContext?.IsModified() == true)" /&gt;
///
/// &lt;ExitPrompt Enabled="@hasChanges"
/// Message="Your draft is not saved yet."
/// OnPrompted="() => analytics.Track(&quot;exit-prompt&quot;)" /&gt;
/// </code>
/// </example>
public partial class ExitPrompt : ComponentBase, IAsyncDisposable
{
 // 实例标识前缀照本仓去品牌惯例扫成 atom- 形（原文「id 前缀 atom-cascader-」
 // -i 把余下九族一并跟进，Dialog/Dock/Gantt/ListBox/Scheduler/ScrollToTop/Tabs/ScrollArea 同批）。
    // 该串会渲染进使用方页面的 id 属性，属对外可见标记而非纯内部常量。
    private readonly string instanceId = $"atom-exit-prompt-{Guid.NewGuid():N}";
    private IDisposable? navigationRegistration;
    private IJSObjectReference? module;
    private string? pending;
    private bool armedInBrowser;

    /// <summary>
    /// 获取或设置是否存在需要守护的未保存工作。本值为 <c>false</c> 期间不拦截任何东西。
    /// </summary>
    [Parameter]
    public bool Enabled { get; set; }

    /// <summary>
    /// 获取或设置对话框标题。默认取本地化的 <c>ExitPrompt.Title</c>。
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    /// 获取或设置对话框正文。默认取本地化的 <c>ExitPrompt.Message</c>。
    /// 浏览器自带提示不使用它，那个提示展示自己的文案。
    /// </summary>
    [Parameter]
    public string? Message { get; set; }

    /// <summary>
    /// 获取或设置取消导航的按钮文案。
    /// </summary>
    [Parameter]
    public string? StayLabel { get; set; }

    /// <summary>
    /// 获取或设置放行导航、放弃工作的按钮文案。
    /// </summary>
    [Parameter]
    public string? LeaveLabel { get; set; }

    /// <summary>
    /// 获取或设置导航被拦下、对话框已展示时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback OnPrompted { get; set; }

    /// <summary>
    /// 获取或设置导航被放行时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback OnLeft { get; set; }

    /// <summary>
    /// 获取或设置导航被取消、工作被保留时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback OnStayed { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnInitialized() =>
        navigationRegistration = Navigation.RegisterLocationChangingHandler(HandleLocationChangingAsync);

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender) => await SyncBrowserPromptAsync();

    /// <summary>
    /// 在 <see cref="Enabled"/> 变化时武装或解除浏览器自带提示。
    /// 放在渲染之后调用，以确保 interop 可用；状态未变则保留监听器。
    /// </summary>
    private async Task SyncBrowserPromptAsync()
    {
        if (Enabled == armedInBrowser)
        {
            return;
        }

        try
        {
 // 有意偏差（记录在案）：按实例直连 exit-prompt.js 模块路径
            // 本仓库统一改走共享 components-core.js barrel（camelCase 点分标识符）
            // 规则见 PrimitiveJsModuleTests。
            module ??= await ComponentModules.GetCoreAsync(JS);
            await module.InvokeVoidAsync(Enabled ? "exitPrompt.arm" : "exitPrompt.disarm", instanceId);
            armedInBrowser = Enabled;
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException
            or TaskCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            // 预渲染，或电路已经不在了。应用内导航仍受守护，因为那条路径从不离开 C#。
        }
    }

    /// <summary>
    /// 拒绝一次应用内导航，并记住它原本要去哪里。
    /// </summary>
    /// <remarks>
    /// 处理器直接返回，而不是等待答复。过去把路由器一直悬着等按钮按下、再在处理器
    /// 内部导航，结果是 URL 变了而新页面没渲染——重发的导航在仍在处理被拒导航的管线
    /// 里成了再入操作。改为先拒绝、再由按钮自己的处理器导航，两者就分开了。
    /// </remarks>
    private ValueTask HandleLocationChangingAsync(LocationChangingContext context)
    {
        if (!Enabled || pending is not null)
        {
            return ValueTask.CompletedTask;
        }

        context.PreventNavigation();
        pending = context.TargetLocation;

        return new ValueTask(PromptAsync());
    }

    private async Task PromptAsync()
    {
        await InvokeAsync(StateHasChanged);

        if (OnPrompted.HasDelegate)
        {
            await OnPrompted.InvokeAsync();
        }
    }

    private Task HandleOpenChangedAsync(bool open) => open ? Task.CompletedTask : StayAsync();

    private async Task StayAsync()
    {
        if (pending is null)
        {
            return;
        }

        pending = null;
        // BL0012：与一致保留显式 StateHasChanged——对话框须在 OnStayed 回调之前
        // 立即落定关闭；行为面属实况，bug-for-bug 优先。
#pragma warning disable BL0012
        StateHasChanged();
#pragma warning restore BL0012

        if (OnStayed.HasDelegate)
        {
            await OnStayed.InvokeAsync();
        }
    }

    private async Task LeaveAsync()
    {
        var target = pending;

        if (target is null)
        {
            return;
        }

        // 在重发导航之前先把守护放下，同一个处理器就不会把重发也拦下来。
        Enabled = false;
        pending = null;
        await SyncBrowserPromptAsync();
        StateHasChanged();

        if (OnLeft.HasDelegate)
        {
            await OnLeft.InvokeAsync();
        }

        Navigation.NavigateTo(target);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        navigationRegistration?.Dispose();

        if (module is not null && armedInBrowser)
        {
            try
            {
                await module.InvokeVoidAsync("exitPrompt.disarm", instanceId);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException
                or TaskCanceledException or ObjectDisposedException)
            {
                // 电路已经不在了；监听器随之而去。
            }
        }

        GC.SuppressFinalize(this);
    }
}
