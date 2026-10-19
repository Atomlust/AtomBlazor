// responsive-nav.js 的真实文件导入与行为测试。
// 模块顶层零 DOM 触碰（仅常量与两个模块级变量），window/document/ResizeObserver
// 均在函数体内引用，可在无 DOM 的 Node 环境以 file-URL 导入；模块无相对依赖。
// C# 侧调用点为 barrel 点分标识符 responsiveNav.initialize / responsiveNav.cleanup
// （ResponsiveNavProvider.razor.cs，照 / 共享桶先例经 ComponentModules.GetCoreAsync
// 装载 components-core.js）；本波无 JS 返回句柄的清理调用面，"句柄不点分"口径零命中，
// cleanup 系模块级导出、随点分口径收口（照 scrollToTop.dispose 先例）。
// DOM 桩沿用 menu-triggers/scroll-area 先例的最小桩：可编程 window.innerWidth、
// document.body 哨兵对象与记录回调/断联的 ResizeObserver 桩。
// 不可测项登记：真实 resize 事件时序与浏览器 innerWidth 语义在纯桩环境下只核对
// 调用面（手动重放观察器回调），不伪造浏览器级断言；模块级 dotNetRef/resizeObserver
// 为共享状态，各测试按序 initialize→cleanup 复位。

import assert from 'node:assert/strict';
import { test } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/responsive-nav.js', import.meta.url);

const bodySentinel = { tag: 'body' };
globalThis.document = { body: bodySentinel };
globalThis.window = { innerWidth: 1024 };

// ResizeObserver 桩：记录回调、观察目标与断联标记。
const observers = [];
globalThis.ResizeObserver = class {
    constructor(cb) {
        this.cb = cb;
        this.targets = [];
        this.disconnected = false;
        observers.push(this);
    }
    observe(target) { this.targets.push(target); }
    disconnect() { this.disconnected = true; }
    fire() { this.cb(); }
};

// DotNet 引用桩：记录 invokeMethodAsync 的 (方法名, 载荷)。
function makeRef() {
    return {
        calls: [],
        invokeMethodAsync(name, value) {
            this.calls.push([name, value]);
            return Promise.resolve();
        },
    };
}

const nav = await import(moduleUrl.href);

test('模块导出面：initialize 与 cleanup 两枚函数，无其他导出', () => {
    assert.equal(typeof nav.initialize, 'function');
    assert.equal(typeof nav.cleanup, 'function');
    assert.deepEqual(Object.keys(nav).sort(), ['cleanup', 'initialize']);
});

test('initialize 立即上报桌面态且回调名为 OnMobileChange', () => {
    globalThis.window.innerWidth = 1024;
    const ref = makeRef();
    nav.initialize(ref);

    assert.deepEqual(ref.calls, [['OnMobileChange', false]]);
    nav.cleanup();
});

test('窄视口 initialize 上报移动端态', () => {
    globalThis.window.innerWidth = 500;
    const ref = makeRef();
    nav.initialize(ref);

    assert.deepEqual(ref.calls, [['OnMobileChange', true]]);
    nav.cleanup();
});

test('断点 768 属严格小于语义：恰 768 不算移动端', () => {
    globalThis.window.innerWidth = 768;
    const ref = makeRef();
    nav.initialize(ref);
    assert.deepEqual(ref.calls, [['OnMobileChange', false]]); // MOBILE_BREAKPOINT=768，<768 才为移动端
    nav.cleanup();
});

test('initialize 构造 ResizeObserver 并观察 document.body', () => {
    const before = observers.length;
    const ref = makeRef();
    nav.initialize(ref);

    assert.equal(observers.length, before + 1);
    const observer = observers.at(-1);
    assert.deepEqual(observer.targets, [bodySentinel]);
    nav.cleanup();
});

test('观察器回调重放按新宽度二次上报', () => {
    globalThis.window.innerWidth = 1024;
    const ref = makeRef();
    nav.initialize(ref);
    const observer = observers.at(-1);

    globalThis.window.innerWidth = 400;
    observer.fire();
    assert.deepEqual(ref.calls, [['OnMobileChange', false], ['OnMobileChange', true]]);
    nav.cleanup();
});

test('cleanup 断联观察器并摘除引用', () => {
    const ref = makeRef();
    nav.initialize(ref);
    const observer = observers.at(-1);

    nav.cleanup();
    assert.equal(observer.disconnected, true);
});

test('重复 initialize 替换回调引用且只更新模块级 ref', () => {
    globalThis.window.innerWidth = 1024;
    const first = makeRef();
    nav.initialize(first);
    const firstObserver = observers.at(-1);
    const second = makeRef();
    nav.initialize(second);
    assert.equal(second.calls.length, 1); // 重装载即再报一次初始检查

    const latest = observers.at(-1);
    latest.fire();
    assert.equal(second.calls.length, 2); // 后续事件流向新引用
    assert.equal(first.calls.length, 1);  // 旧引用停在初报
    // 怪癖照源锁存：重装载直接覆盖 resizeObserver 模块变量，前一枚观察器不断联；
    // cleanup 只断联最新一枚。
    assert.equal(firstObserver.disconnected, false);
    nav.cleanup();
    assert.equal(latest.disconnected, true);
    assert.equal(firstObserver.disconnected, false);
});

test('cleanup 幂等：连续两次调用与无 initialize 场景均静默', () => {
    const ref = makeRef();
    nav.initialize(ref);
    nav.cleanup();
    nav.cleanup(); // resizeObserver 已置 null：跳过断联分支，不抛出
});

test('components-core.js barrel 已收口 responsiveNav 命名空间', async () => {
    const { readFile } = await import('node:fs/promises');
    const core = new URL('../../src/AtomBlazor.Components/wwwroot/js/components-core.js', import.meta.url);
    const text = await readFile(core, 'utf8');
    assert.match(text, /^import \* as responsiveNav from '\.\/responsive-nav\.js\?assets=1';$/m);
    assert.match(text, /^assertFresh\('responsive-nav\.js', responsiveNav, 'initialize'\);$/m);
    assert.match(text, /^export \{[\s\S]*\bresponsiveNav,[\s\S]*\};$/m);
});
