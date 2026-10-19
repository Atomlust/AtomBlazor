using System.Collections;

namespace AtomBlazor;

/// <summary>
/// 为图表组件把系列名映射到其配置（标签与颜色）。
/// </summary>
/// <remarks>
/// <para>
/// ChartConfig 沿袭 shadcn/ui 将数据键映射到显示配置的
/// 模式。这一分离使数据可以用技术性键，
/// 同时展示对用户友好的标签。
/// </para>
/// <para>
/// 特性：
/// - 把系列键映射到标签与颜色
/// - 支持 CSS 自定义属性以便配主题
/// - 提供默认颜色（--chart-1 至 --chart-5）
/// - 实现 IEnumerable，支持集合初始化器语法
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var config = ChartConfig.Create(
///     ("desktop", new ChartSeriesConfig { Label = "Desktop", Color = "var(--chart-1)" }),
///     ("mobile", new ChartSeriesConfig { Label = "Mobile", Color = "var(--chart-2)" })
/// );
///
/// // Or using collection initializer:
/// var config = new ChartConfig
/// {
///     { "desktop", new ChartSeriesConfig { Label = "Desktop", Color = "var(--chart-1)" } },
///     { "mobile", new ChartSeriesConfig { Label = "Mobile", Color = "var(--chart-2)" } }
/// };
/// </code>
/// </example>
public class ChartConfig : IEnumerable<KeyValuePair<string, ChartSeriesConfig>>
{
    private readonly Dictionary<string, ChartSeriesConfig> _configs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 获取指定键的系列配置。
    /// </summary>
    /// <param name="key">系列键（例如 "desktop"、"mobile"）。</param>
    /// <returns>该系列的配置；未找到时为 null。</returns>
    public ChartSeriesConfig? this[string key]
    {
        get => _configs.TryGetValue(key, out var config) ? config : null;
        set
        {
            if (value != null)
            {
                _configs[key] = value;
            }
            else
            {
                _configs.Remove(key);
            }
        }
    }

    /// <summary>
    /// 向图表配置添加一个系列配置。
    /// </summary>
    /// <param name="key">与数据属性名相匹配的系列键。</param>
    /// <param name="config">该系列的配置。</param>
    public void Add(string key, ChartSeriesConfig config) =>
        _configs[key] = config;

    /// <summary>
    /// 获取系列键的标签；未配置时返回键本身。
    /// </summary>
    /// <param name="key">系列键。</param>
    /// <returns>配置好的标签，或以键作为兜底。</returns>
    public string GetLabel(string key)
    {
        return _configs.TryGetValue(key, out var config) && !string.IsNullOrEmpty(config.Label)
            ? config.Label
            : key;
    }

    /// <summary>
    /// 获取为系列键配置的颜色。
    /// </summary>
    /// <param name="key">系列键。</param>
    /// <param name="index">
    /// 键未配置时可退用的调色序号。负值（默认）
    /// 表示不兜底：调用方得到 <c>null</c>，可把选择权留给图表。
    /// </param>
    /// <returns>配置的颜色、按序号取到的调色色，或 <c>null</c>。</returns>
    /// <remarks>
    /// 兜底序号过去默认为 <c>0</c>，于是每个未配置的键都返回
    /// <c>--chart-1</c>。Config 只点名了部分系列的图表遂把其余系列
    /// 全画成同一颜色，而没能让自己调色轮转下去。
    /// </remarks>
    public string? GetColor(string key, int index = -1)
    {
        if (_configs.TryGetValue(key, out var config) && !string.IsNullOrEmpty(config.Color))
        {
            return config.Color;
        }

        return index >= 0 ? ChartColor.GetDefault(index) : null;
    }

    /// <summary>
    /// 获取全部已配置的系列键。
    /// </summary>
    public IEnumerable<string> Keys => _configs.Keys;

    /// <summary>
    /// 获取已配置系列的数量。
    /// </summary>
    public int Count => _configs.Count;

    /// <summary>
    /// 检查某系列键是否已配置。
    /// </summary>
    /// <param name="key">待检查的系列键。</param>
    /// <returns>已配置为 true，否则为 false。</returns>
    public bool ContainsKey(string key) => _configs.ContainsKey(key);

    /// <summary>
    /// 由一组键值对创建 ChartConfig。
    /// </summary>
    /// <param name="configs">(key, config) 对的元组集合。</param>
    /// <returns>新的 ChartConfig 实例。</returns>
    /// <example>
    /// <code>
    /// var config = ChartConfig.Create(
    ///     ("desktop", new ChartSeriesConfig { Label = "Desktop", Color = "var(--chart-1)" }),
    ///     ("mobile", new ChartSeriesConfig { Label = "Mobile", Color = "var(--chart-2)" })
    /// );
    /// </code>
    /// </example>
    public static ChartConfig Create(params (string Key, ChartSeriesConfig Config)[] configs)
    {
        var chartConfig = new ChartConfig();
        foreach (var (key, config) in configs)
        {
            chartConfig.Add(key, config);
        }
        return chartConfig;
    }

    /// <summary>
    /// 为指定标签创建带默认颜色的 ChartConfig。
    /// </summary>
    /// <param name="seriesLabels">各系列的标签（键将自动生成）。</param>
    /// <returns>使用默认图表颜色的新 ChartConfig。</returns>
    public static ChartConfig FromLabels(params string[] seriesLabels)
    {
        var chartConfig = new ChartConfig();
        for (var i = 0; i < seriesLabels.Length; i++)
        {
            var label = seriesLabels[i];
            var key = label.ToLowerInvariant().Replace(" ", "-");
            chartConfig.Add(key, new ChartSeriesConfig
            {
                Label = label,
                Color = ChartColor.GetDefault(i)
            });
        }
        return chartConfig;
    }

    public IEnumerator<KeyValuePair<string, ChartSeriesConfig>> GetEnumerator() =>
        _configs.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();
}
