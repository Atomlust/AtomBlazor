using System.ComponentModel;
using AtomBlazor.Primitives.Utilities;
using Microsoft.JSInterop;

namespace AtomBlazor.Primitives.Services;

/// <summary>
/// 使用 JavaScript interop 做全局键盘处理的全局快捷键服务实现。
/// </summary>
/// <remarks>
/// 同一组合键可以同时被多个注册占用。服务按组合键维护引用计数，
/// 只有最后一个注册释放时才通知 JavaScript 注销该键。
/// </remarks>
public class KeyboardShortcutService : IKeyboardShortcutService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly object _stateLock = new();
    private readonly Dictionary<string, List<ShortcutRegistration>> _registrationsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ShortcutRegistration> _registrationsById = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _moduleLock = new(1, 1);
    private readonly SemaphoreSlim _registrationLock = new(1, 1);
    private IJSObjectReference? _module;
    private DotNetObjectReference<KeyboardShortcutService>? _dotNetRef;
    private volatile bool _suspended;
    private volatile bool _disposed;
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
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ObjectDisposedException.ThrowIf(_disposed, nameof(KeyboardShortcutService));

        var parsed = KeyboardShortcut.Parse(shortcut);
        var normalizedKey = parsed.GetNormalizedKey();

        await EnsureInitializedAsync();

        // 串行化注册：JS 侧的登记/注销顺序与引用计数保持一致。
        await _registrationLock.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(KeyboardShortcutService));

            var registration = new ShortcutRegistration(id, normalizedKey, callback);
            ShortcutRegistration? previous;
            bool isFirstForKey;
            bool keyChanged;

            lock (_stateLock)
            {
                _registrationsById.TryGetValue(id, out previous);
                if (previous is not null)
                {
                    RemoveRegistration(previous);
                }

                keyChanged = previous is null || previous.NormalizedKey != normalizedKey;
                isFirstForKey = !_registrationsByKey.ContainsKey(normalizedKey);
                AddRegistration(registration);
            }

            try
            {
                if (keyChanged && previous is not null && IsKeyUnregistered(previous.NormalizedKey))
                {
                    await InvokeUnregisterAsync(previous.NormalizedKey);
                }

                if (isFirstForKey)
                {
                    await InvokeRegisterAsync(normalizedKey);
                }
            }
            catch
            {
                // 恢复注册表，避免 JS 调用失败后留下无法释放的登记。
                lock (_stateLock)
                {
                    if (_registrationsById.TryGetValue(id, out var current) && ReferenceEquals(current, registration))
                    {
                        RemoveRegistration(registration);
                    }

                    if (previous is not null)
                    {
                        AddRegistration(previous);
                    }
                }

                throw;
            }

            return new ShortcutHandle(this, id);
        }
        finally
        {
            _registrationLock.Release();
        }
    }

    /// <inheritdoc />
    public void Unregister(string id)
    {
        if (_disposed || string.IsNullOrEmpty(id))
        {
            return;
        }

        string? keyToUnregister = null;

        lock (_stateLock)
        {
            if (!_registrationsById.TryGetValue(id, out var registration))
            {
                return;
            }

            RemoveRegistration(registration);
            if (!_registrationsByKey.ContainsKey(registration.NormalizedKey))
            {
                keyToUnregister = registration.NormalizedKey;
            }
        }

        if (keyToUnregister is not null)
        {
            // 从 JS 侧注销采用发后即忘；断线时随电路一并消散。
            UnregisterFromJs(keyToUnregister);
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

        ShortcutRegistration[] registrations;
        lock (_stateLock)
        {
            if (!_registrationsByKey.TryGetValue(normalizedKey, out var keyRegistrations) ||
                keyRegistrations.Count == 0)
            {
                return;
            }

            registrations = keyRegistrations.ToArray();
        }

        // 回调在锁外执行：允许回调内注册/注销或打开对话框。
        foreach (var registration in registrations)
        {
            try
            {
                await registration.Callback();
            }
            catch (Exception ex)
            {
                // 记录并吞下异常——不让它传播回 JS，也不阻断同键的其他回调。
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

        lock (_stateLock)
        {
            _registrationsByKey.Clear();
            _registrationsById.Clear();
        }

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
        _registrationLock.Dispose();
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
            if (_module != null)
            {
                return;
            }

            var module = await PrimitiveModules.GetAsync(_jsRuntime);
            var dotNetRef = DotNetObjectReference.Create(this);
            try
            {
                // BL0016（.NET 11）：初始化失败必须抛回调用方（注册即失败），
                // 断线恢复语义由组件决定
#pragma warning disable BL0016
                await module.InvokeVoidAsync("keyboardShortcuts.initialize", dotNetRef);
#pragma warning restore BL0016
            }
            catch
            {
                // 初始化失败时不发布半初始化的模块，允许后续注册重试。
                dotNetRef.Dispose();
                throw;
            }

            _dotNetRef = dotNetRef;
            _module = module;
        }
        finally
        {
            _moduleLock.Release();
        }
    }

    private bool IsKeyUnregistered(string normalizedKey)
    {
        lock (_stateLock)
        {
            return !_registrationsByKey.ContainsKey(normalizedKey);
        }
    }

    private async Task InvokeRegisterAsync(string normalizedKey)
    {
        if (_module is null)
        {
            return;
        }

        // BL0016（.NET 11）：断线/预渲染守卫留给调用方组件，服务只做转发
#pragma warning disable BL0016
        await _module.InvokeVoidAsync("keyboardShortcuts.registerShortcut", normalizedKey);
#pragma warning restore BL0016
    }

    private async Task InvokeUnregisterAsync(string normalizedKey)
    {
        if (_module is null)
        {
            return;
        }

#pragma warning disable BL0016
        await _module.InvokeVoidAsync("keyboardShortcuts.unregisterShortcut", normalizedKey);
#pragma warning restore BL0016
    }

    private void UnregisterFromJs(string normalizedKey)
    {
        if (_module == null)
        {
            return;
        }

#pragma warning disable BL0016 // 同上：注销尽力而为，断线时随电路一并消散
        _ = _module.InvokeVoidAsync("keyboardShortcuts.unregisterShortcut", normalizedKey).AsTask();
#pragma warning restore BL0016
    }

    // 以下两个方法必须在 _stateLock 内调用。
    private void AddRegistration(ShortcutRegistration registration)
    {
        if (!_registrationsByKey.TryGetValue(registration.NormalizedKey, out var keyRegistrations))
        {
            keyRegistrations = [];
            _registrationsByKey[registration.NormalizedKey] = keyRegistrations;
        }

        keyRegistrations.Add(registration);
        _registrationsById[registration.Id] = registration;
    }

    private void RemoveRegistration(ShortcutRegistration registration)
    {
        _registrationsById.Remove(registration.Id);

        if (_registrationsByKey.TryGetValue(registration.NormalizedKey, out var keyRegistrations))
        {
            keyRegistrations.Remove(registration);
            if (keyRegistrations.Count == 0)
            {
                _registrationsByKey.Remove(registration.NormalizedKey);
            }
        }
    }

    private sealed class ShortcutRegistration
    {
        public string Id { get; }
        public string NormalizedKey { get; }
        public Func<Task> Callback { get; }

        public ShortcutRegistration(string id, string normalizedKey, Func<Task> callback)
        {
            Id = id;
            NormalizedKey = normalizedKey;
            Callback = callback;
        }
    }

    private sealed class ShortcutHandle : IDisposable
    {
        private readonly KeyboardShortcutService _service;
        private readonly string _id;
        private bool _disposed;

        public ShortcutHandle(KeyboardShortcutService service, string id)
        {
            _service = service;
            _id = id;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _service.Unregister(_id);
        }
    }
}
