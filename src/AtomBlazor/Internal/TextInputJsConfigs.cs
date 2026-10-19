namespace AtomBlazor;

/// <summary>
/// 输入类组件通过 JS interop 发给浏览器的配置载荷
/// （<c>textInput.initialize/updateConfig</c> 与 <c>numericInput.*</c> 模块）。
/// </summary>
/// <remarks>
/// 为什么用命名 record 而不是匿名类型：
/// <list type="number">
///   <item>消费方开启 PublishTrimmed（Blazor WASM 体积优化）时，STJ 反射序列化
///         需要载荷类型的构造函数与属性 getter 存活。匿名类型是编译器生成的
///         内部类型，名称随编译重编号（<c>&lt;&gt;f__AnonymousTypeN`M</c>），
///         无法在 ILLink 根描述符中稳定引用；命名类型可在 TrimRoots.xml 中
///         精确定位，从而不必整体保留组件程序集——那会把库依赖的 Markdig /
///         HtmlSanitizer / Ical.Net / AngleSharp 重新拖回可达图
///         （实测 raw 8.5MB → 15.5MB）。</item>
///   <item>record 保持与匿名类型一致的结构相等语义：各输入组件的
///         lastJsConfig 变化检测依赖 Equals 按值比较
///         （见 Input.OnAfterRenderAsync 的 updateConfig 分支）。</item>
/// </list>
/// 属性名刻意保持与 JS 侧约定一致的小写形式：JSON 键名不随序列化命名策略漂移，
/// 与改造前匿名类型的输出逐字节一致。
/// </remarks>
internal sealed record TextInputJsConfig
{
    /// <summary>输入更新时机：immediate / onchange / debounced。</summary>
    public string mode { get; init; } = "";

    /// <summary>debounced 模式的防抖间隔（毫秒）。</summary>
    public int debounceMs { get; init; }
}

/// <summary>InputField 的 textInput 配置载荷（比 <see cref="TextInputJsConfig"/> 多 notifyOnBlur）。</summary>
internal sealed record InputFieldJsConfig
{
    public string mode { get; init; } = "";
    public int debounceMs { get; init; }

    /// <summary>失焦时是否通知 EditContext 校验。</summary>
    public bool notifyOnBlur { get; init; }
}

/// <summary>
/// 数字输入类组件（NumericInput / CurrencyInput）的 numericInput 配置载荷。
/// 两者形状完全一致（同一组 JS 模块函数），共用同一类型。
/// </summary>
internal sealed record NumericInputJsConfig
{
    /// <summary>是否禁用防抖（立即上报）。</summary>
    public bool disableDebounce { get; init; }

    /// <summary>防抖间隔（毫秒）。</summary>
    public int debounceMs { get; init; }

    /// <summary>触发步进的按键列表。</summary>
    public string[] stepKeys { get; init; } = [];

    /// <summary>是否允许小数。</summary>
    public bool allowDecimal { get; init; }

    /// <summary>是否允许负数。</summary>
    public bool allowNegative { get; init; }

    /// <summary>小数点分隔符（按当前区域或固定不变区域）。</summary>
    public string decimalSeparator { get; init; } = ".";

    /// <summary>是否启用滚轮步进。</summary>
    public bool enableWheelStep { get; init; }
}

/// <summary>Textarea 的 textInput 配置载荷（带字符计数与最大长度）。</summary>
internal sealed record TextareaJsConfig
{
    public string mode { get; init; } = "";
    public int debounceMs { get; init; }

    /// <summary>是否显示字符计数。</summary>
    public bool hasCharacterCount { get; init; }

    /// <summary>字符计数元素的选择器（未启用时为 null）。</summary>
    public string? characterCountSelector { get; init; }

    /// <summary>最大长度（未设置时为 null）。</summary>
    public int? maxLength { get; init; }
}
