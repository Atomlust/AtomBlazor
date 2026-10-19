namespace AtomBlazor;

/// <summary>
/// 定义是什么把 <c>TagInput</c> 里键入的文本提交为一个标签。
/// 多个触发方式可用按位 OR 组合。
/// </summary>
[Flags]
public enum TagInputTrigger
{
    /// <summary>按下 Enter 键把当前输入提交为标签。</summary>
    Enter = 1,

    /// <summary>按下逗号键把当前输入提交为标签。</summary>
    Comma = 2,

    /// <summary>按下空格键把当前输入提交为标签。</summary>
    Space = 4,

    /// <summary>按下 Tab 键把当前输入提交为标签。</summary>
    Tab = 8,

    /// <summary>按下分号键把当前输入提交为标签。</summary>
    Semicolon = 16,

    /// <summary>
    /// 离开输入框时把已键入的文本提交为标签，避免用户点击别处或 Tab 走人时
    /// 丢掉写了一半的条目。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 唯一一个不是按键的触发方式。它提交的是用户键入的文本，而不是高亮中的
    /// 建议项——点击建议项会加入该建议并取消失焦提交，焦点回到输入框也会取消，
    /// 因此两条路径都不会重复加标签。
    /// </para>
    /// <para>
    /// 未通过 <c>Validate</c>、<c>MaxTags</c>、<c>MaxTagLength</c> 或去重检查的文本
    /// 会留在输入框里并经 <c>OnTagRejected</c> 上报，与按键提交时的行为完全一致。
    /// 不会有内容被静默丢弃。
    /// </para>
    /// </remarks>
    Blur = 32
}
