using System.Globalization;

namespace AtomBlazor.Demo.Services;

/// <summary>
/// 文档站显示语言状态（zh/en，默认中文）。Scoped：每个请求（SSR 预渲染）/电路一个实例。
/// 语言切换采用「写 cookie → 整页重载」模型，SSR 首帧即目标语言，无闪烁；
/// 纯客户端宿主（Wasm/Auto Client）首屏后补读 cookie 对齐。
/// </summary>
public sealed class DemoCultureService
{
    /// <summary>简体中文语言码。</summary>
    public const string Chinese = "zh";

    /// <summary>英文语言码。</summary>
    public const string English = "en";

    /// <summary>持久化 cookie 名（宿主 SSR 初始化与浏览器侧读写共用）。</summary>
    public const string CookieName = "atom-lang";

    private string _language = Chinese;

    /// <summary>语言变更事件（供外壳组件即时刷新，重载之外的宿主路径兜底）。</summary>
    public event Action? LanguageChanged;

    /// <summary>当前语言码。</summary>
    public string Language => _language;

    /// <summary>是否中文显示。</summary>
    public bool IsChinese => _language != English;

    /// <summary>不可变快照，经 MainLayout 的 CascadingValue 下发；版本号递增会唤醒整棵渲染树。</summary>
    public DemoCultureState Snapshot { get; private set; } = new(Chinese, 0);

    /// <summary>由 cookie 值初始化（未识别值一律回落中文；与当前值相同则零动作零事件）。</summary>
    public void InitializeFromCookie(string? cookieValue) => Apply(Normalize(cookieValue));

    /// <summary>切换语言并触发事件与快照版本递增。</summary>
    public void SetLanguage(string language) => Apply(Normalize(language));

    /// <summary>按当前语言二选一文本（注入本服务的组件用；级联快照上的同名方法见 <see cref="DemoCultureState.T"/>）。</summary>
    public string T(string zh, string en) => Snapshot.T(zh, en);

    /// <summary>按当前语言二选一文本并做 <c>{0}</c> 式格式化。</summary>
    public string F(string zh, string en, params object?[] args) => Snapshot.F(zh, en, args);

    private void Apply(string language)
    {
        if (language == _language)
        {
            return;
        }

        _language = language;
        Snapshot = new DemoCultureState(language, Snapshot.Version + 1);
        LanguageChanged?.Invoke();
    }

    private static string Normalize(string? value) =>
        value is not null && value.StartsWith(English, StringComparison.OrdinalIgnoreCase) ? English : Chinese;
}

/// <summary>
/// 语言状态不可变快照：文档站双语文本一律经 <see cref="T"/> / <see cref="F"/> 就地二选一，
/// 页面模板内同时保留中英两份文案，无需外部资源表。
/// </summary>
/// <param name="Language">语言码（zh/en）。</param>
/// <param name="Version">版本号，切换即递增，作为 CascadingValue 时驱动子树重渲染。</param>
public sealed record DemoCultureState(string Language, int Version)
{
    /// <summary>缺省快照（中文，站点默认）：级联缺失时的回落值。</summary>
    public static DemoCultureState Default { get; } = new(DemoCultureService.Chinese, 0);

    /// <summary>是否中文显示。</summary>
    public bool IsChinese => Language != DemoCultureService.English;

    /// <summary>按当前语言二选一文本。</summary>
    /// <param name="zh">中文文案。</param>
    /// <param name="en">英文文案。</param>
    public string T(string zh, string en) => IsChinese ? zh : en;

    /// <summary>按当前语言二选一文本并做 <c>{0}</c> 式格式化。</summary>
    /// <param name="zh">中文模板。</param>
    /// <param name="en">英文模板。</param>
    /// <param name="args">格式化参数。</param>
    public string F(string zh, string en, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, T(zh, en), args);
}
