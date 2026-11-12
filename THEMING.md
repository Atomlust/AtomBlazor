# 主题定制（Theming）

AtomBlazor 的视觉全部由 **CSS 变量**驱动，并支持**亮/暗双模**与**基础色 / 主色 / 圆角 / 密度 / 字体**的运行时切换。所有配色为 OKLCH 单主题体系，无需为亮暗各写一套。

## 1. 引入样式

组件库把样式作为静态资源随包分发（RCL），宿主 `<head>` 直接引用：

```html
<link href="_content/AtomBlazor/atomblazor.css" rel="stylesheet" />
<link href="_content/AtomBlazor/css/themes.css" rel="stylesheet" />
<!-- 首绘前套用已保存的主题，避免暗色用户看到亮色闪烁；须为 <head> 内的经典阻塞脚本 -->
<script src="_content/AtomBlazor/js/theme-init.js"></script>
```

`atomblazor.css` 是单文件产物（Tailwind v4 构建）；`themes.css` 提供基础色/主色档位；`theme-init.js` 从 `localStorage` 键 `atom-theme` 读取上次选择，在首次绘制前落到 `<html>` 的 class 与 `data-*` 属性上（见 §4）。

`theme-init.js` 自身也可用 `data-*` 配置，无需内联脚本：

| 脚本属性 | 作用 |
| --- | --- |
| `data-default-dark="true"` | 没有已保存主题时用暗色，而不是跟随系统偏好 |
| `data-default-dark="false"` | 没有已保存主题时用亮色 |
| `data-storage="false"` | 完全不读 `localStorage`（须与 `data-default-dark` 搭配） |

## 2. 默认主题（注册时配置）

`ThemeOptions` 在服务注册阶段设定初值：

```csharp
builder.Services.AddAtomBlazorComponents(configureTheme: options =>
{
    options.DefaultBaseColor    = BaseColor.Zinc;   // Zinc / Slate / Gray / Neutral / Stone
    options.DefaultPrimaryColor = PrimaryColor.Blue;// Default / Blue / Violet / Purple / Rose / Red / Orange / Amber / Yellow / Lime …
    options.DefaultDarkMode     = false;            // 初始是否暗色
    options.DetectSystemPreference = true;          // true 时跟随 prefers-color-scheme
    options.DefaultPreset       = null;             // 可选 ThemePreset（密度 + 字体 + 表面）
});
```

## 3. 运行时切换（ThemeService）

`ThemeService` 注册为 **Scoped**（每个电路/会话独立状态），公开：

| 方法 | 作用 |
| --- | --- |
| `InitializeAsync()` | 读取持久化状态并应用（一般由 `theme-init.js` 预应用，组件内再初始化） |
| `ToggleDarkModeAsync()` / `SetDarkModeAsync(bool)` | 切换 / 设定暗色 |
| `SetBaseColorAsync(BaseColor)` | 切换基础色（中性灰阶） |
| `SetPrimaryColorAsync(PrimaryColor)` | 切换主色（品牌色） |
| `SetRadiusAsync(double)` | 切换圆角尺度 |
| `SetPresetAsync(ThemePreset)` | 套用一套预设（密度 / 字体 / 表面） |
| `SetDesignAsync(ThemeDesign)` | 套用整体设计档 |

只读状态：`IsDarkMode`、`IsInitialized`。选择会写入 `localStorage["atom-theme"]`，刷新后经 `theme-init.js` 复原。

```razor
@inject ThemeService Theme

<button @onclick="Theme.ToggleDarkModeAsync">切换亮/暗</button>
<button @onclick="() => Theme.SetPrimaryColorAsync(PrimaryColor.Violet)">紫主色</button>
```

## 4. 主题落到 DOM 上的开关

`ThemeService` 与 `theme-init.js` 写的是同一组开关，`themes.css` 也只认这一组。**没有 `data-theme` 属性**——手写它不会有任何效果。

