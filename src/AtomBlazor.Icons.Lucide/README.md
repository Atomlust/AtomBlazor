# AtomBlazor.Icons.Lucide

为 Blazor 应用提供 Lucide 图标库——1753 个风格统一的 SVG 图标。（Apache-2.0 ，图标美术保留 ISC/MIT 许可，见 THIRD-PARTY-NOTICES.txt）。

## 特性

- **1753 个图标**：完整 Lucide 图标集
- **组件式 API**：`<LucideIcon Name="camera" />`
- **ARIA 支持**：可定制 aria-label
- **裁剪友好**：发布时未使用的代码可被装配裁剪移除；查找表基于 FrozenDictionary，读取无锁且分配最少
- **类型安全**：完整中文 XML 文档与 IntelliSense
- **可主题化**：图标默认继承父级颜色，支持 CSS 变量

## 用法

在 `_Imports.razor` 中加入：

```razor
@using AtomBlazor.Icons.Lucide
```

```razor
<LucideIcon Name="camera" />
<LucideIcon Name="heart" Size="32" Color="red" />
```

## 许可

包装代码 MIT；图标美术 ISC（部分 Feather 图标为 MIT）。详见 `LICENSE` 与 `THIRD-PARTY-NOTICES.txt`。
