using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

// 命名消歧说明：本类型名为 Image，与框架的 Microsoft.AspNetCore.Components.Web.Image
// 同名不同义。本库仅 _Imports.razor 引入了该 Web 命名空间，而框架该命名空间下
// 实际并不存在 Image 类型，且本工程未引用 System.Drawing，故无冲突。
// 若未来使用者同时引入其他 Image 类型，请使用全名 AtomBlazor.Components.Image。

/// <summary>
/// 一种图片组件：当来源无法加载时展示合理的回退内容。
/// </summary>
/// <remarks>
/// <para>
/// 裸的 <c>&lt;img&gt;</c> 遇到失效 URL 会留下破图标框和 alt 文本，看起来像页面 bug
/// 而不是数据缺失。本组件会换成一个回退：你自己的内容、另一个 URL，或一个中性占位图标。
/// </para>
/// <para>
/// 回退会保留图片的可访问名称——它以 <c>role="img"</c> 渲染并携带相同的
/// <see cref="Alt"/>——所以屏幕阅读器依然能描述本应出现的内容。
/// </para>
/// <para>
/// 图片默认懒加载。首屏（above the fold）图片请关闭 <see cref="Lazy"/>，
/// 那里懒加载只会拖慢最大内容绘制而不是帮助它。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Image Src="@user.AvatarUrl" Alt="@user.Name" Fit="ImageFit.Cover" Class="atom:h-24 atom:w-24 atom:rounded-full"&gt;
///     &lt;Fallback&gt;&lt;span class="atom:text-sm atom:font-medium"&gt;@user.Initials&lt;/span&gt;&lt;/Fallback&gt;
/// &lt;/Image&gt;
/// </code>
/// </example>
public partial class Image : ComponentBase
{
    private bool failed;
    private bool loaded;
    private string? loadedSrc;

    /// <summary>
    /// 获取或设置图片 URL。
    /// </summary>
    [Parameter, EditorRequired]
    public string? Src { get; set; }

    /// <summary>
    /// 获取或设置替代文本。装饰性图片应传空字符串，
    /// 这会使其对屏幕阅读器隐藏。
    /// </summary>
    [Parameter, EditorRequired]
    public string? Alt { get; set; }

    /// <summary>
    /// 获取或设置在图片无法加载时展示的内容。优先级高于 <see cref="FallbackSrc"/>。
    /// </summary>
    [Parameter]
    public RenderFragment? Fallback { get; set; }

    /// <summary>
    /// 获取或设置在第一张图失败时尝试的第二个 URL。该 URL 再失败不做重试，
    /// 因此成对缺失也不会循环。
    /// </summary>
    [Parameter]
    public string? FallbackSrc { get; set; }

    /// <summary>
    /// 获取或设置图片填充盒子尺寸的方式。
    /// </summary>
    [Parameter]
    public ImageFit Fit { get; set; } = ImageFit.None;

    /// <summary>
    /// 获取或设置浏览器是否推迟加载直到图片接近视口。
    /// 默认为 <c>true</c>；首屏请关闭。
    /// </summary>
    [Parameter]
    public bool Lazy { get; set; } = true;

    /// <summary>
    /// 获取或设置在图片加载失败时调用的回调，使失败可以被记录或上报
    /// 而不仅仅是被画出来。
    /// </summary>
    [Parameter]
    public EventCallback OnError { get; set; }

    /// <summary>
    /// 获取或设置在图片加载完成时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback OnLoad { get; set; }

    /// <summary>
    /// 获取或设置额外的 CSS 类。它们同时作用于图片与回退，
    /// 因此圆角缩略图回退后依然是圆角。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到图片元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // 新的来源值得一次新的尝试；否则把一个组件实例复用于多行的列表
        // 会一直显示第一行的失败状态。
        if (!string.Equals(loadedSrc, Src, StringComparison.Ordinal))
        {
            loadedSrc = Src;
            failed = false;
            loaded = false;
        }
    }

    private async Task HandleError()
    {
        failed = true;

        if (OnError.HasDelegate)
        {
            await OnError.InvokeAsync();
        }
    }

    private async Task HandleLoad()
    {
        loaded = true;

        if (OnLoad.HasDelegate)
        {
            await OnLoad.InvokeAsync();
        }
    }

    private string CssClass => ClassNames.cn(
        "atom:block atom:max-w-full",
        Fit switch
        {
            ImageFit.Cover => "atom:object-cover",
            ImageFit.Contain => "atom:object-contain",
            ImageFit.Fill => "atom:object-fill",
            ImageFit.ScaleDown => "atom:object-scale-down",
            _ => null
        },
        Class);

    /// <summary>
    /// 回退继承使用者传入的类，因此固定尺寸或圆角会在切换后保留，
    /// 并把内容居中。
    /// </summary>
    private string FallbackCssClass => ClassNames.cn(
        "atom:flex atom:items-center atom:justify-center atom:bg-muted atom:text-muted-foreground",
        Class);
}
