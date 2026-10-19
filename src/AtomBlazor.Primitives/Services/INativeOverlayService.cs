using Microsoft.AspNetCore.Components;

namespace AtomBlazor.Primitives.Services;

/// <summary>
/// 解析组件实际生效的 <see cref="OverlayRenderingStrategy"/>，并驱动浏览器的原生
/// 浮层元素（目前是 <c>&lt;dialog&gt;</c>）。
/// </summary>
public interface INativeOverlayService
{
    /// <summary>
    /// 获取浏览器是否支持原生 <c>&lt;dialog&gt;</c> 元素的 <c>showModal()</c>。
    /// 每个作用域解析一次并缓存。JS interop 不可用时（例如预渲染期间）返回 false。
    /// 用于请求原生策略时的诊断。
    /// </summary>
    public Task<bool> IsDialogSupportedAsync();

    /// <summary>
    /// 同步解析组件应采用的渲染策略（渲染期间调用也安全）。
    /// 非 null 的 <paramref name="requested"/>（组件自身的参数）优先；
    /// 否则应用全局默认值。
    /// </summary>
    public OverlayRenderingStrategy ResolveStrategy(OverlayRenderingStrategy? requested);

    /// <summary>
    /// 让后续所有解析都不再返回 <see cref="OverlayRenderingStrategy.Native"/>，
    /// 于是每个浮层都改走 JavaScript 策略渲染。
    /// </summary>
    /// <remarks>
    /// 当组件发现浏览器没有 <c>&lt;dialog&gt;.showModal()</c> 时调用。
    /// 支持与否只能经 JS interop 探测，而首次解析策略时 interop 尚不可用——
    /// 所以回退是事后纠正而非事前预测。此前这种情况只记一条日志警告：元素照样渲染、
    /// 从不进入顶层（top layer），用户得到一个既无模态行为、也无焦点捕获的对话框。
    /// </remarks>
    public void FallBackToJavaScript();

    /// <summary>
    /// 以模态（顶层）方式打开一个 <c>&lt;dialog&gt;</c> 元素。
    /// </summary>
    public Task ShowDialogAsync(ElementReference element);

    /// <summary>
    /// 关闭一个 <c>&lt;dialog&gt;</c> 元素。
    /// </summary>
    public Task CloseDialogAsync(ElementReference element, string? returnValue = null);

    /// <summary>
    /// 在对话框打开后聚焦其内容（回退到第一个可聚焦元素）。
    /// </summary>
    public Task FocusDialogAsync(ElementReference element);

    /// <summary>
    /// 把焦点恢复到打开对话框的元素。
    /// </summary>
    public Task FocusTriggerAsync(ElementReference element);

    /// <summary>
    /// 把原生 <c>&lt;dialog&gt;</c> 的生命周期事件（Escape/取消、关闭、遮罩点击）接到
    /// 一个 .NET 实例上。返回的句柄必须被释放以移除监听器。
    /// </summary>
    public Task<IAsyncDisposable> SetupDialogAsync(ElementReference element, object dotNetRef);
}
