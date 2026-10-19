namespace AtomBlazor;

/// <summary>
/// 定义向导步骤指示器的布局方向。
/// </summary>
public enum WizardLayout
{
    /// <summary>
    /// 步骤指示器横向排布在顶部，内容在下方。
    /// 适合宽屏表单与桌面布局。
    /// </summary>
    Horizontal,

    /// <summary>
    /// 步骤指示器纵向排布在左侧，内容在右侧。
    /// 适合窄屏表单、移动布局或步骤标题较长的场景。
    /// </summary>
    Vertical
}
