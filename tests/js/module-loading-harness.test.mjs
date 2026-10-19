// JS 测试基建冒烟测试：验证“data: URI base64 再导入”模式在本机 Node
// （Node 24）上可用——这是源项目全部 wwwroot/js 测试导入被测模块的统一方式
// （先 readFile，再把源码 base64 编码后经 data:text/javascript 导入，
//  以便每次运行都拿到全新模块实例，避免 ESM 缓存串扰）。
// 本测试就地生成一个极小模块文件 tests/js/fixtures/sample.js（导出一个函数），
// 用它作为验证对象；后续的真实 JS 测试将直接以相对 URL 导入
// ../../src/AtomBlazor.*/wwwroot/js 下的模块。

import assert from 'node:assert/strict';
import { test } from 'node:test';
import { mkdir, readFile, writeFile } from 'node:fs/promises';

const fixtureUrl = new URL('fixtures/sample.js', import.meta.url);

test('data: URI base64 再导入模式在当前 Node 版本可用', async () => {
    // 1. 就地生成 tiny 模块文件（内容极简：仅导出一个函数）
    const source = [
        '// 由 module-loading-harness.test.mjs 自动生成：仅用于 JS 测试基建冒烟验证',
        'export function greet(name) {',
        '    return `你好, ${name}!`;',
        '}',
        '',
    ].join('\n');
    await mkdir(new URL('fixtures/', import.meta.url), { recursive: true });
    await writeFile(fixtureUrl, source, 'utf8');

    // 2. 与源项目一致的导入路径：new URL 相对定位 -> readFile -> base64 -> data: URI 导入
    const loaded = await readFile(fixtureUrl, 'utf8');
    const mod = await import(`data:text/javascript;base64,${Buffer.from(loaded).toString('base64')}`);

    // 3. 验证导入的模块确实携带了导出函数且逻辑正确
    assert.equal(typeof mod.greet, 'function');
    assert.equal(mod.greet('Atom'), '你好, Atom!');
});

// 验证定位模块及其 vendored Floating UI 静态依赖在真实文件 URL 下可解析。
// 这里刻意不走 data: URI 再导入——那种方式会切断相对静态导入的解析基准；
// positioning.js/overlay.js 需要按 file:// 相对路径拉起 ../vendor/floating-ui-dom.esm.min.js
// 与兄弟模块，正好把 barrel 的整张静态依赖图当作导入的隐含断言。
const positioningUrl = new URL(
    '../../src/AtomBlazor.Primitives/wwwroot/js/primitives/positioning.js', import.meta.url);
const overlayUrl = new URL(
    '../../src/AtomBlazor.Primitives/wwwroot/js/primitives/overlay.js', import.meta.url);

test('positioning.js 可导入，vendored floating-ui 随静态依赖一并解析', async () => {
    const mod = await import(positioningUrl.href);

    for (const name of [
        'isElementReady', 'waitForElement', 'computePosition', 'applyPosition',
        'hidePosition', 'autoUpdate', 'setupPositioningById', 'focusById',
    ]) {
        assert.equal(typeof mod[name], 'function', `positioning.js 缺少导出 ${name}`);
    }
});

test('overlay.js 可导入，其整张兄弟模块依赖图在无 DOM 环境下求值通过', async () => {
    // overlay.js 静态导入 click-outside / escape-keydown / menu-keyboard / select /
    // positioning（含 Floating UI）/ theme-scope；任一文件顶层摸 DOM 都会让这里炸掉。
    const mod = await import(overlayUrl.href);

    for (const name of ['open', 'close', 'isOpen']) {
        assert.equal(typeof mod[name], 'function', `overlay.js 缺少导出 ${name}`);
    }
});

// 服务层③交付的 JS 模块——焦点捕获、全局快捷键、原生 dialog、滑动区域
// （含其 ../drag-updates.js 相对依赖），以真实 file URL 导入，验证模块图可解析
// 且 C# 侧点分标识符对应的导出确凿存在。
const m5Modules = [
    ['focus-trap.js', 'focusTrap', ['createFocusTrap', 'focusFirst', 'focusLast']],
    ['keyboard-shortcuts.js', 'keyboardShortcuts', ['initialize', 'registerShortcut', 'unregisterShortcut', 'dispose']],
    ['native-dialog.js', 'nativeDialog', ['supportsNativeDialog', 'showModal', 'closeDialog', 'focusDialog', 'focusElement', 'setupDialog']],
    ['swipe-area.js', 'swipeArea', ['initialize', 'cancel', 'dispose']],
];

for (const [file, _ns, exports] of m5Modules) {
    test(`${file} 可导入且导出面完整`, async () => {
        const mod = await import(new URL(`../../src/AtomBlazor.Primitives/wwwroot/js/primitives/${file}`, import.meta.url).href);

        for (const name of exports) {
            assert.equal(typeof mod[name], 'function', `${file} 缺少导出 ${name}`);
        }
    });
}

test('swipe-area.js 的相对依赖 drag-updates.js 可独立解析', async () => {
    // swipe-area.js 静态导入 ../drag-updates.js；本例同时充当该依赖存在性的隐含断言
    // （若相对路径断掉，上面的 swipe-area.js 用例已先炸）。
    const mod = await import(new URL('../../src/AtomBlazor.Primitives/wwwroot/js/drag-updates.js', import.meta.url).href);

    for (const name of ['createDragUpdates', 'releaseDragPreview', 'snapSliderValue']) {
        assert.equal(typeof mod[name], 'function', `drag-updates.js 缺少导出 ${name}`);
    }
});

// 说明：barrel（primitives.js）本身不在这里导入——它静态拉起 element-utils.js，
// 而后者在模块求值期就初始化键盘到达监听器、触碰 document，Node 无 DOM 环境必然失败
//（的 positioning/overlay 用例同样因此只按子图导入）。barrel 的完整性由
// PrimitiveJsModuleTests 以文本方式核对导入、新鲜性校验与导出清单。
