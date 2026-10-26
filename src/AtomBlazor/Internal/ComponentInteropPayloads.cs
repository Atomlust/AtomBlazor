namespace AtomBlazor;

/// <summary>
/// 组件通过 JS interop 发给浏览器的配置载荷
/// （carousel configure、compositionGuard.attach、motion/height/indicator、
/// dashboardGrid、drawer-snaps、theme.initialize、tagInput.initialize 与 quill 编辑器选项）。
/// </summary>
/// <remarks>
/// 为什么用命名 record 而不是匿名类型（与 <see cref="TextInputJsConfig"/> 同理）：
/// <list type="number">
///   <item>消费方开启 PublishTrimmed（Blazor WASM 体积优化）时，STJ 反射序列化
///         需要载荷类型的构造函数与属性 getter 存活。匿名类型只能走构造函数绑定
///         序列化，而裁剪管线会剥掉构造函数参数名，运行时抛出
///         <c>ConstructorContainsNullParameterNames</c>。命名 record 用默认构造 +
///         init 属性，STJ 走属性序列化路径，与构造函数参数名彻底无关。</item>
///   <item>匿名类型是编译器生成的内部类型，名称随编译重编号
///         （<c>&lt;&gt;f__AnonymousTypeN`M</c>），无法在 ILLink 根描述符中稳定
///         引用；命名类型可以精确定位。</item>
/// </list>
/// 属性名刻意保持与 JS 侧约定一致的小写形式：JSON 键名不随序列化命名策略漂移，
/// 与改造前匿名类型的输出逐字节一致。
/// </remarks>
internal sealed record CarouselJsConfig
{
    /// <summary>当前活动 slide 索引。</summary>
    public int index { get; init; }

    /// <summary>是否垂直方向滚动。</summary>
    public bool vertical { get; init; }

    /// <summary>是否允许拖拽。</summary>
    public bool draggable { get; init; }

    /// <summary>是否开启自动播放。</summary>
    public bool autoplay { get; init; }

    /// <summary>自动播放间隔（毫秒）。</summary>
    public int interval { get; init; }

    /// <summary>自动播放当前是否暂停。</summary>
    public bool paused { get; init; }

    /// <summary>是否循环滚动。</summary>
    public bool loop { get; init; }
}

/// <summary>compositionGuard.attach 载荷（CommandInput 与 MaskedInput 共用同一 JS 模块）。</summary>
internal sealed record CompositionGuardJsConfig
{
    /// <summary>组合输入法期间要抑制的 DOM 事件名。</summary>
    public string[] suppress { get; init; } = [];
}

/// <summary>height（高度动画）载荷。</summary>
internal sealed record HeightAnimationJsConfig
{
    /// <summary>目标状态是否展开。</summary>
    public bool expanded { get; init; }

    /// <summary>是否启用动画。</summary>
    public bool enabled { get; init; }

    /// <summary>动画时长（毫秒）。</summary>
    public int duration { get; init; }

    /// <summary>缓动函数名。</summary>
    public string easing { get; init; } = "ease-out";
}

/// <summary>motion（出入场动画）载荷。</summary>
internal sealed record MotionJsConfig
{
    /// <summary>目标可见状态。</summary>
    public bool visible { get; init; }

    /// <summary>动效预设名。</summary>
    public string preset { get; init; } = "";

    /// <summary>触发方式名。</summary>
    public string trigger { get; init; } = "";

    /// <summary>动画时长（毫秒）。</summary>
    public int duration { get; init; }

    /// <summary>动画延迟（毫秒）。</summary>
    public int delay { get; init; }

    /// <summary>缓动函数名。</summary>
    public string easing { get; init; } = "ease-out";

    /// <summary>位移距离（像素）。</summary>
    public double distance { get; init; }

    /// <summary>是否在首次渲染时也执行入场动画。</summary>
    public bool first { get; init; }

    /// <summary>是否只执行一次。</summary>
    public bool once { get; init; }

    /// <summary>视口交叉判定阈值（0–1）。</summary>
    public double threshold { get; init; }

    /// <summary>自定义关键帧（未设置时为 null）。</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object>>? keyframes { get; init; }
}

/// <summary>indicator（选中态指示条）载荷。</summary>
internal sealed record SelectionIndicatorJsConfig
{
    /// <summary>选中元素的选择器。</summary>
    public string selector { get; init; } = "";

    /// <summary>悬停跟随元素的选择器（未启用时为 null）。</summary>
    public string? hoverSelector { get; init; }

    /// <summary>移动动画时长（毫秒）。</summary>
    public int duration { get; init; }
}

