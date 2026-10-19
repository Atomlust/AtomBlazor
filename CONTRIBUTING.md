# 贡献指南（Contributing）

AtomBlazor 是一套基于 Blazor 的组件库（Tailwind CSS v4 + shadcn/ui 视觉风格），分 Primitives 与 Components 两层，并附带四个图标包。本文说明本仓的开发、构建、测试与打包流程。

## 1. 环境

- **.NET 11 SDK**（预览频道；本仓用 `11.0.100-rc.2`）。`global.json` 锁定 SDK 版本。
- **工作负载**：库工程开 `IsAotCompatible`；MAUI Hybrid 宿主需 `maui-windows`；Wasm 的 **AOT**（`RunAOTCompilation`）额外需 `wasm-tools`。
- **Tailwind CSS v4**（standalone）——经 `tools/atom.tailwind.targets` 在 `dotnet build` 时把 `wwwroot/css/atom-input.css` 构建为 `wwwroot/atomblazor.css`、把 Demo 的 `app-input.css` 构建为 `app.css`。产物 `atomblazor.css` 为提交物；改样式改 `*-input.css` 源，勿手改产物。
- **Node.js ≥ 24**——跑 JS 单测与 Playwright。

## 2. 仓库结构

```
src/AtomBlazor.Primitives/     无样式基元 + 服务（Portal/Positioning/Focus/Overlay/JsModules）
src/AtomBlazor/                预样式组件库（各家族一目录）+ wwwroot/{atomblazor.css,js,lib}
src/AtomBlazor.Icons.*/        Lucide / Heroicons / Feather / FontAwesome 图标包（Data.cs 由 ps1 生成）
demos/AtomBlazor.Demo.Shared/  文档站页面（Pages/Components·Primitives·Charts·Icons·Guides…）+ CodeExamples
demos/AtomBlazor.Demo.{Server,Wasm,Auto}/  Blazor 三宿主
demos/AtomBlazor.Demo.Maui/    MAUI Blazor Hybrid 宿主（多 TF，本机仅 Windows 可建）
tests/AtomBlazor.Tests/        xUnit（自研轻量渲染器，非 bUnit）+ ApiSurface 快照
tests/js/                          node --test 的 JS 模块单测
tests/browser/                     Playwright 配置（spec 待补）
tools/TrimProbe/                   第三方库裁剪/AOT 冒烟
ui/                                ⚠ 只读参考目录——绝不写入
```

## 3. 构建与测试

```bash
# 全解决方案（标准闸：Release 0 警告 0 错误）
dotnet build AtomBlazor.slnx -c Release

# 单元测试（xUnit，分项计数）
dotnet test tests/AtomBlazor.Tests -c Release

# JS 模块单测
node --test tests/js/*.test.mjs
```

> 串行原则：勿并行两个会 `dotnet build` 的任务——同一 `slnx` 的 `obj/bin` 锁竞争会诱发假失败。

## 4. 编码约定

- **命名空间 / 类型名**：本仓统一使用 `AtomBlazor.*`；新代码请勿引入外部库前缀。
- **JS 互操作**：组件 JS 经 `JsModules` 在首次渲染时惰性 `import`（共享 barrel + camelCase 点分标识符），按运行时/路径缓存并附版本戳；仅 `theme-init.js` 与框架启动脚本预加载。
- **诊断处置**：`BL0016`/`BL0012`（Razor 参数 / 冗余 StateHasChanged）优先调用点或文件级 `#pragma warning disable` + 中文理由；`IL2026`/`IL3050`/`IL2080` 报在源码行用成员级 `UnconditionalSuppressMessage`；报在 `.g.cs` 用工程级 `NoWarn`。**不“顺手修好”既有行为**——会改变条目标识/语义，破坏既有约定的一致性。
- **行尾与编码**：CRLF → LF，EOF 末行换行；源码注释以中文为主。

## 5. 验收闸

- **构建**：`dotnet build AtomBlazor.slnx -c Release` → 0 警告 0 错误。
- **ApiSurface**：`tests/.../ApiSurface` 快照比对，新增词条需与组件名册互证。
- **文档站 / SSR**：文档站页面经 SSR 取证，`<pre>` 数对齐页内 CodeBlock 数；页面内不应出现 `[Source not found]` / `>TODO<` 等占位。

## 6. 铁律

- **`ui/` 只读**：参考目录零写入。
- **破坏性动作先确认**：安装工作负载、下载 Playwright 浏览器、`dotnet pack --push` 等须先行确认。

## 7. 打包

版本采用 `Version` 直钉（当前 `0.1.0`）。`dotnet pack AtomBlazor.slnx -c Release` 产出各包 `.nupkg`（图标包 `Data.cs` 由各包内的 `GenerateIconData.ps1`（依各包图标源 JSON 生成）再生，勿手改）。

## 8. 许可

本项目基于 Apache-2.0 许可（详见 `LICENSE`）；第三方组件许可见各包 `LICENSE` 与 `THIRD-PARTY-NOTICES.txt`。
