namespace AtomBlazor.Primitives.Services;

/// <summary>
/// 控制 AtomBlazor 浮层默认渲染方式的全局选项。
/// 经 <c>AddAtomBlazorPrimitives(configure)</c> 提供，运行时读取。
/// </summary>
public class OverlayRenderingOptions
{
    /// <summary>
    /// 浮层组件自身未指定策略时采用的默认 <see cref="OverlayRenderingStrategy"/>。
    /// 默认为 <see cref="OverlayRenderingStrategy.JavaScript"/>，保持既有行为不变。
    /// 设为 <see cref="OverlayRenderingStrategy.Native"/> 可让整个应用改用原生渲染
    /// （浏览器不支持时自动回退）。
    /// </summary>
    public OverlayRenderingStrategy DefaultStrategy { get; set; } = OverlayRenderingStrategy.JavaScript;
}
