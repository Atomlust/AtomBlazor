# AtomBlazor.Icons.Heroicons

为 Blazor 应用提供 Heroicons 图标库——1288 枚图标，覆盖 4 种变体（outline、solid、mini、micro）。
自 .Icons.Heroicons（图标美术为 MIT，见 THIRD-PARTY-NOTICES.txt）。

## 特性

- **4 种变体**：`Variant` 参数选择 Outline(24×24)、Solid(24×24)、Mini(20×20)、Micro(16×16)
- **自适应尺寸/viewBox**：未指定 `Size` 时按变体默认尺寸与 viewBox 渲染
- **描边控制**：`StrokeWidth` 仅作用于 Outline 变体（默认 1.5）
- **组件式 API**：`<HeroIcon Name="home" Variant="HeroIconVariant.Solid" />`
- **可主题化**：硬编码 stroke/fill 已在渲染时剥离，`Color` 参数生效

## 用法

在 `_Imports.razor` 中加入：

```razor
@using AtomBlazor.Icons.Heroicons
@using AtomBlazor.Icons.Heroicons.Data
```

```razor
<HeroIcon Name="home" />
<HeroIcon Name="bell" Variant="HeroIconVariant.Solid" Size="32" Color="red" />
<HeroIcon Name="check" Variant="HeroIconVariant.Mini" />
```

## 许可

包装代码与图标美术均为 MIT。详见 `LICENSE` 与 `THIRD-PARTY-NOTICES.txt`。
