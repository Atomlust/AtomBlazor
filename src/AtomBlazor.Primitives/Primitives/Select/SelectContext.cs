using Microsoft.AspNetCore.Components;
using AtomBlazor.Primitives.Contexts;

namespace AtomBlazor.Primitives;

/// <summary>
/// 暴露 Select 上下文中仅与展示相关契约的非泛型接口。
/// 供需要在不知晓 TValue 的情况下读取选中展示文本的组件使用。
/// </summary>
public interface ISelectDisplayContext
{
    /// <summary>
    /// 获取当前选中值的展示文本。
    /// </summary>
    public string? DisplayText { get; }

    /// <summary>
    /// 状态变化时触发的事件。
    /// </summary>
    public event Action? OnStateChanged;
}

/// <summary>
/// Select 基元上下文的状态。
/// </summary>
/// <typeparam name="TValue">选中值的类型。</typeparam>
public class SelectState<TValue>
{
    /// <summary>
    /// 获取或设置下拉列表当前是否打开。
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// 获取或设置当前选中的值。
    /// </summary>
    public TValue? Value { get; set; }

    /// <summary>
    /// 获取或设置当前选中值的展示文本。
    /// </summary>
    public string? DisplayText { get; set; }

    /// <summary>
    /// 获取或设置触发下拉列表的元素。
    /// 用于定位。
    /// </summary>
    public ElementReference? TriggerElement { get; set; }

    /// <summary>
    /// 获取或设置当前聚焦项的索引。
    /// 用于键盘导航。
    /// </summary>
    public int FocusedIndex { get; set; } = -1;

    /// <summary>
    /// 获取或设置 Select 是否禁用。
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置 Select 是否为必填。
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// 获取或设置下拉列表关闭时是否应把焦点归还给触发元素。
    /// 由有意的关闭路径（选中项、Escape）置为 <c>true</c>，
    /// 而外部关闭（点击外部、Tab）保持 <c>false</c>——那种情况下焦点已经停在
    /// 用户移过去的地方。
    /// </summary>
    public bool RestoreFocusOnClose { get; set; }
}

/// <summary>
/// Select 基元组件及其子级使用的上下文。
/// 管理选择状态、为 ARIA 特性提供 ID，并处理键盘导航。
/// </summary>
/// <typeparam name="TValue">选中值的类型。</typeparam>
public class SelectContext<TValue> : PrimitiveContextWithEvents<SelectState<TValue>>, ISelectDisplayContext
{
    /// <summary>
    /// 获取或设置应用到 Select 所有项的默认 CSS 类。
    /// 由上级 Select 的 ItemClass 参数级联而来。
    /// </summary>
    public string? ItemClass { get; set; }

    /// <summary>
    /// 选中值变化时调用的回调。
    /// </summary>
    public Action<TValue?>? OnValueChange { get; set; }

    /// <summary>
    /// 初始化 SelectContext 的新实例。
    /// </summary>
    public SelectContext() : base(new SelectState<TValue>(), "select")
    {
        TriggerId = GetScopedId("trigger");
    }

    /// <summary>
    /// 获取 Select 触发按钮的 ID。
    /// 当触发元素使用自定义 id 时（例如表单字段中为关联标签），
    /// 可通过 <see cref="SetTriggerId"/> 覆盖。
    /// </summary>
    public string TriggerId { get; private set; }

    /// <summary>
    /// 获取 Select 内容容器的 ID。
    /// </summary>
    public string ContentId => GetScopedId("content");

    /// <summary>
    /// 获取 Select 值展示元素的 ID。
    /// </summary>
    public string ValueId => GetScopedId("value");

    /// <summary>
    /// 获取 Select 当前是否打开。
    /// </summary>
    public bool IsOpen => State.IsOpen;

    /// <summary>
    /// 获取当前聚焦项的索引。
    /// </summary>
    public int FocusedIndex => State.FocusedIndex;

    /// <summary>
    /// 获取当前选中的值。
    /// </summary>
    public TValue? Value => State.Value;

    /// <summary>
    /// 获取当前选中值的展示文本。
    /// </summary>
    public string? DisplayText => State.DisplayText;

    /// <summary>
    /// 获取 Select 是否禁用。
    /// </summary>
    public bool Disabled => State.Disabled;

    /// <summary>
    /// 获取 Select 是否为必填。
    /// </summary>
    public bool Required => State.Required;

    /// <summary>
    /// 设置必填状态。
    /// </summary>
    /// <param name="required">Select 是否为必填。</param>
    public void SetRequired(bool required)
    {
        if (State.Required != required)
        {
            UpdateState(state => state.Required = required);
        }
    }

