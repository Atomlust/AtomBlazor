namespace AtomBlazor;

/// <summary>
/// 定义字段图例的变体选项。
/// </summary>
public enum FieldLegendVariant
{
    /// <summary>
    /// 渲染语义化的 legend 元素（与 FieldSet 搭配使用）。
    /// </summary>
    Legend,

    /// <summary>
    /// 渲染带 role="group" 的 div（与 FieldGroup 搭配使用）。
    /// </summary>
    Label
}
