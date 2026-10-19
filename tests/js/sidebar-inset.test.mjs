// sidebar-inset.js 的真实文件导入与行为测试。
// 模块仅一枚导出 scrollToTop：同滚元素与窗口双通道（覆盖 h-screen 元素级滚动与
// min-h-screen 文档级滚动两种布局）。顶层零 DOM 触碰，window 仅在函数体内引用，
// 可在无 DOM 的 Node 环境以 file-URL 导入；模块无依赖。
// C# 侧调用点为 barrel 点分标识符 sidebarInset.scrollToTop（SidebarInset.razor.cs，
// 经 components-core.js 装载；桶接线断言已随 sidebar.test.mjs 收口，本测不重复）。
// DOM 桩沿用 menu-triggers/responsive-nav 先例最小口径：记录实参的 window.scrollTo
// 与元素 scrollTo 哨兵。不可测项登记：真实滚动语义与 smooth 行为在纯桩环境下
// 只核对调用面，不伪造浏览器级断言。

import assert from 'node:assert/strict';
import { test } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/sidebar-inset.js', import.meta.url);

// window 桩：记录 scrollTo 实参序列。
const windowCalls = [];
globalThis.window = {
    scrollTo(...args) { windowCalls.push(args); },
};

// 元素桩：记录 scrollTo 实参序列；null 沿以裸 undefined/null 传入核对。
function makeEl() {
    const calls = [];
    return { calls, scrollTo(...args) { calls.push(args); } };
}

const inset = await import(moduleUrl.href);

test('导出面：scrollToTop 单枚函数照源面', () => {
    assert.equal(typeof inset.scrollToTop, 'function');
    assert.deepEqual(Object.keys(inset), ['scrollToTop']);
});

test('给元素：元素与窗口双通道同滚顶部，实参皆 (0,0)', () => {
    const el = makeEl();
    windowCalls.length = 0;
    inset.scrollToTop(el);
    assert.deepEqual(el.calls, [[0, 0]], '元素 scrollTo(0,0) 一发');
    assert.deepEqual(windowCalls, [[0, 0]], '窗口 scrollTo(0,0) 同发');
});

test('元素为 null：跳过元素通道仍滚窗口（两种布局形态并立口径）', () => {
    windowCalls.length = 0;
    inset.scrollToTop(null);
    assert.deepEqual(windowCalls, [[0, 0]], '窗口滚动不受元素缺失牵连');
});

test('元素缺省（undefined）：同 null 沿静默不抛', () => {
    windowCalls.length = 0;
    inset.scrollToTop();
    assert.deepEqual(windowCalls, [[0, 0]]);
});
