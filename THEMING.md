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

`atomblazor.css` 是单文件产物（Tailwind v4 构建）；`themes.css` 提供基础色/主色档位；`theme-init.js` 从 `localStorage` 键 `atom-theme` 读取上次选择在首次绘制前落到 `<html data-theme=...>`。

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

## 4. 核心 CSS 变量

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

## 5. 层级约定

Tailwind 层序：`properties, theme, base, components, atom-utilities, utilities, atom`。

- 组件结构性样式（如重连对话框 `#components-reconnect-modal`）刻意放在 **`components`** 层，使消费方工具类可覆盖。
- 库的 `atom:` 前缀工具类随 `atomblazor.css` 分发；消费方构建**不会重复发射**它们（前缀隔离）。

## 6. 响应式与可访问性

- 亮/暗经 `data-theme` 属性 + `prefers-color-scheme` 检测切换。
- 所有配色为语义变量，图标/文本对比度由 `--*-foreground` 与对应背景成对维护。
- `@media (prefers-reduced-motion: reduce)` 下动画自动降级。