    /// <summary>
    /// 打开下拉列表。
    /// </summary>
    /// <param name="triggerElement">可选：触发下拉列表的元素。</param>
    public void Open(ElementReference? triggerElement = null)
    {
        if (State.Disabled)
        {
            return;
        }

        // 打开时清空已注册项——内容挂载时它们会重新注册。
        // ForceMount=false 时项在关闭时被销毁、打开时重建，这一步是必需的。
        if (!State.IsOpen)
        {
            Items.Clear();
        }

        UpdateState(state =>
        {
            state.IsOpen = true;
            state.TriggerElement = triggerElement;
            state.FocusedIndex = -1; // 打开时重置焦点
            state.RestoreFocusOnClose = false; // 清空，交给随后的 Close() 重新判断
        });
    }

    /// <summary>
    /// 关闭下拉列表。
    /// </summary>
    /// <param name="restoreFocus">
    /// 为 <c>true</c> 时表示内容拆退后应把焦点归还给触发元素。
    /// 有意的关闭路径（Escape、键盘确认）传 <c>true</c>；
    /// 外部关闭（点击外部、Tab）保持 <c>false</c>——那种情况下焦点已停在用户想去的地方。
    /// </param>
    public void Close(bool restoreFocus = false)
    {
        UpdateState(state =>
        {
            state.IsOpen = false;
            state.FocusedIndex = -1;
            state.RestoreFocusOnClose = restoreFocus;
        });
    }

    /// <summary>
    /// 切换下拉列表的开/关状态。
    /// </summary>
    /// <param name="triggerElement">可选：触发切换的元素。</param>
    public void Toggle(ElementReference? triggerElement = null)
    {
        if (State.Disabled)
        {
            return;
        }

        if (State.IsOpen)
        {
            Close();
        }
        else
        {
            Open(triggerElement);
        }
    }

    /// <summary>
    /// 选中一个值并更新展示文本。
    /// </summary>
    /// <param name="value">要选中的值。</param>
    /// <param name="displayText">该值的展示文本。</param>
    public void SelectValue(TValue? value, string? displayText)
    {
        UpdateState(state =>
        {
            state.Value = value;
            state.DisplayText = displayText;
            state.IsOpen = false; // 选中后关闭
            state.FocusedIndex = -1;
            // 选中属于有意的关闭——把焦点归还给触发元素，
            // 键盘导航（Tab）才能从正确的位置继续。
            state.RestoreFocusOnClose = true;
        });

        // 调用值变化回调
        OnValueChange?.Invoke(value);
    }

    /// <summary>
    /// 更新当前选中值的展示文本。
    /// 仅当文本确实变化时才触发状态变化通知。
    /// </summary>
    /// <param name="displayText">新的展示文本。</param>
    public void SetDisplayText(string? displayText)
    {
        if (State.DisplayText != displayText)
        {
            UpdateState(state => state.DisplayText = displayText);
        }
    }

    /// <summary>
    /// 设置禁用状态。
    /// </summary>
    /// <param name="disabled">Select 是否禁用。</param>
    public void SetDisabled(bool disabled)
    {
        UpdateState(state =>
        {
            state.Disabled = disabled;
            if (disabled && state.IsOpen)
            {
                state.IsOpen = false;
            }
        });
    }

    /// <summary>
    /// 用自定义值覆盖自动生成的触发元素 ID。
    /// 当触发元素带有自定义 <c>id</c> 属性时（例如表单字段中为关联标签）使用，
    /// 确保点击外部检测与 ARIA 引用都指向正确的 ID。
    /// </summary>
    /// <param name="id">自定义的触发元素 ID。</param>
    public void SetTriggerId(string id) =>
        TriggerId = id;

    /// <summary>
    /// 为键盘导航设置聚焦项索引。
    /// </summary>
    /// <param name="index">要聚焦的项的索引。</param>
    public void SetFocusedIndex(int index) => UpdateState(state => state.FocusedIndex = index);

    /// <summary>
    /// 获取用于键盘导航的已注册项列表。
    /// </summary>
    private List<SelectItemMetadata<TValue>> Items { get; } = new List<SelectItemMetadata<TValue>>();

    /// <summary>
    /// 把一个项注册进 Select 上下文，供键盘导航使用。
    /// </summary>
    /// <param name="value">该项的值。</param>
    /// <param name="disabled">该项是否禁用。</param>
    /// <param name="displayText">该项的展示文本。</param>
    /// <returns>该注册项的稳定标识。</returns>
    public string RegisterItem(TValue? value, bool disabled, string? displayText = null)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var metadata = new SelectItemMetadata<TValue>
        {
            Id = id,
            Value = value,
            Disabled = disabled,
            DisplayText = displayText
        };
        Items.Add(metadata);

        // 若该项的值与当前选中值一致，
        // 就把 DisplayText 更新为正确的显示名
        if (displayText != null && EqualityComparer<TValue>.Default.Equals(State.Value, value))
        {
            UpdateState(state => state.DisplayText = displayText);
        }

