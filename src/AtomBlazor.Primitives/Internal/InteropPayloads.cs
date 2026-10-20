namespace AtomBlazor.Primitives;

/// <summary>
/// 基元组件通过 JS interop 发给浏览器的配置载荷（overlay.*、slider.initialize、
/// menuKeyboard.initialize、scrollbar 等模块）。
/// </summary>
/// <remarks>
/// 为什么用命名 record 而不是匿名类型：
/// <list type="number">
///   <item>消费方开启 PublishTrimmed（Blazor WASM 体积优化）时，STJ 反射序列化
///         需要载荷类型的构造函数与属性 getter 存活。匿名类型只能走构造函数绑定
///         序列化，而裁剪管线会剥掉构造函数参数名，运行时抛出
///         <c>ConstructorContainsNullParameterNames</c>——浮层从此打不开。
///         命名 record 用默认构造 + init 属性，STJ 走属性序列化路径，
///         与构造函数参数名彻底无关。</item>
///   <item>匿名类型是编译器生成的内部类型，名称随编译重编号
///         （<c>&lt;&gt;f__AnonymousTypeN`M</c>），无法在 ILLink 根描述符中稳定
///         引用；命名类型可以精确定位。</item>
/// </list>
/// 属性名刻意保持与 JS 侧约定一致的小写形式：JSON 键名不随序列化命名策略漂移，
/// 与改造前匿名类型的输出逐字节一致。
/// </remarks>
internal sealed record FloatingOpenJsConfig
{
    /// <summary>首选方位，如 "bottom"、"top-start"。</summary>
    public string placement { get; init; } = "";

    /// <summary>与锚元素的偏移距离（像素）。</summary>
    public int offset { get; init; }

    /// <summary>是否让浮动内容宽度与锚元素一致。</summary>
    public bool matchReferenceWidth { get; init; }

    /// <summary>CSS 定位策略："absolute" 或 "fixed"。</summary>
    public string strategy { get; init; } = "absolute";

    /// <summary>浮动内容的 CSS z-index。</summary>
    public int zIndex { get; init; }

    /// <summary>空间不足时翻转到对面方位。</summary>
    public bool flip { get; init; }

    /// <summary>平移以保持内容留在视口内。</summary>
    public bool shift { get; init; }

    /// <summary>应由 JS 写入解析后 data-side 属性的元素 id（未启用时为 null）。</summary>
    public string? sideElementId { get; init; }

    /// <summary>方位变化时是否回调进 .NET 汇报。</summary>
    public bool reportPlacement { get; init; }

    /// <summary>要接线的关闭手势（未启用时为 null）。</summary>
    public FloatingDismissJsConfig? dismiss { get; init; }

    /// <summary>打开时把当前选中项滚动进视野（未启用时为 null）。</summary>
    public FloatingScrollToCurrentJsConfig? scrollToCurrent { get; init; }

    /// <summary>浮层揭示后一帧之内要聚焦的元素 id（未启用时为 null）。</summary>
    public string? autoFocusId { get; init; }

    /// <summary>要接线的键盘行为（未启用时为 null）。</summary>
    public FloatingKeyboardJsConfig? keyboard { get; init; }
}

/// <summary>overlay.open 载荷的 dismiss 段。</summary>
internal sealed record FloatingDismissJsConfig
{
    /// <summary>被视为"内部"的元素 id，其内的交互不算关闭手势。</summary>
    public string contentId { get; init; } = "";

    /// <summary>触发元素 id，同样算"内部"（未设置时为 null）。</summary>
    public string? triggerId { get; init; }

    /// <summary>是否监听内容与触发元素之外的交互。</summary>
    public bool onOutsideInteraction { get; init; }

    /// <summary>是否监听 document 任意位置的 Escape。</summary>
    public bool onEscapeKey { get; init; }
}

