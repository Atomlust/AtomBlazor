// responsive-tabs.js 的真实文件导入与行为测试。
// 模块顶层只建 Map（零 DOM 触碰）；ResizeObserver/querySelector/clientWidth/scrollWidth
// 均在 initialize 手柄体内访问，因此可在无 DOM 的 Node 环境以 file-URL 导入；模块无相对依赖。
// C# 侧调用点为 barrel 点分标识符 responsiveTabs.initialize/remeasure/dispose
// （见 TabsTests 的 interop 载荷断言与 components-core.js 的 assertFresh 收口），
// 回调通道为 dotNetRef.invokeMethodAsync('OnOverflowChange', bool)。
// DOM 桩沿用 tree-keyboard-module.test.mjs 先例的最小元素桩；ResizeObserver 桩记录回调，
// 由测试手动触发一次“尺寸变化”。

import assert from 'node:assert/strict';
import { test, afterEach } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/responsive-tabs.js', import.meta.url);

const observers = [];
globalThis.ResizeObserver = class {
    constructor(cb) {
        this.cb = cb;
        this.observed = [];
        this.disconnected = false;
        observers.push(this);
    }
    observe(el) { this.observed.push(el); }
    disconnect() { this.disconnected = true; }
    fire() { this.cb(); }
};

function makeEl({ attrs = {}, children = [], clientWidth = 100, scrollWidth = 100 } = {}) {
    const el = {
        attrs,
        children,
        clientWidth,
        scrollWidth,
        getAttribute(name) { return attrs[name] ?? null; },
        querySelector(selector) {
            const m = selector.match(/^\[([^\]=]+)="([^"]*)"\]$/);
            for (const child of children) {
                if (m && child.getAttribute(m[1]) === m[2]) return child;
            }
            return null;
        },
    };
    return el;
}

function makeDotNet({ reject = false } = {}) {
    const calls = [];
    return {
        calls,
        invokeMethodAsync(name, ...args) {
            calls.push([name, ...args]);
            return reject ? Promise.reject(new Error('circuit down')) : Promise.resolve();
        },
    };
}

const liveIds = [];
let currentMod = null;

async function freshInitialize(container, dotNet, id) {
    currentMod ??= await import(moduleUrl.href);
    currentMod.initialize(dotNet, id, container);
    liveIds.push(id);
    return currentMod;
}

afterEach(() => {
    while (liveIds.length) currentMod.dispose(liveIds.pop());
    observers.length = 0;
});

// ------------------------------------------------------------------

test('responsive-tabs.js 可导入且导出面与 responsiveTabs. 点分标识符一一对应', async () => {
    const mod = await import(moduleUrl.href);

    // C# 侧调用点：responsiveTabs.initialize / dispose（remeasure 供后续增删标签重测）。
    for (const name of ['initialize', 'remeasure', 'dispose']) {
        assert.equal(typeof mod[name], 'function', `responsive-tabs.js 缺少导出 ${name}`);
    }
});

test('initialize 对缺失入参与无 tablist 的容器静默返回（预渲染/半程释放容错）', async () => {
    const mod = await import(moduleUrl.href);

    mod.initialize(null, 'x', makeEl());
    mod.initialize(makeDotNet(), 'x', null);
    // 容器里找不到 [role="tablist"]：不建观察器。
    mod.initialize(makeDotNet(), 'x', makeEl());
    assert.equal(observers.length, 0);

    mod.dispose('未知id'); // 不抛错即通过
    mod.remeasure('未知id');
});

test('initialize 观察容器并按初始宽度上报 OnOverflowChange', async () => {
    const tablist = makeEl({ attrs: { role: 'tablist' }, scrollWidth: 240 });
    const container = makeEl({ children: [tablist], clientWidth: 100 });
    const dotNet = makeDotNet();

    await freshInitialize(container, dotNet, 'rt-overflow');

    assert.equal(observers.length, 1);
    assert.deepEqual(observers[0].observed, [container]);
    // 自然宽 240 > 可用宽 100 → 初始检查即报 true（一次）。
    assert.deepEqual(dotNet.calls, [['OnOverflowChange', true]]);
});

test('未溢出上报 false，且状态未翻转的重复观察回调不再上报', async () => {
    const tablist = makeEl({ attrs: { role: 'tablist' }, scrollWidth: 80 });
    const container = makeEl({ children: [tablist], clientWidth: 100 });
    const dotNet = makeDotNet();

    await freshInitialize(container, dotNet, 'rt-dedupe');
    assert.deepEqual(dotNet.calls, [['OnOverflowChange', false]]);

    observers[0].fire();
    observers[0].fire();
    assert.equal(dotNet.calls.length, 1); // lastOverflowing 去重

    // 容器变窄翻转状态 → 第二次上报。
    container.clientWidth = 50;
    observers[0].fire();
    assert.deepEqual(dotNet.calls[1], ['OnOverflowChange', true]);
});

test('remeasure(id) 重读 tablist 自然宽并重置去重基线', async () => {
    const tablist = makeEl({ attrs: { role: 'tablist' }, scrollWidth: 240 });
    const container = makeEl({ children: [tablist], clientWidth: 100 });
    const dotNet = makeDotNet();

    const mod = await freshInitialize(container, dotNet, 'rt-remeasure');
    assert.deepEqual(dotNet.calls, [['OnOverflowChange', true]]);

    // 标签被摘走：自然宽缩到 60，但只有显式 remeasure 才重读 scrollWidth。
    tablist.scrollWidth = 60;
    mod.remeasure('rt-remeasure');
    assert.deepEqual(dotNet.calls[1], ['OnOverflowChange', false]);
});

test('dispose 断开观察器且状态出清，重复 dispose 幂等', async () => {
    const mod = await import(moduleUrl.href);
    const tablist = makeEl({ attrs: { role: 'tablist' }, scrollWidth: 80 });
    const container = makeEl({ children: [tablist], clientWidth: 100 });
    const dotNet = makeDotNet();

    mod.initialize(dotNet, 'rt-dispose', container);
    const observer = observers[0];
    assert.equal(observer.disconnected, false);

    mod.dispose('rt-dispose');
    assert.equal(observer.disconnected, true);

    // 状态已删：后续回调与 remeasure 都触碰不到组件引用。
    dotNet.calls.length = 0;
    observer.fire();
    mod.remeasure('rt-dispose');
    assert.deepEqual(dotNet.calls, []);

    mod.dispose('rt-dispose'); // 幂等
});

test('invokeMethodAsync 拒绝时静默吞掉（电路断开不留未处理拒绝）', async () => {
    const tablist = makeEl({ attrs: { role: 'tablist' }, scrollWidth: 240 });
    const container = makeEl({ children: [tablist], clientWidth: 100 });
    const dotNet = makeDotNet({ reject: true });

    await freshInitialize(container, dotNet, 'rt-reject');
    // 初始检查走 .catch(() => {}) 分支：同步不抛错即通过。
    assert.deepEqual(dotNet.calls, [['OnOverflowChange', true]]);
});
