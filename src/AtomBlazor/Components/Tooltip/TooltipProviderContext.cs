namespace AtomBlazor;

/// <summary>
/// 为一个 TooltipProvider 内的所有提示提供共享配置上下文。
/// </summary>
/// <remarks>
/// <para>
/// TooltipProviderContext 用于在应用的一段区域内全局配置提示行为，
/// 由 <see cref="TooltipProvider"/> 组件提供，并级联给所有子 <see cref="Tooltip"/> 组件。
/// </para>
/// <para>
/// 该上下文带来：
/// <list type="bullet">
/// <item>多个提示之间一致的延时</item>
/// <item>快速切换提示时的跳过延时行为</item>
/// <item>集中的提示配置管理</item>
/// </list>
/// </para>
/// <para>
/// 提供者模式尤其适用于：
/// <list type="bullet">
/// <item>提示众多且行为应当一致的应用</item>
/// <item>实现"提示发现模式"——首次交互之后的延时被跳过</item>
/// <item>为不同分区定制提示时序（例如工具栏更快，正文更慢）</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// 典型用法（TooltipProvider 组件内部）：
/// <code>
/// &lt;CascadingValue Value="@providerContext"&gt;
///     @ChildContent
/// &lt;/CascadingValue&gt;
/// </code>
/// </example>
public class TooltipProviderContext
{
    /// <summary>
    /// 获取或设置显示提示前等待的毫秒数。
    /// </summary>
    /// <value>
    /// 延时毫秒数，默认 700。
    /// </value>
    /// <remarks>
    /// <para>
    /// 用户悬停或聚焦提示触发器时应用此延时。提示不会立即出现，
    /// 而是等待这段时间，以避免鼠标快速划过时意外弹出提示。
    /// </para>
    /// <para>
    /// 按场景推荐的取值：
    /// <list type="bullet">
    /// <item><strong>0-200ms：</strong>极快，用于高频访问的 UI（工具栏、图标按钮）</item>
    /// <item><strong>300-500ms：</strong>快，用于预期有提示的交互元素</item>
    /// <item><strong>600-800ms：</strong>标准，在可发现性与干扰之间取得平衡（默认）</item>
    /// <item><strong>900-1200ms：</strong>慢，用于不应分散注意力的详细信息</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>无障碍考量：</strong>较长的延时可减轻运动障碍用户误悬停带来的认知负担；
    /// 但延时过长又会挫伤寻求信息的用户。
    /// </para>
    /// </remarks>
    public int DelayDuration { get; set; } = 700;

    /// <summary>
    /// 获取或设置在多长时间窗口内后续提示可跳过延时（毫秒）。
    /// </summary>
    /// <value>
    /// 跳过延时的时间窗口毫秒数，默认 300。
    /// </value>
    /// <remarks>
    /// <para>
    /// 这就是"提示发现模式"——用户看过一个提示之后，在该时间窗口内的后续提示
    /// 会立即出现而不再等待。这让探索多个带提示元素时的体验更流畅。
    /// </para>
    /// <para>
    /// <strong>工作方式：</strong>
    /// <list type="number">
    /// <item>用户悬停元素 A，等待 DelayDuration（700ms），提示出现</item>
    /// <item>用户在 SkipDelayDuration（300ms）之内移到元素 B</item>
    /// <item>元素 B 的提示立即出现，不再等待</item>
    /// <item>若用户等待超过 SkipDelayDuration 才移到元素 C，则延时重新计起</item>
    /// </list>
    /// </para>
    /// <para>
    /// 推荐取值：
    /// <list type="bullet">
    /// <item><strong>0ms：</strong>关闭跳过行为，始终等待完整延时</item>
    /// <item><strong>200-400ms：</strong>短窗口，用于紧邻成组的元素</item>
    /// <item><strong>500-1000ms：</strong>标准窗口，允许从容探索</item>
    /// <item><strong>1500-3000ms：</strong>长窗口，对慢速交互非常宽容</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>性能说明：</strong>本特性需要记录上一次提示关闭的时间；开销极小，
    /// 但可通过设为 0 关闭。
    /// </para>
    /// </remarks>
    public int SkipDelayDuration { get; set; } = 300;

    /// <summary>
    /// 以默认值初始化 <see cref="TooltipProviderContext"/> 类的新实例。
    /// </summary>
    /// <remarks>
    /// 创建的新上下文为：
    /// <list type="bullet">
    /// <item><see cref="DelayDuration"/> = 700ms（标准延时）</item>
    /// <item><see cref="SkipDelayDuration"/> = 300ms（短跳过窗口）</item>
    /// </list>
    /// 这些默认值提供了适合大多数应用的均衡体验。
    /// </remarks>
    public TooltipProviderContext()
    {
        DelayDuration = 700;
        SkipDelayDuration = 300;
    }

    /// <summary>
    /// 以自定义延时值初始化 <see cref="TooltipProviderContext"/> 类的新实例。
    /// </summary>
    /// <param name="delayDuration">显示提示前等待的毫秒数。</param>
    /// <param name="skipDelayDuration">在该时间窗口内后续提示可跳过延时（毫秒）。</param>
    /// <remarks>
    /// 用此构造器可创建带有贴合应用特定需求的时序值的提供者上下文。
    /// </remarks>
    /// <example>
    /// 为工具栏创建快速响应的提示提供者：
    /// <code>
    /// var context = new TooltipProviderContext(200, 500);
    /// </code>
    /// </example>
    public TooltipProviderContext(int delayDuration, int skipDelayDuration)
    {
        DelayDuration = delayDuration;
        SkipDelayDuration = skipDelayDuration;
    }
}
