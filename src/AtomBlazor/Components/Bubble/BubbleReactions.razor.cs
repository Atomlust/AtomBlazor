using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 锚定在气泡表面上的回应徽章（reaction chips）行。
/// </summary>
/// <remarks>
/// 通过 <c>Side</c>/<c>Align</c> 决定骑跨气泡边缘的位置（上下各偏移四分之三自身高度），
/// 并用 <c>ring-card</c> 在气泡背景上"切"出悬浮缺口；含 button 子元素时取消内边距。
/// </remarks>
public partial class BubbleReactions : ComponentBase
{
    /// <summary>
    /// 获取或设置回应锚定的气泡边。
    /// </summary>
    [Parameter]
    public BubbleReactionsSide Side { get; set; } = BubbleReactionsSide.Bottom;

    /// <summary>
    /// 获取或设置沿气泡边缘的水平对齐。
    /// </summary>
    [Parameter]
    public BubbleReactionsAlign Align { get; set; } = BubbleReactionsAlign.End;

    /// <summary>
    /// 获取或设置应用到回应行的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置回应内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置捕获的额外属性，展开到回应容器。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取回应容器计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:absolute atom:z-10 atom:flex atom:w-fit atom:shrink-0 atom:items-center atom:justify-center atom:gap-1 atom:rounded-full atom:bg-muted atom:px-1.5 atom:py-0.5 atom:text-sm atom:ring-3 atom:ring-card atom:has-[button]:p-0",
        Side == BubbleReactionsSide.Top ? "atom:top-0 atom:-translate-y-3/4" : "atom:bottom-0 atom:translate-y-3/4",
        Align == BubbleReactionsAlign.Start ? "atom:start-3" : "atom:end-3",
        Class
    );
}
