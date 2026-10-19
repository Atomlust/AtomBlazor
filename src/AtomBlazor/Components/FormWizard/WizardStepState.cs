namespace AtomBlazor;

/// <summary>
/// 表示向导步骤在步骤指示器中的视觉状态。
/// </summary>
public enum WizardStepState
{
    /// <summary>
    /// 步骤尚未访问。
    /// </summary>
    Pending,

    /// <summary>
    /// 步骤当前显示中，处于活动状态。
    /// </summary>
    Active,

    /// <summary>
    /// 步骤已访问且通过校验。
    /// </summary>
    Completed,

    /// <summary>
    /// 步骤已访问但存在校验错误。
    /// </summary>
    Invalid,

    /// <summary>
    /// 可选步骤被跳过。
    /// </summary>
    Skipped
}