        return id;
    }

    /// <summary>
    /// 按稳定标识把项从 Select 上下文中注销。
    /// 在释放期间调用也安全——不会破坏其他项的索引。
    /// </summary>
    /// <param name="id"><see cref="RegisterItem"/> 返回的稳定标识。</param>
    public void UnregisterItem(string id)
    {
        var index = Items.FindIndex(i => i.Id == id);
        if (index >= 0)
        {
            Items.RemoveAt(index);

            // 必要时调整聚焦索引
            if (State.FocusedIndex >= Items.Count)
            {
                SetFocusedIndex(Items.Count - 1);
            }
        }
    }

    /// <summary>
    /// 按稳定标识获取项当前的索引。
    /// 找不到时返回 -1。
    /// </summary>
    /// <param name="id"><see cref="RegisterItem"/> 返回的稳定标识。</param>
    /// <returns>项列表中的当前索引；找不到时为 -1。</returns>
    public int GetItemIndex(string id) =>
        Items.FindIndex(i => i.Id == id);

    /// <summary>
    /// 获取当前聚焦项的稳定标识。
    /// 没有项聚焦时返回 null。
    /// </summary>
    public string? GetFocusedItemId()
    {
        if (State.FocusedIndex >= 0 && State.FocusedIndex < Items.Count)
        {
            return Items[State.FocusedIndex].Id;
        }
        return null;
    }

    /// <summary>
    /// 把焦点移动到下一个未禁用的项。
    /// </summary>
    /// <param name="direction">1 表示下一项，-1 表示上一项。</param>
    public void MoveFocus(int direction)
    {
        if (Items.Count == 0)
        {
            return;
        }

        var startIndex = State.FocusedIndex;
        var newIndex = startIndex;

        // 找到下一个未禁用的项
        do
        {
            newIndex += direction;

            // 回卷
            if (newIndex < 0)
            {
                newIndex = Items.Count - 1;
            }
            else if (newIndex >= Items.Count)
            {
                newIndex = 0;
            }


            // 所有项都禁用时避免死循环
            if (newIndex == startIndex)
            {
                break;
            }


        } while (newIndex >= 0 && newIndex < Items.Count && Items[newIndex].Disabled);

        SetFocusedIndex(newIndex);
    }

    /// <summary>
    /// 把焦点移动到第一个未禁用的项。
    /// </summary>
    public void FocusFirst()
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (!Items[i].Disabled)
            {
                SetFocusedIndex(i);
                return;
            }
        }
    }

    /// <summary>
    /// 把焦点移动到当前选中项；没有选中项时移到第一个未禁用的项。
    /// </summary>
    public void FocusSelectedOrFirst()
    {
        // 尝试找到并聚焦选中项
        if (State.Value != null)
        {
            for (var i = 0; i < Items.Count; i++)
            {
                if (!Items[i].Disabled && EqualityComparer<TValue>.Default.Equals(Items[i].Value, State.Value))
                {
                    SetFocusedIndex(i);
                    return;
                }
            }
        }

        // 兜底：聚焦第一个项
        FocusFirst();
    }

    /// <summary>
    /// 把焦点移动到最后一个未禁用的项。
    /// </summary>
    public void FocusLast()
    {
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (!Items[i].Disabled)
            {
                SetFocusedIndex(i);
                return;
            }
        }
    }

    /// <summary>
    /// 选中当前聚焦的项。
    /// </summary>
    public void SelectFocusedItem()
    {
        if (State.FocusedIndex >= 0 && State.FocusedIndex < Items.Count)
        {
            var item = Items[State.FocusedIndex];
            if (!item.Disabled)
            {
                SelectValue(item.Value, item.DisplayText);
            }
        }
    }

    /// <summary>
    /// 通过查找已注册项获取指定值的展示文本。
    /// </summary>
    /// <param name="value">要查找的值。</param>
    /// <returns>找到的展示文本；否则为 null。</returns>
    public string? GetDisplayTextForValue(TValue? value)
    {
        if (value == null)
        {
            return null;
        }


        var item = Items.FirstOrDefault(i => EqualityComparer<TValue>.Default.Equals(i.Value, value));
        return item?.DisplayText;
    }

}

/// <summary>
/// 已注册 Select 项的元数据。
/// </summary>
/// <typeparam name="TValue">项值的类型。</typeparam>
public class SelectItemMetadata<TValue>
{
    /// <summary>
    /// 获取或设置本次项注册的稳定标识。
    /// 用于安全注销而不破坏索引。
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// 获取或设置 Select 项的值。
    /// </summary>
    public TValue? Value { get; set; }

    /// <summary>
    /// 获取或设置该项是否禁用。
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置该项的展示文本。
    /// </summary>
    public string? DisplayText { get; set; }
}
