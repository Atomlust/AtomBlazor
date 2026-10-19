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
    /// 若受控值与非受控值均为 null，则回退到默认值。
    /// </summary>
    public T Value
    {
        get
        {
            if (IsControlled)
            {
                return ControlledValue ?? _defaultValue;
            }
            return _uncontrolledValue ?? _defaultValue;
        }
    }

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
    /// <param name="isControlled">组件是否处于受控模式（通常是：controlledValue != null）。</param>
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
            // 未显式指定时，依据回调是否携带委托来推断
            IsControlled = isControlled ?? (onValueChanged.HasDelegate && controlledValue != null)
        };
    }
}
