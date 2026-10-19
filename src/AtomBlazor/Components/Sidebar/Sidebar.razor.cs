using AtomBlazor.Primitives;
using Microsoft.AspNetCore.Components;
using System;

namespace AtomBlazor;

public partial class Sidebar : IDisposable
{
    [CascadingParameter]
    private SidebarContext? Context { get; set; }

    private SidebarContext? _subscribedContext;

    /// <summary>
    /// 在侧边栏内部渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 应用到侧边栏的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 可折叠行为：折叠时仅显示图标，展开时全宽。
    /// 默认为 true。
    /// </summary>
    [Parameter]
    public bool Collapsible { get; set; } = true;

    /// <summary>
    /// 应用到侧边栏元素的附加属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private bool MobileOpen
    {
        get => Context?.OpenMobile ?? false;
        set => Context?.SetOpenMobile(value);
    }

    private SheetSide GetSheetSide()
    {
        return Context?.Side == SidebarSide.Right
            ? SheetSide.Right
            : SheetSide.Left;
    }

    private string GetDesktopClasses()
    {
        // hidden md:flex 可防止桌面侧边栏在移动端屏幕上一闪而过
        // ——在 JS 检测到断点并切换到 Sheet 渲染器之前它会先短暂可见。
        var baseClasses = "atom:group atom:peer atom:hidden atom:md:flex atom:flex-col atom:text-sidebar-foreground atom:shrink-0";

        // 按变体区分的类
        var variantClasses = Context?.Variant switch
        {
            SidebarVariant.Floating => "atom:bg-sidebar atom:border atom:border-sidebar-border atom:rounded-lg atom:shadow-lg atom:data-[state=closed]:border-0 atom:data-[state=closed]:shadow-none",
            SidebarVariant.Inset => "atom:bg-sidebar",
            _ => "atom:bg-sidebar atom:border-r atom:border-sidebar-border atom:data-[state=closed]:border-0"
        };

        // 按侧边区分的定位
        var sideClasses = Context?.Side == SidebarSide.Right
            ? "atom:border-r-0 atom:border-l atom:data-[state=closed]:border-0"
            : "";

        // 宽度与过渡类
        var widthClasses = Collapsible && Context?.CollapsedMode != SidebarCollapsedMode.Pill
            ? "atom:w-[var(--sidebar-width)] atom:transition-[width] atom:duration-200 atom:ease-linear atom:data-[state=collapsed]:w-[var(--sidebar-width-icon)]"
            : "atom:w-[var(--sidebar-width)] atom:transition-[width,opacity] atom:duration-200 atom:ease-linear atom:data-[state=closed]:w-0 atom:data-[state=closed]:opacity-0 atom:overflow-hidden";

        // 按变体区分的布局类
        var layoutClasses = Context?.Variant switch
        {
            SidebarVariant.Floating => "atom:fixed atom:top-2 atom:bottom-2 atom:z-10",
            SidebarVariant.Inset => "atom:relative atom:h-full",
            _ => "atom:sticky atom:top-0 atom:min-h-full"
        };

        // 为 floating/default 变体补充左右定位
        if (Context?.Variant != SidebarVariant.Inset)
        {
            if (Context?.Variant == SidebarVariant.Floating)
            {
                layoutClasses += Context?.Side == SidebarSide.Right ? " atom:right-2" : " atom:left-2";
            }
            else
            {
                layoutClasses += Context?.Side == SidebarSide.Right ? " atom:right-0" : " atom:left-0";
            }
        }

        // cn() 在带前缀的成对类之外保留裸 `group`/`peer` 标记，令调用方自己的
        // `group-data-[collapsible=icon]:*` 能够解析到本元素上。
        return ClassNames.cn(
            baseClasses,
            variantClasses,
            sideClasses,
            widthClasses,
            layoutClasses,
            "atom:motion-reduce:transition-none",
            Class
        );
    }

    private string GetMobileClasses()
    {
        // 移动端 sheet 本有专属宽度令牌 --sidebar-width-mobile，样式表定义了它
        // 却无人使用：sheet 实际取的是桌面端 --sidebar-width，因此设置
        // 移动端令牌不改变任何东西（实况，照源锁存）。
        return ClassNames.cn(
            "atom:w-[var(--sidebar-width-mobile,var(--sidebar-width))] atom:bg-sidebar atom:p-0 atom:flex atom:flex-col",
            "atom:[&>button]:hidden", // 隐藏 Sheet 默认的关闭按钮
            Class
        );
    }

    private string GetDataState()
    {
        if (Context == null)
        {
            return "collapsed";
        }

        if (Context.Open)
        {
            return "expanded";
        }

        // 未打开时：Collapsible 则返回 "collapsed"（显示图标），非 Collapsible 则 "closed"（完全隐藏）
        return Collapsible && Context.CollapsedMode != SidebarCollapsedMode.Pill ? "collapsed" : "closed";
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        // 仅当上下文引用变化时才重新订阅
        if (Context != _subscribedContext)
        {
            // 取消订阅旧上下文
            _subscribedContext?.StateChanged -= OnContextStateChanged;

            // 订阅新上下文
            Context?.StateChanged += OnContextStateChanged;

            _subscribedContext = Context;
        }
    }

    private void OnContextStateChanged(object? sender, EventArgs e) =>
        // 侧边栏状态变化时强制重渲染
        StateHasChanged();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _subscribedContext?.StateChanged -= OnContextStateChanged;
        _subscribedContext = null;
    }
}
