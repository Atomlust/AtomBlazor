<div align="center">

# AtomBlazor

**为 Blazor 而生的 shadcn/ui 级组件库——无头基元打底，精致默认样式交付，100% 纯 .NET。**

*让你的 Blazor 应用，长出 Vercel 级的界面。*

[![NuGet](https://img.shields.io/nuget/v/AtomBlazor.svg)](https://www.nuget.org/packages/AtomBlazor)
![.NET](https://img.shields.io/badge/.NET-11.0+-512BD4?logo=.net&logoColor=white)
![可裁剪](https://img.shields.io/badge/trim-AOT%20ready-success)
![Tailwind CSS](https://img.shields.io/badge/Tailwind_CSS-v4-38BDF8?logo=tailwindcss&logoColor=white)
![License](https://img.shields.io/badge/license-Apache--2.0-blue)

[English](./README.md) | **[简体中文](./README.zh-CN.md)**

</div>

---

## 一眼看全这套库

| 组件 | 无头基元 | 内联 SVG 图标 | 样式表（gzip） | 自动化测试 | 演示页 |
| :---: | :---: | :---: | :---: | :---: | :---: |
| **474**<br>141 个族 | **128**<br>40 个族 | **5,393** | **< 30 KB** | **4,773** | **216** |

*以上每个数字都是从本仓库实测得出，没有为了宣传四舍五入。组件数直接取自公开 API 基线：`AtomBlazor` 有 474 个具体 `ComponentBase` 类型，`AtomBlazor.Primitives` 有 128 个。*

## 为什么选择 AtomBlazor？

- **141 个族、474 个生产级组件。** 从按钮到 DataGrid、甘特图、日程、透视表、图表与富文本编辑器——统一采用 shadcn/ui 现代视觉语言，由 Tailwind CSS v4 驱动。不是让你自己收尾的脚手架，而是可以直接交付的完整目录。
- **双层架构。** 在 **AtomBlazor.Primitives**（40 个族、128 个无样式且完全可组合的基元，内置 ARIA 属性与完整键盘导航）之上构建你自己的设计系统；或直接使用其上的 474 个预样式组件，即刻交付。同一台引擎，你的品牌。
- **真正可用的主题系统。** 单一 OKLCH 设计令牌体系驱动亮/暗双模、5 套基础色、17 种主色、圆角与密度预设——全部支持**运行时切换**并跨刷新持久化。无需双份样式表，没有 `!important` 大战，不用重新构建。
- **覆盖这么全，体积却小得离谱。** 全部 474 个组件的完整样式表加上整套主题层，gzip 后**不到 30 KB**（26.7 KB + 3.1 KB）。一份 CSS，装下所有组件、所有主题。
- **Blazor 能跑的地方它都能跑。** 四种宿主模型全部在 CI 中实测通过：Blazor Server、WebAssembly、Auto、MAUI Blazor Hybrid。不是「应该能用」——每次提交都真实构建并测试。
- **默认可裁剪，零裁剪警告。** 库工程开启 `IsAotCompatible`，经 ILLink 分析构建干净，并由专门的 AOT 冒烟任务守住。**Native AOT 有一点需注意**：动态筛选与排序路径会编译为 LINQ 表达式树，需要动态代码——以 Native AOT 部署时，请在内存中预先完成筛选与排序（或改用强类型 LINQ）。
- **JS 互操作不添乱。** 交互模块在首次渲染时惰性 `import`，按运行时缓存并附版本戳；仅主题引导脚本预加载。运行期不需要 npm，你的流水线里不需要打包器，也没有新的 JS 框架要学。
- **5,393 枚图标，零网络请求。** 四个图标包——Lucide（1,753 枚）、Heroicons（1,288 枚，Outline/Solid/Mini/Micro 四变体）、Feather（286 枚）、Font Awesome（2,066 枚，Solid/Regular/Brands）——以内联 SVG 编译进程序集。无 HTTP 往返，离线可用，且每个图标包都是独立程序集——只引用你用得到的那些。
- **本地化不是事后补的。** 全部 574 条组件字符串统一走 `ILocalizer`。启动时覆盖单个键，或子类化对接 `IStringLocalizer`，即可运行时切换文化。
- **按基础设施的标准做测试。** 4,302 个 C# 测试（含渲染标记快照）加 471 个 JavaScript 模块测试——4,773 道自动化检查横在你和回归缺陷之间。

## 环境要求

> **[!IMPORTANT]**
> AtomBlazor 面向 **.NET 11 及以上版本**（`net11.0`）。**不兼容** .NET 10、.NET 8 或任何更早版本。

## 包一览

| 包 | NuGet | 说明 |
| --- | --- | --- |
| 组件 | [`AtomBlazor`](https://www.nuget.org/packages/AtomBlazor) | 141 个族、474 个预样式组件（依赖 Primitives；内置 Markdig/HtmlSanitizer/TailwindMerge.NET/Ical.Net） |
| 基元 | [`AtomBlazor.Primitives`](https://www.nuget.org/packages/AtomBlazor.Primitives) | 40 个族、128 个无样式可组合基元（Portal / Positioning / Focus / Overlay 等服务） |
| 图标 | [`AtomBlazor.Icons.Lucide`](https://www.nuget.org/packages/AtomBlazor.Icons.Lucide) | Lucide 图标（1,753 枚，内联 SVG） |
| 图标 | [`AtomBlazor.Icons.Heroicons`](https://www.nuget.org/packages/AtomBlazor.Icons.Heroicons) | Heroicons（1,288 枚 — Outline / Solid / Mini / Micro） |
| 图标 | [`AtomBlazor.Icons.Feather`](https://www.nuget.org/packages/AtomBlazor.Icons.Feather) | Feather 图标（286 枚） |
| 图标 | [`AtomBlazor.Icons.FontAwesome`](https://www.nuget.org/packages/AtomBlazor.Icons.FontAwesome) | Font Awesome Free（2,066 枚 — Solid / Regular / Brands） |

## 组件目录

**141 个组件族，无死角覆盖。** 按「你要构建什么」分组，而非按字母排序。所有组件共享同一套 OKLCH 令牌主题、完整键盘支持与 ARIA 无障碍语义——会用一族，其余各族都是同一套约定。
### Buttons & Actions（按钮与动作）

*你需要的每一个可点击目标。*

| 组件 | 说明 |
| --- | --- |
| `Button` | 主力按钮——五种视觉变体、加载与图标状态、AsChild 组合模式 |
| `ButtonGroup` | 分段按钮组，多个动作浑然一体 |
| `SplitButton` | 主操作 + 备选操作下拉 |
| `Fab` | 悬浮操作按钮，突出关键动作 |
| `Toggle` | 两态按压切换 |
| `Link` | 套用按钮设计语言的超链接 |

### Text & Numeric Inputs（文本与数字输入）

*想怎么输就怎么输，格式却从不失控。*

| 组件 | 说明 |
| --- | --- |
| `Input` | 单行文本输入，支持前后缀装饰、字符上限与校验样式 |
| `Textarea` | 自动增高的多行文本域 |
| `MaskedInput` | 格式掩码输入：手机号、证件号、车牌等 |
| `NumericInput` | 数字输入，步进器与精度控制 |
| `CurrencyInput` | 金额输入，货币符号与千分位 |
| `InputOTP` | 一次性验证码输入框 |
| `InputGroup` | 组合输入框，可拼接按钮与下拉 |
| `TagInput` | 回车即成标签，高效收集输入 |

### Selection Controls（选择控件）

*从一个开关到一整棵组织架构树。*

| 组件 | 说明 |
| --- | --- |
| `Select` | 自定义下拉选择，完整键盘导航 |
| `NativeSelect` | 原生 select，外观与全库一致 |
| `MultiSelect` | 多选下拉，选中项呈标签 |
| `Combobox` | 输入即联想的自动完成 |
| `ListBox` | 可滚动选择列表面板 |
| `Checkbox` | 复选框，支持半选态 |
| `CheckboxGroup` | 复选框组，一次绑定 |
| `RadioGroup` | 单选组，焦点游走 |
| `Switch` | 用户最熟悉的开关 |
| `Cascader` | 级联选择，逐层钻取 |
| `TreeSelect` | 树形下拉选择 |
| `PickList` | 双列表穿梭选择 |
| `Sortable` | 拖拽排序列表 |

### Date & Time（日期与时间）

*一个日期、一段区间、一个时刻，一次搞定。*

| 组件 | 说明 |
| --- | --- |
| `DatePicker` | 日历弹层选择器，支持预设 |
| `DateRangePicker` | 起止日期一次选好 |
| `DateTimePicker` | 日期与时间一框搞定 |
| `DateInput` | 可直接键入的日期输入 |
| `TimeInput` | 可直接键入的时间输入 |
| `TimePicker` | 下拉时间选择 |

### Sliders, Files & More（滑块、文件与其他输入）

*别的库忘记做的输入，这里都有。*

| 组件 | 说明 |
| --- | --- |
| `Slider` | 单值滑块，支持刻度 |
| `RangeSlider` | 双滑块区间选择 |
| `Rating` | 星级评分，可交互或只读 |
| `QuantityStepper` | 数量加减步进器 |
| `ColorPicker` | 取色器：色板 + 自定义颜色 |
| `FileUpload` | 拖拽上传，进度可见 |
| `Attachment` | 附件列表，一目了然 |
| `Signature` | 手写签名板，触屏鼠标皆可 |

### Forms & Validation（表单与校验）

*从裸输入到 Schema 驱动的动态表单，每步只要一行。*

| 组件 | 说明 |
| --- | --- |
| `Label` | 无障碍表单标签 |
| `Field` | 标签 + 提示 + 错误的布局外壳 |
| `InputField` | 一个标签搞定带外壳的输入 |
| `FormField*` | 25 个现成字段包装器——上述输入全部预接好标签、提示、错误文案与校验状态 |
| `DynamicForm` | 配置即表单：按 Schema 动态渲染 |
| `FormWizard` | 分步表单，进度一目了然 |

### Overlays & Feedback（弹层与反馈）

*更少打断，更多告知。*

| 组件 | 说明 |
| --- | --- |
| `Dialog` | 模态对话框，焦点进得来也回得去 |
| `AlertDialog` | 危险操作确认框 |
| `Sheet` | 屏幕边缘滑出面板 |
| `Drawer` | 抽屉式导航与内容 |
| `Popover` | 锚定弹层，承载丰富内容 |
| `Tooltip` | 悬停即时提示 |
| `HoverCard` | 悬停富预览卡片 |
| `ContextMenu` | 任意位置的右键菜单 |
| `DropdownMenu` | 动作菜单，支持子菜单与勾选 |
| `Menubar` | 桌面风格的菜单栏 |
| `Command` | 命令面板，高手的最爱 |
| `Toast` | 轻通知，不打断 |
| `Message` | 行内状态消息 |
| `Alert` | 信息/成功/警告/危险四色警示 |
| `NotificationBadge` | 任意元素上的红点与计数 |
| `ExitPrompt` | 未保存离开前的拦截确认 |
| `Empty` | 优雅的空状态 |
| `Skeleton` | 加载中的微光占位 |
| `Spinner` | 不确定性加载指示 |
| `Progress` | 确定性进度条 |

### Navigation（导航）

*在应用里前进的每一种方式。*

| 组件 | 说明 |
| --- | --- |
| `Tabs` | 最符合直觉的标签页导航 |
| `Breadcrumb` | 深层页面的层级轨迹 |
| `Pagination` | 任何列表都适用的分页 |
| `NavigationMenu` | 横向导航菜单 |
| `BottomNav` | 移动端风格的底部导航 |
| `ResponsiveNav` | 一个组件，桌面/移动自适应导航 |
| `Stepper` | 引导式分步流程 |
| `TreeView` | 可展开的层级树 |
| `Sidebar` | 完整应用侧栏，分组可折叠 |
| `AppBar` | 顶部应用栏 |
| `Dock` | IDE 式停靠与最大化面板布局 |
| `ScrollToTop` | 该出现时才出现的回到顶部 |

### Data Display & Visualization（数据展示与可视化）

*从一枚徽章到一张甘特图，全梯队覆盖。*

| 组件 | 说明 |
| --- | --- |
| `DataGrid` | 旗舰级表格——排序、筛选、分页，客户端/服务端数据源皆宜 |
| `DataTable` | 轻量表格，该有的都有 |
| `DataView` | 工具栏驱动的列表/卡片数据视图 |
| `PivotDataGrid` | 透视数据网格，多维汇总 |
| `FilterBuilder` | 面向最终用户的可视化筛选构造器 |
| `Calendar` | 月历选择，可显示农历副标签 |
| `EventCalendar` | 事件日历，日程内容就地呈现 |
| `Scheduler` | 时间排程器 |
| `Gantt` | 甘特图，项目排期可视化 |
| `Chart` | 基于 Apache ECharts 的交互图表 |
| `Timeline` | 时间线叙事 |
| `Accordion` | 手风琴折叠区块 |
| `Collapsible` | 单区块折叠展开 |
| `Avatar` | 用户头像，带回退与组合 |
| `Badge` | 状态徽章 |
| `Chip` | 紧凑标签 Chip |
| `Kbd` | 按键可视化 |
| `Typography` | 完整的排版字号体系 |
| `SectionHeader` | 统一的区块标题 |
| `Image` | 图片展示，带加载与回退 |
| `Carousel` | 触控友好的轮播 |
| `Highlighter` | 命中关键词高亮 |
| `CopyText` | 一键复制，反馈可见 |
| `Marker` | 紧凑行内标记链接 |
| `Bubble` | 对话式界面的聊天气泡 |

### Editors & Generators（编辑器与生成器）

*重型内容能力，零外部服务依赖。*

| 组件 | 说明 |
| --- | --- |
| `RichTextEditor` | 开箱即用的所见即所得编辑器 |
| `MarkdownEditor` | Markdown 编辑与实时预览 |
| `QrCode` | 纯本地渲染的二维码，零服务调用 |
| `Barcode` | 纯本地渲染的条形码，零服务调用 |

### Layout（布局）

*只搭台，不抢戏。*

| 组件 | 说明 |
| --- | --- |
| `Card` | 万能内容容器 |
| `AspectRatio` | 锁定宽高比 |
| `Separator` | 横竖皆宜的分隔线 |
| `Resizable` | 拖拽调整面板大小 |
| `ScrollArea` | 自定义样式的滚动区 |
| `DashboardGrid` | 可拖拽缩放的仪表盘网格 |
| `Item` | 媒体/内容/动作的组合条目 |

### Platform（平台能力）

*让其余一切都好用背后那台机器。*

| 组件 | 说明 |
| --- | --- |
| `Theme` | 运行时主题引擎：色板、圆角、密度、暗色模式 |
| `Motion` | 声明式出入场动效 |

## 快速上手

**1. 安装包**

```bash
dotnet add package AtomBlazor
dotnet add package AtomBlazor.Icons.Lucide   # 可选，按需选择图标集
```

**2. 注册服务**（`Program.cs`）

```csharp
builder.Services.AddAtomBlazorComponents(
    configureTheme: options =>
    {
        options.DefaultBaseColor = BaseColor.Zinc;
        options.DetectSystemPreference = true; // 跟随系统亮/暗
    });
```

完整签名：`AddAtomBlazorComponents(Action<DefaultLocalizer>? , Action<ThemeOptions>? , Action<OverlayRenderingOptions>?)`。

**3. 引入样式**（宿主页 `<head>`）——样式随组件库以静态资源交付：

```html
<link href="_content/AtomBlazor/atomblazor.css" rel="stylesheet" />
<link href="_content/AtomBlazor/css/themes.css" rel="stylesheet" />
<script src="_content/AtomBlazor/js/theme-init.js"></script>
```

**4. 引入命名空间**（`_Imports.razor`）

```razor
@using AtomBlazor
@using AtomBlazor.Primitives
@using AtomBlazor.Icons.Lucide
```

**5. 使用组件**

```razor
<Button Variant="ButtonVariant.Default" OnClick="@(() => count++)">计数：@count</Button>
<LucideIcon Name="house" Size="20" />
```

## 主题：一套令牌，贯穿所有组件

运行时改变整个应用的气质——不换样式表、不重新构建、不写 `!important` 覆盖。

```razor
@inject ThemeService Themes

<Button OnClick="@(() => Themes.SetPrimaryColorAsync(PrimaryColor.Violet))">换成紫罗兰</Button>
<Button OnClick="@(() => Themes.ToggleDarkModeAsync())">切换暗色</Button>
<Button OnClick="@(() => Themes.SetPresetAsync(ThemePreset.Compact))">紧凑密度</Button>
```

| 旋钮 | 可选值 |
| --- | --- |
| 基础调色板 | Zinc · Slate · Gray · Neutral · Stone |
| 主色 | Default + 17 种命名色相，从 Blue 到 Pink |
| 暗色模式 | 亮色 · 暗色 · 跟随系统偏好 |
| 密度预设 | Standard · Compact · Dense · Spacious |
| 圆角半径 | 0 到 4 rem 任意取值，即时生效 |

每个旋钮彼此正交：Slate 基础色配 Amber 主色、Dense 密度、0.75 rem 圆角，随意组合。选择会持久化到 `localStorage`，并由一段极小的阻塞式引导脚本在**首次绘制之前**还原——暗色用户永远看不到白闪，你的 CSP 也不需要 `unsafe-inline`。完整令牌参考：[THEMING.md](./THEMING.md)。

## 本地化：574 条字符串，一个接口

474 个组件里的每一个占位符、按钮文案、`aria-label` 与空状态提示，都经由 `ILocalizer` 解析。没有任何字符串硬编码在标记里。

```csharp
// 模式 A —— 启动时覆盖单个键
builder.Services.AddAtomBlazorComponents(localizer =>
{
    localizer.Set("DataGrid.Loading", "加载中...");
    localizer.Set("Pagination.Next", "下一页");
});

// 模式 B —— 子类化，借 IStringLocalizer 实现运行时文化切换
public class AppLocalizer(IStringLocalizer<SharedResources> loc) : DefaultLocalizer
{
    public override string this[string key] =>
        loc[key] is { ResourceNotFound: false } found ? found.Value : base[key];
}
```

库内置英文默认值；键采用点分记法（`DataGrid.Loading`），格式化使用标准 `string.Format` 占位符（`"Showing {0}–{1} of {2}"`）。

## 按基础设施的标准打造

UI 库是承重代码，就该按承重代码的标准来测。

- **4,302 个 C# 测试** —— 覆盖行为、ARIA 语义、键盘导航，以及渲染标记快照：哪怕只有一个类名漂移，构建也会失败。
- **471 个 JavaScript 模块测试** —— 互操作层被当作真实代码来测，而不是「假定它能跑」，并且每次提交都由 CI 把关。
- **一份公共 API 基线** —— 每个组件参数、枚举与服务接口都有快照。意外的破坏性变更混不进发布版本。
- **一个 AOT 冒烟任务** —— 裁剪与 Native AOT 兼容性由 CI 实证，不是靠 README 自夸。
- **四种宿主模型每次提交都构建** —— Server、WebAssembly、Auto、MAUI Blazor Hybrid。
- **216 个演示页、1,268 段可直接复制的示例** —— 运行 `demos/` 下任意项目，即可在线浏览整个组件目录。

## 渲染模式

AtomBlazor 不绑定宿主形态，已在四种宿主实测：

- **Blazor Server**（`demos/AtomBlazor.Demo.Server`）
- **Blazor WebAssembly**（`demos/AtomBlazor.Demo.Wasm`）
- **Auto**（`demos/AtomBlazor.Demo.Auto`）
- **MAUI Blazor Hybrid**（`demos/AtomBlazor.Demo.Maui`，实证 Portal / JS 模块惰性加载 / `_content/` 静态资源路径）

## 文档

- **主题定制指南** —— [THEMING.md](./THEMING.md)（中文）
- **参与贡献 / 构建与测试** —— [CONTRIBUTING.md](./CONTRIBUTING.md)（中文）
- 在线组件浏览器：运行 `demos/` 下任意宿主项目

## 许可

Apache-2.0。第三方图标与字体遵循其原始许可（Lucide/Heroicons/Feather = MIT，Font Awesome 图标 = SIL OFL / CC-BY-4.0），详见各包 `LICENSE` 与 `THIRD-PARTY-NOTICES.txt`，完整许可文本见根目录 [`LICENSE`](./LICENSE)。

> 本项目用于学习与二次开发，并非 shadcn/ui、Tailwind Labs 或上述任一图标库的官方产品。
