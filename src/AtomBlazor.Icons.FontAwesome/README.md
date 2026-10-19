# AtomBlazor.Icons.FontAwesome

为 Blazor 应用提供 Font Awesome Free 图标库——2066 枚图标，覆盖 3 种变体（solid、regular、brands）。
自 .Icons.FontAwesome（包装代码 MIT；Font Awesome Free 美术为 CC-BY-4.0，见 THIRD-PARTY-NOTICES.txt）。

## 特性

- **3 种变体**：`Variant` 参数选择 Solid、Regular、Brands（默认 Solid）
- **非方形宽高比**：按图标固有 width/height 等比缩放高度并生成 viewBox（Brands 图标尤其非方形）
- **组件式 API**：`<FontAwesomeIcon Name="github" Variant="FontAwesomeIconVariant.Brands" />`
- **可主题化**：硬编码 fill/stroke 已在渲染时剥离，`Color` 参数生效

## 用法

在 `_Imports.razor` 中加入：

```razor
@using AtomBlazor.Icons.FontAwesome
@using AtomBlazor.Icons.FontAwesome.Data
```

```razor
<FontAwesomeIcon Name="camera" />
<FontAwesomeIcon Name="github" Variant="FontAwesomeIconVariant.Brands" Size="32" Color="#181717" />
```

## 许可

包装代码 MIT；Font Awesome Free 图标美术 CC-BY-4.0。详见 `LICENSE` 与 `THIRD-PARTY-NOTICES.txt`。
