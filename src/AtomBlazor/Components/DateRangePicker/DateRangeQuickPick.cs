namespace AtomBlazor;

/// <summary>
/// 表示日期范围选择器的快捷预设项。
/// 可以是内置预设（自带本地化标签），也可以是使用者自定义条目。
/// </summary>
public class DateRangeQuickPick
{
    /// <summary>
    /// 内置预设类型；若这是内置预设则有值，自定义预设为 null。
    /// </summary>
    internal DateRangePreset? Preset { get; }

    /// <summary>
    /// 自定义预设的显示标签；内置预设为 null（标签经本地化解析）。
    /// </summary>
    public string? Label { get; }

    /// <summary>
    /// 自定义预设产出日期范围的工厂函数；内置预设为 null。
    /// </summary>
    public Func<DateRange>? RangeFactory { get; }

    private DateRangeQuickPick(DateRangePreset preset)
    {
        Preset = preset;
    }

    private DateRangeQuickPick(string label, Func<DateRange> rangeFactory)
    {
        Label = label;
        RangeFactory = rangeFactory;
    }

    /// <summary>
    /// 从内置预设创建快捷项。标签经本地化自动解析。
    /// </summary>
    public static DateRangeQuickPick FromPreset(DateRangePreset preset) => new(preset);

    /// <summary>
    /// 创建完全自定义的快捷项，由使用者提供标签与范围工厂。
    /// </summary>
    public static DateRangeQuickPick Custom(string label, Func<DateRange> rangeFactory) => new(label, rangeFactory);

    /// <summary>
    /// 自 <see cref="DateRangePreset"/> 的隐式转换，便于顺手书写。
    /// </summary>
    public static implicit operator DateRangeQuickPick(DateRangePreset preset) => FromPreset(preset);
}
