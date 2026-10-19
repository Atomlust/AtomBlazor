using AtomBlazor.Primitives.Utilities;

namespace AtomBlazor.Primitives.Contexts;

/// <summary>
/// 基元组件上下文的基类。
/// 为借助 Blazor 的 <c>CascadingValue</c> 机制在父子组件之间共享状态提供统一模式。
/// </summary>
/// <typeparam name="TState">本上下文管理的状态类型。</typeparam>
public abstract class PrimitiveContext<TState> where TState : class
{
    /// <summary>
    /// 获取或设置本上下文的状态。
    /// </summary>
    public TState State { get; set; }

    /// <summary>
    /// 自动生成的组件 ID。
    /// 用于在组件之间协调 ARIA 特性。
    /// </summary>
    public string Id { get; protected set; }

    /// <summary>
    /// 以状态实例初始化新的基元上下文。
    /// </summary>
    /// <param name="state">初始状态。</param>
    /// <param name="idPrefix">生成 ID 的前缀。</param>
    protected PrimitiveContext(TState state, string idPrefix = "primitive")
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Id = IdGenerator.GenerateId(idPrefix);
    }

    /// <summary>
    /// 为子组件生成带作用域的 ID。
    /// 适用于为 ARIA 特性创建唯一 ID。
    /// </summary>
    /// <param name="suffix">追加到组件 ID 末尾的后缀。</param>
    /// <returns>唯一的作用域 ID。</returns>
    public string GetScopedId(string suffix) =>
        $"{Id}-{suffix}";
}

/// <summary>
/// 带事件回调的基元组件上下文基类。
/// 在 PrimitiveContext 之上扩展出常见的事件处理模式。
/// </summary>
/// <typeparam name="TState">本上下文管理的状态类型。</typeparam>
public abstract class PrimitiveContextWithEvents<TState> : PrimitiveContext<TState>
    where TState : class
{
    /// <summary>
    /// 状态变化时触发的事件。
    /// 订阅方应在此事件触发时调用 StateHasChanged。
    /// </summary>
    public event Action? OnStateChanged;

    /// <summary>
    /// 以事件模式初始化新的基元上下文。
    /// </summary>
    /// <param name="state">初始状态。</param>
    /// <param name="idPrefix">生成 ID 的前缀。</param>
    protected PrimitiveContextWithEvents(TState state, string idPrefix = "primitive")
        : base(state, idPrefix)
    {
    }

    /// <summary>
    /// 通知订阅方状态已变化。
    /// 子组件在收到通知后应当重新渲染。
    /// </summary>
    protected void NotifyStateChanged() =>
        OnStateChanged?.Invoke();

    /// <summary>
    /// 更新状态并通知订阅方。
    /// </summary>
    /// <param name="updateAction">更新状态的动作。</param>
    protected void UpdateState(Action<TState> updateAction)
    {
        updateAction(State);
        NotifyStateChanged();
    }

    /// <summary>
    /// 更新状态，并可选择是否通知。
    /// </summary>
    /// <param name="updateAction">更新状态的动作。</param>
    /// <param name="notifyChanged">是否通知订阅方。默认为 true。</param>
    protected void UpdateState(Action<TState> updateAction, bool notifyChanged)
    {
        updateAction(State);
        if (notifyChanged)
        {
            NotifyStateChanged();
        }
    }
}
