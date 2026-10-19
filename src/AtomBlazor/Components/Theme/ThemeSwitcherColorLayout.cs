namespace AtomBlazor;

/// <summary>
/// 控制 <c>ThemeSwitcher</c> 呈现颜色选择的方式。
/// 本枚举先行交付以保持类型面完整；所依赖的 Button、Popover、Separator、
/// NativeSelect 组件族已随各组件交付，<c>ThemeSwitcher</c> 本体已完成。
/// </summary>
public enum ThemeSwitcherColorLayout
{
    /// <summary>
    /// 基础色与主色分区展示，各带独立选中标识；选择其一永远不会重置另一个。
    /// </summary>
    Split,

    /// <summary>
    /// 基础色与主色合并为单一网格、共用一个选中标识。选择基础色会把主色重置为
    /// <see cref="PrimaryColor.Default"/>（旧版行为）。
    /// </summary>
    Combined
}
