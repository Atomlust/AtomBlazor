// 主题模块的真实文件导入测试。
// theme.js 顶层不触碰 document/localStorage（只有函数体内部才访问），
// 因此可以像 barrel 成员一样在无 DOM 环境下求值；这里以导出面作为断言主体，
// 与 PrimitiveJsModuleTests 对 C# 点分标识符 `theme.*` 的核对互为镜像。
//
// theme-init.js 不在此测试：它是经典阻塞 <script> 的 IIFE，顶层立即读取
// document.currentScript/localStorage 并写 <html>——与 primitives barrel 的
// theme-scope 场景同一先例，Node 下导入必炸，故按约定跳过并留此说明。

import assert from 'node:assert/strict';
import { register } from 'node:module';
import { test } from 'node:test';

// components-core.js barrel 自 起静态打包 range-slider.js，其跨包
// drag-updates 说明符按 _content/ 服务布局写成，仓库布局下须先挂解析钩子
//（详见 fixtures/cross-package-resolver.mjs 头注释）才能在 Node 里整图导入。
register(new URL('./fixtures/cross-package-resolver.mjs', import.meta.url));

const themeUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/theme.js', import.meta.url);
const bundleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/components-core.js', import.meta.url);

test('theme.js 可导入且导出面与 ThemeService 的点分标识符一一对应', async () => {
    const mod = await import(themeUrl.href);

    for (const name of [
        'applyTheme', 'applyDarkMode', 'applyBaseColor', 'applyPrimaryColor',
        'applyRadius', 'getPrefersDark', 'loadTheme', 'initialize',
        'saveTheme', 'clearTheme',
    ]) {
        assert.equal(typeof mod[name], 'function', `theme.js 缺少导出 ${name}`);
    }
});

test('components-core.js barrel 以 theme 命名空间再导出核心模块', async () => {
    const bundle = await import(bundleUrl.href);

    assert.equal(typeof bundle.theme, 'object');
    // C# 侧调用点是 `theme.initialize` 等；命名空间即文件名 camelCase 的约定在此验证。
    assert.equal(typeof bundle.theme.initialize, 'function');
    assert.equal(typeof bundle.theme.applyTheme, 'function');
    assert.equal(typeof bundle.theme.saveTheme, 'function');
});
