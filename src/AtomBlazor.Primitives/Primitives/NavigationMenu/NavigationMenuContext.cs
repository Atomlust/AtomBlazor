using Microsoft.AspNetCore.Components;

namespace AtomBlazor.Primitives;

/// <summary>
/// 导航菜单的根上下文。管理哪个项处于打开、关闭定时器，
/// 以及为键盘导航登记触发器。
/// </summary>
public class NavigationMenuContext : IDisposable
{
    private readonly Action stateChanged;
    private readonly List<(object Owner, ElementReference Element)> triggerRefs = new();
    private CancellationTokenSource? closeTimerCts;

    /// <summary>
    /// 当前打开的菜单项的值，全部关闭时为 <c>null</c>。
    /// </summary>
    public string? ActiveItem { get; private set; }

    /// <summary>
    /// 下拉内是否启用键盘导航。
    /// </summary>
    public bool EnableKeyboardNavigation { get; }

    /// <summary>
 /// 已登记的触发器数量。
    /// </summary>
    public int TriggerCount => triggerRefs.Count;

    /// <summary>
    /// 创建一个新的 <see cref="NavigationMenuContext"/>。
    /// </summary>
    public NavigationMenuContext(Action stateChanged, bool enableKeyboardNavigation)
    {
        this.stateChanged = stateChanged;
        EnableKeyboardNavigation = enableKeyboardNavigation;
    }

    /// <summary>
    /// 设置激活的菜单项并触发一次重渲染。
    /// </summary>
    public void SetActiveItem(string? value)
    {
        ActiveItem = value;
        stateChanged();
    }

    /// <summary>
 /// 登记一个触发器，或替换其中已为它持有的元素引用。
    /// </summary>
    /// <param name="owner">触发器组件，在列表中用作它的标识。</param>
    /// <param name="triggerRef">触发器的元素，它只在首渲染后才真实。</param>
    /// <remarks>
    /// 以组件为键，而不是以一个调用方必须记住的位置为键。此前那对按下标寻址的
    /// 方法无法表达移除——取走一个条目会使其后发放的每个下标都发生
    /// 位移——所以触发器从来不会被注销，方向键导航也会不断
    /// 跳到已经离开页面的按钮上。
    /// </remarks>
    public void RegisterTrigger(object owner, ElementReference triggerRef)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var index = IndexOf(owner);

        if (index >= 0)
        {
            triggerRefs[index] = (owner, triggerRef);
            return;
        }

        triggerRefs.Add((owner, triggerRef));
    }

    /// <summary>
    /// 移除一个触发器。在触发器被释放时调用它。
    /// </summary>
    /// <param name="owner">传给 <see cref="RegisterTrigger"/> 的触发器组件。</param>
    public void UnregisterTrigger(object owner)
    {
        var index = IndexOf(owner);

        if (index >= 0)
        {
            triggerRefs.RemoveAt(index);
        }
    }

    /// <summary>
 /// 获取指定下标处的触发器元素，按登记顺序。
    /// </summary>
    public ElementReference? GetTriggerAt(int index)
    {
        if (index >= 0 && index < triggerRefs.Count)
        {
            return triggerRefs[index].Element;
        }

        return null;
    }

    /// <summary>
 /// 该触发器在列表中的当前位置；未登记时为 -1。
    /// </summary>
    /// <param name="owner">传给 <see cref="RegisterTrigger"/> 的触发器组件。</param>
    /// <remarks>
    /// 需要某个触发器的下标时读取此值——不要记住一个下标。一个触发器离开页面时会带走它的
    /// 条目，其后的每个下标都会位移。
    /// </remarks>
    public int TriggerIndexOf(object owner) => IndexOf(owner);

    private int IndexOf(object owner) =>
        triggerRefs.FindIndex(entry => ReferenceEquals(entry.Owner, owner));

    /// <summary>
    /// 启动一个共享的关闭定时器。延迟之后关闭全部菜单。
    /// 用 <see cref="CancelCloseTimer"/> 取消。
    /// </summary>
    public async void StartCloseTimer()
    {
        CancelCloseTimer();
        closeTimerCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(150, closeTimerCts.Token);
            SetActiveItem(null);
        }
        catch (TaskCanceledException)
        {
            // 定时器已被取消
        }
        catch (Exception)
        {
            // async void：没有人 await 它，所以逃逸出的异常没有可到达的调用方，
            // Blazor Server 会将其视为致命——电路随之关闭，用户看到重连遮罩。这个方法做的
            // 一切都是尽力而为，没有任何一部分值得付出那样的代价。
        }
    }

    /// <summary>
    /// 取消一切挂起的关闭定时器。
    /// </summary>
    public void CancelCloseTimer()
    {
        closeTimerCts?.Cancel();
        closeTimerCts?.Dispose();
        closeTimerCts = null;
    }

    /// <summary>
    /// 释放关闭定时器所占用的资源。
    /// </summary>
    public void Dispose()
    {
        closeTimerCts?.Cancel();
        closeTimerCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 单个导航菜单项的项级上下文。
/// </summary>
public class NavigationMenuItemContext
{
    private readonly NavigationMenuContext parent;

    /// <summary>
    /// 标识本菜单项的唯一值。
    /// </summary>
    public string? Value { get; }

    /// <summary>
    /// 本项的下拉当前是否打开。
    /// </summary>
    public bool IsOpen => Value is not null && parent.ActiveItem == Value;

    /// <summary>
    /// 创建一个新的 <see cref="NavigationMenuItemContext"/>。
    /// </summary>
    public NavigationMenuItemContext(NavigationMenuContext parent, string? value)
    {
        this.parent = parent;
        Value = value;
    }

    /// <summary>把本项设为打开。</summary>
    public void Open() => parent.SetActiveItem(Value);

    /// <summary>若本项打开则将其关闭。</summary>
    public void Close()
    {
        if (IsOpen)
        {
            parent.SetActiveItem(null);
        }
    }

    /// <summary>切换打开/关闭。</summary>
    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    /// <summary>启动父级的共享关闭定时器。</summary>
    public void StartCloseTimer() => parent.StartCloseTimer();

    /// <summary>取消父级的关闭定时器。</summary>
    public void CancelCloseTimer() => parent.CancelCloseTimer();
}
