<div align="center">

# AtomBlazor

**为 Blazor 而生的 shadcn/ui 级组件库——无头基元打底，精致默认样式交付，100% 纯 .NET。**

*让你的 Blazor 应用，长出 Vercel 级的界面。*

[![NuGet](https://img.shields.io/nuget/v/AtomBlazor.svg)](https://www.nuget.org/packages/AtomBlazor)
![.NET](https://img.shields.io/badge/.NET-11.0+-512BD4?logo=.net&logoColor=white)
![AOT](https://img.shields.io/badge/AOT-compatible-success)
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
- **AOT 与裁剪就绪。** 库工程开启 `IsAotCompatible`，经 ILLink 裁剪实测**零裁剪警告**——Native AOT 部署是一等公民。
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
@using AtomBlazor.Icons.Lucide.Components
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
