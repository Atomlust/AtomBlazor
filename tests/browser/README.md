# 浏览器交互测试（脚手架）

本目录是 Playwright 浏览器测试的 ** 脚手架**：当前**没有任何 `*.spec.mjs` 用例**。
真实的浏览器交互回归测试将在 AtomBlazor 演示应用（Server / Wasm /
Auto 三种宿主就绪后）随用例一并。届时可参考源项目（AtomBlazor）的
`tests/browser` 目录结构补充 spec 文件。

## 已就绪内容

- `playwright.config.mjs`：为 server / wasm / auto 三个宿主 × chromium / webkit
  两个浏览器生成 6 个 project（`server-chromium`、`wasm-webkit` 等）。
  失败时保留截图与 trace（输出到被忽略的 `test-results` 目录），不自动重试。
- `package.json`：依赖 `@playwright/test` 1.63.0，`npm test` 即 `playwright test`。

## 演示项目就绪后的运行方式

先在停止演示进程的情况下构建解决方案，再在各自终端中启动三种宿主：

```sh
dotnet build AtomBlazor.slnx
dotnet run --project demos/AtomBlazor.Demo.Server --no-build --launch-profile http
dotnet run --project demos/AtomBlazor.Demo.Wasm --no-build --launch-profile http
dotnet run --project demos/AtomBlazor.Demo.Auto --no-build --launch-profile http
```

HTTP profile 默认端口沿用源项目约定：7172 / 5184 / 5185（Wasm 与 Auto 的 HTTPS
profile 为 7173 / 7174）。若 Atom 演示最终采用不同端口，请修改
`playwright.config.mjs` 中的默认值，或通过环境变量 `ATOM_SERVER_URL`、
`ATOM_WASM_URL`、`ATOM_AUTO_URL` 覆盖。

安装测试依赖与浏览器后运行：

```sh
cd tests/browser
npm ci
npx playwright install chromium webkit
npm test
```

只运行某个宿主/浏览器组合：`npm test -- --project=wasm-chromium`。
使用本机已安装的 Google Chrome 替代内置 Chromium：设置
`ATOM_CHROMIUM_CHANNEL=chrome`。使用单独安装的可兼容 WebKit：将
`ATOM_WEBKIT_EXECUTABLE` 指向其启动器路径（默认仍使用内置版本）。

## 注意

- 在演示应用就绪之前直接运行 `npm test` 会因找不到任何 spec 而报告“没有测试”，属预期现象。
- 不要在本阶段添加 spec；组件级浏览器用例跟随各组件与演示应用一起落地。