/// <summary>overlay.open 载荷的 scrollToCurrent 段。</summary>
internal sealed record FloatingScrollToCurrentJsConfig
{
    /// <summary>在其中查找"当前选中项"的元素 id。</summary>
    public string containerId { get; init; } = "";

    /// <summary>查找选中项的 CSS 选择器。</summary>
    public string selector { get; init; } = "";
}

/// <summary>overlay.open 载荷的 keyboard 段。</summary>
internal sealed record FloatingKeyboardJsConfig
{
    /// <summary>键盘行为种类："Listbox" 或 "Menu"。</summary>
    public string kind { get; init; } = "";

    /// <summary>按键归属元素的 id。</summary>
    public string contentId { get; init; } = "";

    /// <summary>供处理器回调的 DotNetObjectReference（按运行时类型序列化）。</summary>
    public object? callbackRef { get; init; }

    /// <summary>当前选中值，用于打开前滚动与高亮（未设置时为 null）。</summary>
    public string? selectedValue { get; init; }

    /// <summary>方向键是否从末项回卷到首项。</summary>
    public bool loop { get; init; }

    /// <summary>"vertical" 用于下拉菜单；"menubar" 额外响应左右方向键。</summary>
    public string mode { get; init; } = "vertical";

    /// <summary>揭示后的焦点落点（null 表示保持现有焦点）。</summary>
    public string? initialFocus { get; init; }
}

/// <summary>overlay.close 载荷。</summary>
internal sealed record FloatingCloseJsConfig
{
    /// <summary>关闭后是否回调 JsOnClosed（非 ForceMount 的内容要等退场动画结束再卸载）。</summary>
    public bool notifyClosed { get; init; }

    /// <summary>要释放的键盘接线（未启用时为 null）。</summary>
    public FloatingKeyboardReleaseJsConfig? keyboard { get; init; }

    /// <summary>要把焦点归还到的元素 id（未启用时为 null）。</summary>
    public string? restoreFocusToId { get; init; }

    /// <summary>组合式触发元素兜底：要把焦点归还到的真实元素（未启用时为 null）。</summary>
    public Microsoft.AspNetCore.Components.ElementReference? restoreFocusToElement { get; init; }
}

/// <summary>overlay.close 载荷的 keyboard 段：释放某个内容元素上的键盘接线。</summary>
internal sealed record FloatingKeyboardReleaseJsConfig
{
    /// <summary>键盘行为种类，与接线时一致。</summary>
    public string kind { get; init; } = "";

    /// <summary>按键归属元素的 id。</summary>
    public string contentId { get; init; } = "";
}

/// <summary>释放路径上的 overlay.close 载荷：组件析构时只请求隐藏，不再回调。</summary>
internal sealed record FloatingNotifyClosedJsConfig
{
    /// <summary>恒为 false——释放路径上没有东西需要被告知退场动画结束。</summary>
    public bool notifyClosed { get; init; }
}

/// <summary>slider.initialize 载荷。</summary>
internal sealed record SliderJsConfig
{
    /// <summary>滑块方向："horizontal" 或 "vertical"。</summary>
    public string orientation { get; init; } = "horizontal";
}

/// <summary>menuKeyboard.initialize 载荷（右键菜单与菜单栏共用同一 JS 模块）。</summary>
internal sealed record MenuKeyboardJsConfig
{
    /// <summary>"vertical" 用于下拉/右键菜单；"menubar" 额外响应左右方向键。</summary>
    public string mode { get; init; } = "vertical";

    /// <summary>方向键是否从末项回卷到首项。</summary>
    public bool loop { get; init; }

    /// <summary>揭示后的焦点落点："first"、"last"、"container"，或 null 保持现有焦点。</summary>
    public string? initialFocus { get; init; }
}

/// <summary>scrollbar 模块的滚动条方向载荷。</summary>
internal sealed record ScrollAreaJsConfig
{
    /// <summary>滚动条方向："vertical" 或 "horizontal"。</summary>
    public string orientation { get; init; } = "vertical";
}
