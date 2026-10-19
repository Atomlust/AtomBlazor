namespace AtomBlazor;

/// <summary>
/// <see cref="ChipSet{TValue}"/> 级联给其芯片的非泛型桥。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Chip"/> 刻意保持非泛型，使芯片可以脱离集合、脱离类型参数独立使用。
/// 集合保留自己的 <c>TValue</c>，并通过本对象暴露芯片所需的三个操作：
/// 传入时装箱值，取出时再转回来。
/// </para>
/// <para>
/// 该对象在集合生命周期内身份稳定，因此以固定（IsFixed）方式级联。
/// 芯片在这里注册自身，选择变化时由集合通过 <see cref="NotifyStateChanged"/>
/// 显式重渲染它们。
/// </para>
/// </remarks>
internal sealed class ChipSetContext
{
    private readonly List<Chip> chips = [];

    /// <summary>芯片未自行声明变体时的回退变体。</summary>
    internal ChipVariant Variant { get; set; } = ChipVariant.Default;

    /// <summary>芯片未自行声明尺寸时的回退尺寸。</summary>
    internal ChipSize Size { get; set; } = ChipSize.Default;

    /// <summary>除非芯片主动退出，是否显示移除按钮。</summary>
    internal bool Dismissible { get; set; }

    /// <summary>除非芯片主动退出，选中芯片是否显示对勾。</summary>
    internal bool ShowCheckMark { get; set; }

    /// <summary>集合内所有芯片是否禁用。</summary>
    internal bool Disabled { get; set; }

    /// <summary>集合一次可选中多少芯片。</summary>
    internal ChipSelectionMode SelectionMode { get; set; } = ChipSelectionMode.Single;

    /// <summary>报告集合当前是否持有给定的芯片值。</summary>
    internal Func<object?, bool> IsSelected { get; set; } = static _ => false;

    /// <summary>把给定芯片值加入或移出集合的选择。</summary>
    internal Func<object?, Task> ToggleAsync { get; set; } = static _ => Task.CompletedTask;

    /// <summary>把给定芯片值从选择中移除，并触发集合的移除回调。</summary>
    internal Func<object?, Task> DismissAsync { get; set; } = static _ => Task.CompletedTask;

    /// <summary>跟踪一个芯片，使选择变化能重渲染它。</summary>
    internal void Register(Chip chip)
    {
        if (!chips.Contains(chip))
        {
            chips.Add(chip);
        }
    }

    /// <summary>停止跟踪已释放的芯片。</summary>
    internal void Unregister(Chip chip) => chips.Remove(chip);

    /// <summary>选择变化后重渲染所有已注册的芯片。</summary>
    internal void NotifyStateChanged()
    {
        foreach (var chip in chips)
        {
            chip.NotifySetStateChanged();
        }
    }
}
