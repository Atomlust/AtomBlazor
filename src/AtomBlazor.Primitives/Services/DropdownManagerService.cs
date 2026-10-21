namespace AtomBlazor.Primitives.Services;

/// <summary>
/// 全局管理下拉组件的接口，确保同一时刻最多只有一个下拉处于打开状态。
/// </summary>
public interface IDropdownManagerService
{
    /// <summary>
 /// 把一个下拉登记为打开。若已有另一个下拉打开，会先关闭它。
    /// </summary>
    /// <param name="dropdownId">下拉的唯一标识（应在多次渲染之间保持稳定）</param>
    /// <param name="closeCallback">当下拉需要被管理器关闭时要调用的回调</param>
    public void RegisterOpen(string dropdownId, Action closeCallback);

    /// <summary>
    /// 在下拉自然关闭时（用户操作或点击外部）注销它。
    /// </summary>
    /// <param name="dropdownId">要注销的下拉 ID</param>
    public void Unregister(string dropdownId);

    /// <summary>
 /// 获取指定下拉当前是否登记为打开。
    /// </summary>
    /// <param name="dropdownId">要检查的下拉 ID</param>
 /// <returns>该下拉当前登记为打开时为 true</returns>
    public bool IsOpen(string dropdownId);
}

/// <summary>
/// 全局管理下拉组件，确保同一时刻最多只有一个下拉处于打开状态。
/// </summary>
/// <remarks>
/// 本服务应在 DI 容器中注册为 Scoped。
/// 当下拉（Select、Combobox、DropdownMenu、Popover 等）打开时，它向本服务登记自身；
/// 若已有另一个下拉打开，本服务会先关闭旧的再登记新的。
///
/// 对 Blazor Server 中多个异步操作并发访问本服务的场景是线程安全的。
/// </remarks>
public class DropdownManagerService : IDropdownManagerService
{
    private readonly object gate = new();
    private string? currentDropdownId;
    private Action? currentCloseCallback;

    /// <summary>
 /// 把一个下拉登记为打开。若已有另一个下拉打开，会先关闭它。
    /// </summary>
    /// <param name="dropdownId">下拉的唯一标识（应在多次渲染之间保持稳定）</param>
    /// <param name="closeCallback">当下拉需要被管理器关闭时要调用的回调</param>
    /// <exception cref="ArgumentException">dropdownId 为 null、空白或含非法字符时抛出。</exception>
    public void RegisterOpen(string dropdownId, Action closeCallback)
    {
        // 校验输入，防止状态被恶意篡改
        if (string.IsNullOrWhiteSpace(dropdownId))
        {
            throw new ArgumentException("下拉 ID 不能为 null 或空。", nameof(dropdownId));
        }

        ArgumentNullException.ThrowIfNull(closeCallback);

        // 基础净化——ID 只应包含安全字符
        if (!IsValidDropdownId(dropdownId))
        {
            throw new ArgumentException("下拉 ID 含有非法字符。", nameof(dropdownId));
        }

        string? previousDropdownId = null;
        Action? callbackToClose = null;

        lock (gate)
        {
            // 若当前打开的下拉与被登记者不同，记下要关闭的那个；
            // 状态先切换到新下拉，确保旧回调里的注销不会误伤新登记。
            if (currentDropdownId != null && currentDropdownId != dropdownId)
            {
                previousDropdownId = currentDropdownId;
                callbackToClose = currentCloseCallback;
            }

            currentDropdownId = dropdownId;
            currentCloseCallback = closeCallback;
        }

        // 关闭回调可能引发渲染、注销甚至再次登记，必须在锁外执行；
        // 其失败不应回滚已经生效的新下拉登记。
        if (callbackToClose is not null)
        {
            try
            {
                callbackToClose();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"关闭下拉 '{previousDropdownId}' 时出错：{ex.Message}");
            }
        }
    }

    /// <summary>
    /// 校验下拉 ID 只包含安全字符。
    /// </summary>
    private static bool IsValidDropdownId(string id)
    {
        // 允许字母数字、连字符、下划线与冒号（生成式 ID 的常见字符）
        foreach (var c in id)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != ':')
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 在下拉自然关闭时（用户操作或点击外部）注销它。
    /// </summary>
    /// <param name="dropdownId">要注销的下拉 ID</param>
    public void Unregister(string dropdownId)
    {
        lock (gate)
        {
            if (currentDropdownId == dropdownId)
            {
                currentDropdownId = null;
                currentCloseCallback = null;
            }
        }
    }

    /// <summary>
 /// 获取指定下拉当前是否登记为打开。
    /// </summary>
    /// <param name="dropdownId">要检查的下拉 ID</param>
 /// <returns>该下拉当前登记为打开时为 true</returns>
    public bool IsOpen(string dropdownId)
    {
        lock (gate)
        {
            return currentDropdownId == dropdownId;
        }
    }
}
