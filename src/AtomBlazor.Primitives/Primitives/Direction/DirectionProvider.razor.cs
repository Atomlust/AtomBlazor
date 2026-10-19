using Microsoft.AspNetCore.Components;

namespace AtomBlazor.Primitives;

/// <summary>
/// 为其内容设定书写方向，使本库的布局在右到左语言环境下镜像翻转。
/// </summary>
/// <remarks>
/// <para>
/// 把应用布局（含 <see cref="PortalHost"/>）包在它里面——经门户宿主渲染的浮层
/// 同样需要方向信息。宿主位于 provider 之外的浮层也能拿到：浮动元素在打开时
/// 会从其触发元素复制方向，正如它复制局部主题一样。
/// </para>
/// <para>
/// 该 provider 不渲染自己的盒子（<c>display: contents</c>），加入它不会改变任何布局。
/// 它写出 <c>dir</c> 特性——本库的逻辑 CSS 属性据此响应——并级联一个
/// <see cref="DirectionContext"/>，供 CSS 无法替我们做的决定使用，例如方向键应向哪边移动。
/// </para>
/// <para>
/// 在没有 provider 的情况下使用的组件遵循 <see cref="System.Globalization.CultureInfo.CurrentCulture"/>，
/// 即本组件出现之前库的既有行为。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;DirectionProvider Direction="TextDirection.RightToLeft"&gt;
///     @Body
///     &lt;PortalHost /&gt;
/// &lt;/DirectionProvider&gt;
/// </code>
/// </example>
public partial class DirectionProvider : ComponentBase
{
    private DirectionContext context = new() { Direction = TextDirection.Auto };

    /// <summary>
    /// 获取或设置书写方向。默认为 <see cref="TextDirection.Auto"/>，即跟随当前区域文化。
    /// </summary>
    [Parameter]
    public TextDirection Direction { get; set; } = TextDirection.Auto;

    /// <summary>
    /// 获取或设置该方向所作用的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // 每次变化都新建实例，令级联通知到后代；对象其余时间不可变，
        // 在 provider 的整个生命周期内共享同一份。
        if (context.Direction != Direction)
        {
            context = new DirectionContext { Direction = Direction };
        }
    }
}