/// <summary>dashboardGrid 的 initialize / updateGridOptions 载荷（两个函数同一形状）。</summary>
internal sealed record DashboardGridJsConfig
{
    /// <summary>大屏列数。</summary>
    public int columns { get; init; }

    /// <summary>行高（像素）。</summary>
    public int rowHeight { get; init; }

    /// <summary>网格间距（像素）。</summary>
    public int gap { get; init; }

    /// <summary>是否允许拖拽。</summary>
    public bool allowDrag { get; init; }

    /// <summary>是否允许缩放。</summary>
    public bool allowResize { get; init; }

    /// <summary>是否处于编辑态。</summary>
    public bool editable { get; init; }

    /// <summary>是否紧凑布局。</summary>
    public bool compact { get; init; }

    /// <summary>是否显示网格辅助线。</summary>
    public bool showGridGuide { get; init; }

    /// <summary>中屏断点（像素）。</summary>
    public int mediumBreakpoint { get; init; }

    /// <summary>小屏断点（像素）。</summary>
    public int smallBreakpoint { get; init; }

    /// <summary>中屏列数。</summary>
    public int mediumColumns { get; init; }

    /// <summary>小屏列数。</summary>
    public int smallColumns { get; init; }
}

/// <summary>drawer-snaps 的 configure 载荷。</summary>
internal sealed record DrawerSnapsJsConfig
{
    /// <summary>抽屉方向："bottom"、"top"、"left"、"right"。</summary>
    public string direction { get; init; } = "bottom";

    /// <summary>吸附点列表（0–1 的比例值）。</summary>
    public IReadOnlyList<double> points { get; init; } = [];

    /// <summary>当前吸附点索引。</summary>
    public int index { get; init; }

    /// <summary>拖拽越过阈值时是否请求关闭。</summary>
    public bool dismiss { get; init; }
}

/// <summary>theme.initialize 载荷。</summary>
internal sealed record ThemeInitializeJsConfig
{
    /// <summary>是否把偏好持久化到 localStorage。</summary>
    public bool persist { get; init; }

    /// <summary>是否探测操作系统配色偏好。</summary>
    public bool detectSystemPreference { get; init; }

    /// <summary>无持久化偏好时套用的默认主题。</summary>
    public ThemeDefaultsJsConfig defaults { get; init; } = new();

    /// <summary>合法基础（灰阶）色名（小写）——让浏览器就地回落损坏的持久化条目。</summary>
    public string[] validBaseColors { get; init; } = [];

    /// <summary>合法主色名（小写）——让浏览器就地回落损坏的持久化条目。</summary>
    public string[] validPrimaryColors { get; init; } = [];
}

/// <summary><see cref="ThemeInitializeJsConfig"/> 的 defaults 子对象：默认主题值。</summary>
internal sealed record ThemeDefaultsJsConfig
{
    /// <summary>默认是否暗色。</summary>
    public bool isDarkMode { get; init; }

    /// <summary>默认基础（灰阶）色名（小写）。</summary>
    public string baseColor { get; init; } = "";

    /// <summary>默认主色名（小写）。</summary>
    public string primaryColor { get; init; } = "";

    /// <summary>默认圆角半径（rem）。</summary>
    public double radius { get; init; }

    /// <summary>默认设计（密度/字体/表面/菜单色/菜单强调色的小写取值）。</summary>
    public ThemeDesignJsConfig design { get; init; } = new();
}

/// <summary>theme 载荷的 design 子对象：设计五项的小写取值。</summary>
internal sealed record ThemeDesignJsConfig
{
    /// <summary>密度。</summary>
    public string density { get; init; } = "";

    /// <summary>字体。</summary>
    public string font { get; init; } = "";

    /// <summary>表面。</summary>
    public string surface { get; init; } = "";

    /// <summary>菜单背景与文字颜色。</summary>
    public string menuColor { get; init; } = "";

    /// <summary>菜单焦点与悬停颜色。</summary>
    public string menuAccent { get; init; } = "";
}

/// <summary>tagInput.initialize 载荷。</summary>
internal sealed record TagInputJsConfig
{
    /// <summary>新增标签的触发方式（AddTrigger 位标志的整数值）。</summary>
    public int triggers { get; init; }
}

/// <summary>quill 富文本编辑器的初始化选项载荷。</summary>
internal sealed record RichTextEditorJsConfig
{
    /// <summary>编辑器空态占位文本。</summary>
    public string placeholder { get; init; } = "";

    /// <summary>是否处于只读态。</summary>
    public bool readOnly { get; init; }

    /// <summary>是否接入了图片上传处理器。</summary>
    public bool hasImageUploader { get; init; }
}