| `<html>` 上的开关 | 取值 | 由谁写入 |
| --- | --- | --- |
| `class="dark"` | 有 / 无 | `SetDarkModeAsync` / `ToggleDarkModeAsync` |
| `data-base-color` | `zinc` / `slate` / `gray` / `neutral` / `stone` | `SetBaseColorAsync` |
| `data-primary-color` | `blue` / `violet` / `purple` / `rose` / `red` / `orange` / `amber` / `yellow` / `lime` …（`Default` 时整个属性被移除） | `SetPrimaryColorAsync` |
| 内联 `--radius` | 如 `0.5rem` | `SetRadiusAsync` |
| `data-atom-density` / `data-atom-font` / `data-atom-surface` / `data-atom-menu-color` / `data-atom-menu-accent` | 各设计档取值 | `SetPresetAsync` / `SetDesignAsync` |

暗色与基础色/主色是**正交**的：`themes.css` 为每个档位同时写了亮色与 `.dark` 两套规则（如 `[data-base-color="slate"]` 与 `.dark[data-base-color="slate"]`），所以切换主色不会丢掉暗色，反之亦然。

`theme.js` 还用 `MutationObserver` 盯着这组开关（`attributeFilter: ['class', 'data-base-color', 'data-primary-color', 'style', …设计档属性]`），因此第三方脚本或 SSR 重渲染把属性剥掉后会被自动补回。

## 5. 核心 CSS 变量

覆盖这些变量即可自定义，无需 !important（组件结构性样式位于 Tailwind 的 `components` 层，你的 `atom:` 工具类在更高层，天然胜出）：

```
--background  --foreground  --border  --ring
--primary  --primary-foreground
--secondary  --muted  --muted-foreground  --accent  --accent-foreground
--popover  --popover-foreground  --card  --card-foreground
--radius
```

示例——在你的样式表里覆盖主色：

```css
:root {
  --primary: oklch(0.55 0.20 265);
  --primary-foreground: oklch(0.98 0.01 265);
}
```

**这些令牌是完整的颜色函数，不是裸通道值。** 因此 `hsl(var(--primary))` / `rgb(var(--primary) / 0.5)` 都是无效 CSS，浏览器会整条丢弃。要透明度就直接写 `oklch(from var(--primary) l c h / 0.5)`，或沿用库内既有的 `atom:bg-primary/50` 工具类（Tailwind v4 用 `color-mix()` 展开，不需要通道拆分）。

## 6. 层级约定

Tailwind 层序：`properties, theme, base, components, atom-utilities, utilities, atom`。

- 组件结构性样式（如重连对话框 `#components-reconnect-modal`）刻意放在 **`components`** 层，使消费方工具类可覆盖。
- 库的 `atom:` 前缀工具类随 `atomblazor.css` 分发；消费方构建**不会重复发射**它们（前缀隔离）。

### 6.1 浮层 z-index 刻度

与上面的 CSS 层序无关——这是浮层的堆叠次序。全库共用一套刻度，新增浮层请落在既有档位上，不要另取魔法数：

| z-index | 谁在用 | 为什么 |
| --- | --- | --- |
| 50 | `FloatingPortal` 默认值、DropdownMenu、Tooltip、HoverCard，以及 Dialog / AlertDialog / Sheet / Drawer 的遮罩与内容 | 常规浮层与模态同档，靠 DOM 先后决定次序 |
| 60 | Menu 子菜单（`MenuSubContent` 及四个包装） | 必须压过父菜单 |
| 9999 | Popover、Select | 刻意跳档：要盖在 Dialog / Sheet / Drawer 的遮罩（50）之上 |
| 10000 | Toast | 通知是瞬时的，被任何打开的浮层盖住就等于没送达，因此永远最上层 |

Popover 与 Select 的 `ZIndex` 是公开参数（默认 9999），可按应用需要调整；Toast 的 10000 是固定的。若你把 Popover/Select 调到 10000 以上，请同时抬高 Toast，否则会重新出现「通知被下拉盖住」。

## 7. 响应式与可访问性

- 亮/暗经 `<html>` 上的 `dark` class 切换（见 §4），并可由 `DetectSystemPreference` 跟随 `prefers-color-scheme`。
- 所有配色为语义变量，图标/文本对比度由 `--*-foreground` 与对应背景成对维护。
- `@media (prefers-reduced-motion: reduce)` 下动画自动降级。
