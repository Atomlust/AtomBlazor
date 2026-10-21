using Microsoft.AspNetCore.Components;

namespace AtomBlazor.Primitives.Utilities;

/// <summary>
/// 管理受控与非受控组件状态的工具类。
/// 同时支持两种模式：由父组件控制状态，或组件自行管理内部状态。
/// </summary>
/// <typeparam name="T">状态值的类型。</typeparam>
public class UseControllableState<T>
{
    private T? _uncontrolledValue;
    private readonly T _defaultValue;

    /// <summary>
    /// 创建新的受控状态管理器。
    /// </summary>
    /// <param name="defaultValue">非受控模式下的默认值。</param>
    public UseControllableState(T defaultValue)
    {
        _defaultValue = defaultValue;
        _uncontrolledValue = defaultValue;
    }

    /// <summary>
    /// 父组件提供的受控值。
    /// 设置本属性即可从父级控制取值。
    /// </summary>
    public T? ControlledValue { get; set; }

    /// <summary>
    /// 显式声明本状态是否由父组件控制。
    /// 组件必须依据是否提供了 ControlledValue 参数来设置它。
    /// </summary>
    public bool IsControlled { get; set; }

    /// <summary>
    /// 用于通知父组件值变化的事件回调。
    /// </summary>
    public EventCallback<T> OnValueChanged { get; set; }

    /// <summary>
    /// 获取当前值（受控或非受控）。
    /// </summary>
    /// <remarks>
    /// 默认值只用于初始化非受控状态，绝不是 null 的兜底：受控模式下
    /// <see langword="null"/> 是父级明确要求的值（例如清空选择），必须原样返回；
    /// 非受控模式下 <see cref="SetValueAsync"/> 写入的 null 同样生效。
    /// </remarks>
    public T Value => IsControlled ? ControlledValue! : _uncontrolledValue!;

    /// <summary>
    /// 设置取值，更新受控或非受控状态。
    /// </summary>
    /// <param name="value">新值。</param>
    /// <returns>值变化处理完成后完成的任务。</returns>
    public async Task SetValueAsync(T value)
    {
        if (!IsControlled)
        {
            _uncontrolledValue = value;
        }

        // 无论受控与否，都始终通知父组件变化
        if (OnValueChanged.HasDelegate)
        {
            await OnValueChanged.InvokeAsync(value);
        }
    }

    /// <summary>
    /// 将非受控值重置为默认值。
    /// 在受控模式下无效果。
    /// </summary>
    public void Reset()
    {
        if (!IsControlled)
        {
            _uncontrolledValue = _defaultValue;
        }
    }
}

/// <summary>
/// 受控状态相关的扩展方法。
/// </summary>
public static class UseControllableStateExtensions
{
    /// <summary>
    /// 从组件参数创建受控状态管理器。
    /// </summary>
    /// <typeparam name="T">状态值的类型。</typeparam>
    /// <param name="controlledValue">受控值参数。</param>
    /// <param name="onValueChanged">值变化回调。</param>
    /// <param name="defaultValue">非受控模式下的默认值。</param>
    /// <param name="isControlled">
    /// 组件是否处于受控模式。未显式指定时仅依据是否绑定了变化回调推断；
    /// 绝不能依据 <paramref name="controlledValue"/> 是否为 null 推断——
    /// null 是合法的受控值（例如清空选择）。
    /// </param>
    /// <returns>配置完成的受控状态管理器。</returns>
    public static UseControllableState<T> Create<T>(
        T? controlledValue,
        EventCallback<T> onValueChanged,
        T defaultValue,
        bool? isControlled = null)
    {
        return new UseControllableState<T>(defaultValue)
        {
            ControlledValue = controlledValue,
            OnValueChanged = onValueChanged,
            IsControlled = isControlled ?? onValueChanged.HasDelegate
        };
    }
}
