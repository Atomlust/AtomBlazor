using System.ComponentModel;
using AtomBlazor.Primitives.Utilities;
using Microsoft.JSInterop;

namespace AtomBlazor.Primitives.Services;

/// <summary>
/// 使用 JavaScript interop 做全局键盘处理的全局快捷键服务实现。
/// </summary>
public class KeyboardShortcutService : IKeyboardShortcutService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly Dictionary<string, ShortcutRegistration> _shortcuts = new();
    private readonly SemaphoreSlim _moduleLock = new(1, 1);
    private IJSObjectReference? _module;
    private DotNetObjectReference<KeyboardShortcutService>? _dotNetRef;
    private bool _suspended;
    private bool _disposed;
    private int _registrationCounter;

    /// <summary>
    /// 初始化 <see cref="KeyboardShortcutService"/> 类的新实例。
    /// </summary>
    /// <param name="jsRuntime">用于调用键盘处理函数的 JavaScript 运行时。</param>
    public KeyboardShortcutService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc />
    public bool IsSuspended => _suspended;

    /// <inheritdoc />
    public async Task<IDisposable> RegisterAsync(string shortcut, Func<Task> callback)
    {
        var id = $"shortcut_{Interlocked.Increment(ref _registrationCounter)}";
        return await RegisterAsync(shortcut, callback, id);
    }

    /// <inheritdoc />
    public async Task<IDisposable> RegisterAsync(string shortcut, Func<Task> callback, string id)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(KeyboardShortcutService));

        var parsed = KeyboardShortcut.Parse(shortcut);
        var normalizedKey = parsed.GetNormalizedKey();

        await EnsureInitializedAsync();

 // 在服务侧登记
        var registration = new ShortcutRegistration(id, parsed, callback);
        _shortcuts[normalizedKey] = registration;

 // 在 JavaScript 侧登记
        if (_module != null)
        {
            // BL0016（.NET 11）：断线/预渲染守卫留给调用方组件，服务只做转发
#pragma warning disable BL0016
            await _module.InvokeVoidAsync("keyboardShortcuts.registerShortcut", normalizedKey);
#pragma warning restore BL0016
        }

        return new ShortcutHandle(this, normalizedKey, id);
    }

    /// <inheritdoc />
    public void Unregister(string id)
    {
        var toRemove = _shortcuts.FirstOrDefault(kvp => kvp.Value.Id == id);
        if (toRemove.Key != null)
        {
            UnregisterInternal(toRemove.Key);
        }
    }

    /// <inheritdoc />
    public void Suspend() => _suspended = true;

    /// <inheritdoc />
    public void Resume() => _suspended = false;

    /// <summary>
 /// 由 JavaScript 在已登记的快捷键被按下时调用。
    /// </summary>
    /// <param name="normalizedKey">规范化后的组合键（例如 "ctrl+n"）。</param>
    [JSInvokable]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public async Task HandleShortcutAsync(string normalizedKey)
    {
        if (_suspended || _disposed)
        {
            return;
        }

        // 输入校验——拒绝明显非法的输入
        if (string.IsNullOrWhiteSpace(normalizedKey) || normalizedKey.Length > 50)
        {
            return;
        }

        if (_shortcuts.TryGetValue(normalizedKey, out var registration))
        {
            try
            {
                await registration.Callback();
            }
            catch (Exception ex)
            {
                // 记录并吞下异常——不让它传播回 JS
                Console.Error.WriteLine($"处理键盘快捷键 '{normalizedKey}' 时出错：{ex.Message}");
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        GC.SuppressFinalize(this);
        _disposed = true;
        _shortcuts.Clear();

        if (_module != null)
        {
            try
            {
                await _module.InvokeVoidAsync("keyboardShortcuts.dispose");
            }
            catch
            {
                // 模块可能已被释放（例如页面导航时）
            }
        }

        _dotNetRef?.Dispose();
        _moduleLock.Dispose();
    }

    private async Task EnsureInitializedAsync()
    {
        if (_module != null)
        {
            return;
        }

        await _moduleLock.WaitAsync();
        try
        {
            if (_module == null)
            {
                _dotNetRef = DotNetObjectReference.Create(this);
                _module = await PrimitiveModules.GetAsync(_jsRuntime);

                // BL0016（.NET 11）：初始化失败必须抛回调用方（注册即失败），
                // 断线恢复语义由组件决定
#pragma warning disable BL0016
                await _module.InvokeVoidAsync("keyboardShortcuts.initialize", _dotNetRef);
#pragma warning restore BL0016
            }
        }
        finally
        {
            _moduleLock.Release();
        }
    }

    private void UnregisterInternal(string normalizedKey)
    {
        if (_shortcuts.Remove(normalizedKey) && _module != null)
        {
            // 从 JS 侧注销采用发后即忘
#pragma warning disable BL0016 // 同上：注销尽力而为，断线时随电路一并消散
            _ = _module.InvokeVoidAsync("keyboardShortcuts.unregisterShortcut", normalizedKey).AsTask();
#pragma warning restore BL0016
        }
    }

    private sealed class ShortcutRegistration
    {
        public string Id { get; }
        public KeyboardShortcut Shortcut { get; }
        public Func<Task> Callback { get; }

        public ShortcutRegistration(string id, KeyboardShortcut shortcut, Func<Task> callback)
        {
            Id = id;
            Shortcut = shortcut;
            Callback = callback;
        }
    }

    private sealed class ShortcutHandle : IDisposable
    {
        private readonly KeyboardShortcutService _service;
        private readonly string _normalizedKey;
        private readonly string _id;
        private bool _disposed;

        public ShortcutHandle(KeyboardShortcutService service, string normalizedKey, string id)
        {
            _service = service;
            _normalizedKey = normalizedKey;
            _id = id;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (!_service._disposed)
            {
                _service.UnregisterInternal(_normalizedKey);
            }
        }
    }
}
