# AtomBlazor.Icons.Feather

为 Blazor 应用提供 Feather 图标库——286 枚简约线条 SVG 图标（24×24、2px 描边）。
自 .Icons.Feather（图标美术为 MIT，见 THIRD-PARTY-NOTICES.txt）。

## 特性

- **286 个图标**：完整 Feather 图标集
- **组件式 API**：`<FeatherIcon Name="camera" />`
- **ARIA 支持**：可定制 aria-label
- **可主题化**：图标默认继承父级颜色，硬编码 stroke 已在渲染时剥离，`Color` 参数生效
- **裁剪友好**：查找表为大小写不敏感字典，随程序集裁剪未用图标

## 用法

在 `_Imports.razor` 中加入：

```razor
@using AtomBlazor.Icons.Feather
```

```razor
<FeatherIcon Name="camera" />
<FeatherIcon Name="heart" Size="32" Color="red" StrokeWidth="1.5" />
```

## 许可

包装代码与图标美术均为 MIT。详见 `LICENSE` 与 `THIRD-PARTY-NOTICES.txt`。
