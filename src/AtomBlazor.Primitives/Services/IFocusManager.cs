using Microsoft.AspNetCore.Components;

namespace AtomBlazor.Primitives.Services;

/// <summary>
/// 管理组件内部焦点的服务，包括为模态对话框提供焦点捕获。
/// </summary>
public interface IFocusManager
{
    /// <summary>
    /// 把焦点捕获在指定容器元素之内。Tab 与 Shift+Tab 只会在容器内部循环移动焦点。
    /// </summary>
    /// <param name="container">要捕获焦点的容器元素。</param>
    /// <returns>在焦点捕获建立后完成的任务。</returns>
    public Task<IAsyncDisposable> TrapFocus(ElementReference container);

    /// <summary>
    /// 把焦点捕获在指定容器元素之内，并决定捕获建立后把焦点落在哪里。
    /// </summary>
    /// <param name="container">要捕获焦点的容器元素。</param>
    /// <param name="initialFocus">
    /// 捕获建立后聚焦的对象。默认为第一个可 Tab 到的后代元素，即历史行为。
    /// </param>
    /// <param name="initialFocusElement">
    /// 显式指定要聚焦的元素，优先级高于 <paramref name="initialFocus"/> 与任何
    /// <c>data-autofocus</c> 元素。若它不在 <paramref name="container"/> 内部则被忽略。
    /// </param>
    /// <returns>在焦点捕获建立后完成的任务。</returns>
    /// <remarks>
    /// 解析顺序为：先 <paramref name="initialFocusElement"/>，再容器内携带
    /// <c>data-autofocus</c> 的元素，最后才是 <paramref name="initialFocus"/>。
    ///
    /// 这是一个默认接口实现，因此加入它没有破坏既有的 <see cref="IFocusManager"/> 实现方。
    /// 默认实现忽略两个新参数、退回到单参数重载，所以自定义实现照常工作——
    /// 只是在自己重写此方法之前得不到初始焦点行为。
    /// </remarks>
    public Task<IAsyncDisposable> TrapFocus(
        ElementReference container,
        FocusTrapInitialFocus initialFocus,
        ElementReference? initialFocusElement = null)
        => TrapFocus(container);

    /// <summary>
    /// 把焦点恢复到先前聚焦的元素。通常在关闭对话框后用于把焦点送回触发器。
    /// </summary>
    /// <param name="previousElement">要恢复焦点的元素；为 null 时什么也不做。</param>
    /// <returns>在焦点恢复后完成的任务。</returns>
    public Task RestoreFocus(ElementReference? previousElement);

    /// <summary>
    /// 聚焦容器内第一个可聚焦元素。
    /// </summary>
    /// <param name="container">要搜索可聚焦元素的容器。</param>
    /// <returns>在焦点设置完成后任务即完成。</returns>
    public Task FocusFirst(ElementReference container);

    /// <summary>
    /// 聚焦容器内最后一个可聚焦元素。
    /// </summary>
    /// <param name="container">要搜索可聚焦元素的容器。</param>
    /// <returns>在焦点设置完成后任务即完成。</returns>
    public Task FocusLast(ElementReference container);
}
