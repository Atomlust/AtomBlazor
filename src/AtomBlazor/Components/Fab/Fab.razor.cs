using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AtomBlazor;

/// <summary>
/// 悬浮操作按钮（FAB）：一个带投影的按钮，固定在屏幕角落并承载该屏的主要操作。
/// </summary>
/// <remarks>
/// <para>
/// 每屏只应使用一个。第二个悬浮按钮会与第一个争夺注意力，让两者都不再像"主要操作"；
/// 其他操作请交给普通的 <see cref="Button"/>。
/// </para>
/// <para>
/// 本按钮基于 <see cref="Button"/> 构建，因此同样可以作为浮层的 AsChild 触发器。
/// 速度拨号（speed dial）就是这么组合的：把 FAB 放进
/// <c>DropdownMenuTrigger AsChild="true"</c>（DropdownMenu 家族已交付），菜单即可从按钮弹出。
/// </para>
/// <para>
/// 特性：
/// - 默认按主题圆角半径呈圆角方形；<see cref="Shape"/> 可改为正圆
/// - 纯图标，或通过 <see cref="ChildContent"/> 加标签展宽
/// - 五种停靠方位，可钉在视口或最近的定位祖先上
/// - 安全区留白，避开机身的 Home 指示条
/// - 逻辑方位语义，角落随书写方向移动
/// </para>
/// <para>
/// 纯图标 FAB 没有文本，必须提供 <see cref="AriaLabel"/> 才有可读名称。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Fab AriaLabel="New message" OnClick="Compose"&gt;
///     &lt;Icon&gt;&lt;LucideIcon Name="plus" Size="24" /&gt;&lt;/Icon&gt;
/// &lt;/Fab&gt;
///
/// &lt;Fab Placement="FabPlacement.BottomStart" OnClick="Compose"&gt;
///     &lt;Icon&gt;&lt;LucideIcon Name="plus" Size="20" /&gt;&lt;/Icon&gt;
///     &lt;ChildContent&gt;New task&lt;/ChildContent&gt;
/// &lt;/Fab&gt;
/// </code>
/// </example>
public partial class Fab : ComponentBase
{
    /// <summary>
    /// 获取或设置视觉样式变体。
    /// </summary>
    [Parameter]
    public FabVariant Variant { get; set; } = FabVariant.Primary;

    /// <summary>
    /// 获取或设置按钮尺寸。
    /// </summary>
    [Parameter]
    public FabSize Size { get; set; } = FabSize.Default;

    /// <summary>
    /// 获取或设置按钮的停靠方位。
    /// </summary>
    [Parameter]
    public FabPlacement Placement { get; set; } = FabPlacement.BottomEnd;

    /// <summary>
    /// 获取或设置角落形状。默认 <see cref="FabShape.Rounded"/>，跟随主题圆角半径；
    /// 需要正圆时用 <see cref="FabShape.Circle"/>，按钮带上标签后即成胶囊形。
    /// </summary>
    [Parameter]
    public FabShape Shape { get; set; } = FabShape.Rounded;

    /// <summary>
    /// 获取或设置按钮是否钉在视口上。为 <c>false</c> 时改为钉在最近的位置受限祖先上，
    /// 卡片或面板内需要这样做。<see cref="FabPlacement.Inline"/> 时忽略。
    /// </summary>
    [Parameter]
    public bool Fixed { get; set; } = true;

    /// <summary>
    /// 获取或设置按钮是否避开设备安全区，以免被 Home 指示条或刘海遮挡。
    /// <see cref="FabPlacement.Inline"/> 时忽略。
    /// </summary>
    [Parameter]
    public bool SafeArea { get; set; } = true;

    /// <summary>
    /// 获取或设置渲染在按钮内的图标。
    /// </summary>
    [Parameter]
    public RenderFragment? Icon { get; set; }

    /// <summary>
    /// 获取或设置标签。提供标签会加宽按钮，让操作自解释。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置无障碍名称。纯图标按钮没有可供屏幕阅读器朗读的文本，必须提供。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置按钮是否禁用。
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置按钮被点击时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <summary>
    /// 获取或设置应用到按钮上的额外 CSS 类。它们优先于组件自身的类，
    /// 是修改贴边偏移量的推荐方式。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到按钮元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>是否提供了标签——标签会把按钮加宽到超出图标。</summary>
    private bool IsExtended => ChildContent is not null;

    private ButtonVariant MappedVariant => Variant switch
    {
        FabVariant.Secondary => ButtonVariant.Secondary,
        FabVariant.Surface => ButtonVariant.Outline,
        FabVariant.Destructive => ButtonVariant.Destructive,
        _ => ButtonVariant.Default
    };

    private string CssClass => ClassNames.cn(
        "atom:shadow-lg atom:transition-shadow atom:duration-200",
        "atom:motion-reduce:transition-none atom:hover:shadow-xl",
        // rounded-md 即主题圆角，与 Button 用的同一个类。
        Shape == FabShape.Circle ? "atom:rounded-full" : "atom:rounded-md",
        (Size, IsExtended) switch
        {
            (FabSize.Small, false) => "atom:h-10 atom:w-10 atom:p-0",
            (FabSize.Small, true) => "atom:h-10 atom:px-4",
            (FabSize.Large, false) => "atom:h-16 atom:w-16 atom:p-0",
            (FabSize.Large, true) => "atom:h-16 atom:px-8 atom:text-base",
            (_, false) => "atom:h-14 atom:w-14 atom:p-0",
            (_, true) => "atom:h-14 atom:px-6"
        },
        PlacementCssClass,
        SafeAreaCssClass,
        Class
    );

    private string? PlacementCssClass => Placement switch
    {
        FabPlacement.Inline => null,
        FabPlacement.BottomStart => ClassNames.cn(AnchorCssClass, "atom:bottom-6 atom:start-6"),
        FabPlacement.BottomCenter => ClassNames.cn(AnchorCssClass, "atom:bottom-6 atom:left-1/2 atom:-translate-x-1/2"),
        FabPlacement.TopEnd => ClassNames.cn(AnchorCssClass, "atom:top-6 atom:end-6"),
        FabPlacement.TopStart => ClassNames.cn(AnchorCssClass, "atom:top-6 atom:start-6"),
        _ => ClassNames.cn(AnchorCssClass, "atom:bottom-6 atom:end-6")
    };

    /// <summary>位于底部导航栏之上、浮层层级之下。</summary>
    private string AnchorCssClass => Fixed ? "atom:fixed atom:z-40" : "atom:absolute atom:z-40";

    private string? SafeAreaCssClass => !SafeArea || Placement == FabPlacement.Inline
        ? null
        : Placement switch
        {
            FabPlacement.TopEnd or FabPlacement.TopStart => "atom:mt-[env(safe-area-inset-top)]",
            _ => "atom:mb-[env(safe-area-inset-bottom)]"
        };
}
