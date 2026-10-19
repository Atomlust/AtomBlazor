using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor.Primitives.Services;

/// <summary>
/// 注册并管理全局键盘快捷键的服务。快捷键在整个应用范围内生效，
/// 菜单关闭时同样会触发。
/// </summary>
public interface IKeyboardShortcutService : IAsyncDisposable
{
    /// <summary>
    /// 注册一个按下时调用回调的键盘快捷键。
    /// </summary>
    /// <param name="shortcut">快捷键字符串（例如 "Ctrl+N"、"Shift+Delete"、"Ctrl+Shift+S"）。</param>
    /// <param name="callback">按下时要调用的异步回调。</param>
    /// <returns>用于注销该快捷键的可释放句柄。</returns>
    /// <example>
    /// <code>
    /// var registration = await KeyboardShortcuts.RegisterAsync("Ctrl+S", async () => await SaveAsync());
    /// // 之后注销：
    /// registration.Dispose();
    /// </code>
    /// </example>
    public Task<IDisposable> RegisterAsync(string shortcut, Func<Task> callback);

    /// <summary>
    /// 注册一个带唯一标识、便于日后注销的键盘快捷键。
    /// </summary>
    /// <param name="shortcut">快捷键字符串（例如 "Ctrl+N"）。</param>
    /// <param name="callback">按下时要调用的异步回调。</param>
    /// <param name="id">本次注册的唯一标识。</param>
    /// <returns>用于注销该快捷键的可释放句柄。</returns>
    public Task<IDisposable> RegisterAsync(string shortcut, Func<Task> callback, string id);

    /// <summary>
    /// 按唯一标识注销快捷键。
    /// </summary>
    /// <param name="id">注册时使用的标识。</param>
    public void Unregister(string id);

    /// <summary>
    /// 临时挂起全部快捷键。适用于打开模态对话框等应禁用快捷键的场景。
    /// </summary>
    public void Suspend();

    /// <summary>
    /// 恢复此前挂起的快捷键。
    /// </summary>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Resume 是正确的领域术语")]
    public void Resume();

    /// <summary>
    /// 获取快捷键当前是否处于挂起状态。
    /// </summary>
    public bool IsSuspended { get; }
}
