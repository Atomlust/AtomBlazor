using AtomBlazor.Primitives.Contexts;

namespace AtomBlazor.Primitives;

/// <summary>
/// Accordion 基元上下文的状态。
/// </summary>
public class AccordionState
{
    /// <summary>
    /// 获取或设置当前展开的条目值（单开模式）或值集合（多开模式）。
    /// </summary>
    public HashSet<string> OpenValues { get; set; } = new();

    /// <summary>
    /// 获取或设置 Accordion 的类型（单开或多开）。
    /// </summary>
    public AccordionType Type { get; set; } = AccordionType.Single;

    /// <summary>
    /// 获取或设置单开模式下条目是否允许收起。
    /// </summary>
    public bool Collapsible { get; set; }
}

/// <summary>
/// Accordion 基元组件及其子组件的上下文。
/// 管理 Accordion 状态并为 ARIA 特性提供 ID。
/// </summary>
public class AccordionContext : PrimitiveContextWithEvents<AccordionState>
{
    /// <summary>
    /// 初始化 AccordionContext 的新实例。
    /// </summary>
    public AccordionContext() : base(new AccordionState(), "accordion")
    {
    }

    /// <summary>
    /// 获取指定 Accordion 条目标题的 ID。
    /// </summary>
    public string GetHeaderId(string value) => GetScopedId($"header-{value}");

    /// <summary>
    /// 获取指定 Accordion 条目触发器的 ID。
    /// </summary>
    public string GetTriggerId(string value) => GetScopedId($"trigger-{value}");

    /// <summary>
    /// 获取指定 Accordion 条目内容区域的 ID。
    /// </summary>
    public string GetContentId(string value) => GetScopedId($"content-{value}");

    /// <summary>
    /// 获取 Accordion 的类型。
    /// </summary>
    public AccordionType Type => State.Type;

    /// <summary>
    /// 获取单开模式下 Accordion 是否可收起。
    /// </summary>
    public bool Collapsible => State.Collapsible;

    /// <summary>
    /// 检查指定条目当前是否展开。
    /// </summary>
    /// <param name="value">要检查的条目值。</param>
    /// <returns>条目已展开时为 true，否则为 false。</returns>
    public bool IsItemOpen(string value) =>
        State.OpenValues.Contains(value);

    /// <summary>
    /// 切换 Accordion 条目的展开或收起状态。
    /// </summary>
    /// <param name="value">要切换的条目值。</param>
    public void ToggleItem(string value)
    {
        UpdateState(state =>
        {
            if (state.Type == AccordionType.Single)
            {
                // 单开模式：关闭其他条目并切换当前条目
                if (state.OpenValues.Contains(value))
                {
                    // 仅当 Collapsible 为 true 时才允许收起
                    if (state.Collapsible)
                    {
                        state.OpenValues.Clear();
                    }
                }
                else
                {
                    state.OpenValues.Clear();
                    state.OpenValues.Add(value);
                }
            }
            else
            {
                // 多开模式：各条目独立切换
                if (!state.OpenValues.Remove(value))
                {
                    state.OpenValues.Add(value);
                }
            }
        });
    }

    /// <summary>
    /// 设置展开的条目集合，用于受控状态。
    /// </summary>
    /// <param name="values">展开条目值的集合。</param>
    public void SetOpenItems(HashSet<string> values) =>
        UpdateState(state => state.OpenValues = new HashSet<string>(values));
}
