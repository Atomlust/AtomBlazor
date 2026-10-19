namespace AtomBlazor;

/// <summary>
/// 定义 <see cref="Stepper"/> 的布局。
/// </summary>
public enum StepperOrientation
{
    /// <summary>步骤沿顶部横排，内容在下方。</summary>
    Horizontal,

    /// <summary>步骤沿前导侧纵排，内容在旁边。</summary>
    Vertical
}

/// <summary>
/// 定义 <see cref="Stepper"/> 中单个步骤的视觉状态。
/// </summary>
/// <remarks>
/// 步骤依据自身相对活动步骤的位置推导出状态。设置
/// <see cref="Step.State"/> 可以覆盖它，这正是错误或跳过步骤的显示方式。
/// </remarks>
public enum StepState
{
    /// <summary>该步骤还在前方。</summary>
    Pending,

    /// <summary>该步骤是当前步骤。</summary>
    Active,

    /// <summary>该步骤位于活动步骤之前，或被标记为已完成。</summary>
    Completed,

    /// <summary>该步骤需要处理，否则整轮无法完成。</summary>
    Error,

    /// <summary>一个被跳过的可选步骤。</summary>
    Skipped
}
