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

## 为什么选择 AtomBlazor？

- **141 个生产级组件。** 从按钮到 DataGrid、甘特图、日程、透视表、图表与富文本编辑器——统一采用 shadcn/ui 现代视觉语言，由 Tailwind CSS v4 驱动。
- **双层架构。** 在 **AtomBlazor.Primitives**（无样式、完全可组合的基元，内置 ARIA 属性与完整键盘导航）之上构建你自己的设计系统；或直接使用其上的预样式组件，即刻交付。
- **真正可用的主题系统。** 单一 OKLCH 设计令牌体系驱动亮/暗双模、5 套基础色、10+ 主色、圆角与密度预设——全部支持**运行时切换**并跨刷新持久化。无需双份样式表，没有 `!important` 大战。
- **Blazor 能跑的地方它都能跑。** 四种宿主模型全部实测通过：Blazor Server、WebAssembly、Auto、MAUI Blazor Hybrid。
- **默认可裁剪，零裁剪警告。** 库工程开启 `IsAotCompatible`，经 ILLink 分析构建干净。**Native AOT 有一点需注意**：动态筛选与排序路径会编译为 LINQ 表达式树，需要动态代码——以 Native AOT 部署时，请在内存中预先完成筛选与排序（或改用强类型 LINQ）。
- **JS 互操作不添乱。** 交互模块在首次渲染时惰性 `import`，按运行时缓存并附版本戳；仅主题引导脚本预加载。
- **图标零网络请求。** 四个图标包——Lucide（1,753 枚）、Heroicons（Outline/Solid/Mini/Micro 四变体）、Feather（286 枚）、Font Awesome（Solid/Regular/Brands）——以内联 SVG 编译进程序集。无 HTTP 往返，离线可用。

## 环境要求

> **[!IMPORTANT]**
> AtomBlazor 面向 **.NET 11 及以上版本**（`net11.0`）。**不兼容** .NET 10、.NET 8 或任何更早版本。

## 包一览

| 包 | NuGet | 说明 |
| --- | --- | --- |
| 组件 | [`AtomBlazor`](https://www.nuget.org/packages/AtomBlazor) | 141 个预样式组件（依赖 Primitives；内置 Markdig/HtmlSanitizer/TailwindMerge.NET/Ical.Net） |
| 基元 | [`AtomBlazor.Primitives`](https://www.nuget.org/packages/AtomBlazor.Primitives) | 无样式可组合基元（Portal / Positioning / Focus / Overlay 等服务） |
| 图标 | [`AtomBlazor.Icons.Lucide`](https://www.nuget.org/packages/AtomBlazor.Icons.Lucide) | Lucide 图标（1,753 枚，内联 SVG） |
| 图标 | [`AtomBlazor.Icons.Heroicons`](https://www.nuget.org/packages/AtomBlazor.Icons.Heroicons) | Heroicons（Outline / Solid / Mini / Micro） |
| 图标 | [`AtomBlazor.Icons.Feather`](https://www.nuget.org/packages/AtomBlazor.Icons.Feather) | Feather 图标（286 枚） |
| 图标 | [`AtomBlazor.Icons.FontAwesome`](https://www.nuget.org/packages/AtomBlazor.Icons.FontAwesome) | Font Awesome（Solid / Regular / Brands） |

## 组件目录

**141 个组件，无死角覆盖。** 按「你要构建什么」分组，而非按字母排序。所有组件共享同一套 OKLCH 令牌主题、完整键盘支持与 ARIA 无障碍语义——会用一个，其余 140 个就已经会了。
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
| `Input` | 单行文本输入，支持前后缀装饰与校验样式 |
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
| `Calendar` | 月历选择 |
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
