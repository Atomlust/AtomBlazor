using Microsoft.JSInterop;

namespace AtomBlazor.Demo.Services;

/// <summary>
/// 把折叠菜单的展开状态持久化到浏览器 localStorage 的服务。
/// </summary>
public class CollapsibleStateService : IAsyncDisposable
{
    private readonly IJSRuntime jsRuntime;

    // CSS 域改名规则的镜像：bb/ 存储前缀改为 atom。
    private const string localStoragePrefix = "atom:collapsible:";
    private IJSObjectReference? utilsModule;

    /// <summary>构造服务。</summary>
    public CollapsibleStateService(IJSRuntime jsRuntime)
    {
        this.jsRuntime = jsRuntime;
    }

    private async ValueTask<IJSObjectReference?> GetUtilsModuleAsync()
    {
        if (utilsModule is not null)
        {
            return utilsModule;
        }

        try
        {
            utilsModule = await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/AtomBlazor.Demo.Shared/js/demo-utils.js");
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
        {
            // BL0016：JS 互操作须容错；电路断开时返回 null 由调用方静默处理
        }

        return utilsModule;
    }

    /// <summary>
    /// 读取某个折叠菜单的保存状态。
    /// </summary>
    /// <param name="key">折叠菜单的唯一标识（例如 "sidebar-primitives-menu"）</param>
    /// <param name="defaultValue">没有保存状态时的默认值</param>
    /// <returns>菜单应展开返回 true，否则 false</returns>
    public async Task<bool> GetStateAsync(string key, bool defaultValue = false)
    {
        try
        {
            var storageKey = localStoragePrefix + key;
            var value = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", storageKey);

            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return bool.TryParse(value, out var result) && result;
        }
        catch
        {
            // localStorage 不可用或出错时返回默认值
            return defaultValue;
        }
    }

    /// <summary>
    /// 保存某个折叠菜单的状态。
    /// 以 fire-and-forget 方式触发 JS 互操作，避免阻塞 UI 线程。
    /// </summary>
    /// <param name="key">折叠菜单的唯一标识</param>
    /// <param name="isOpen">菜单是否展开</param>
    public void SetState(string key, bool isOpen)
    {
        var storageKey = localStoragePrefix + key;
        _ = SetStateFireAndForgetAsync(storageKey, isOpen);
    }

    private async Task SetStateFireAndForgetAsync(string storageKey, bool isOpen)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", storageKey, isOpen.ToString());
        }
        catch
        {
            // localStorage 不可用时静默失败
        }
    }

    /// <summary>
    /// 清除某个折叠菜单的保存状态。
    /// </summary>
    /// <param name="key">折叠菜单的唯一标识</param>
    public async Task ClearStateAsync(string key)
    {
        try
        {
            var storageKey = localStoragePrefix + key;
            await jsRuntime.InvokeVoidAsync("localStorage.removeItem", storageKey);
        }
        catch
        {
            // localStorage 不可用时静默失败
        }
    }

    /// <summary>
    /// 清除全部已保存的折叠菜单状态。
    /// </summary>
    public async Task ClearAllStatesAsync()
    {
        try
        {
            var module = await GetUtilsModuleAsync();
            if (module is null)
            {
                return;
            }

            var keys = await module.InvokeAsync<string[]>("getLocalStorageKeysByPrefix", localStoragePrefix);

            foreach (var key in keys)
            {
                await jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
            }
        }
        catch
        {
            // localStorage 不可用时静默失败
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (utilsModule != null)
        {
            try
            {
                await utilsModule.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路已断开，忽略
            }
        }

        GC.SuppressFinalize(this);
    }
}
