// stepper.js 的真实文件导入与行为测试。
// 模块顶层只建 WeakMap（零 DOM 触碰）；MutationObserver、querySelectorAll、closest、dataset
// 仅在函数体内访问，因此可在无 DOM 的 Node 环境以 file-URL 导入；模块无相对依赖。
// C# 侧调用点为 barrel 点分标识符 stepper.initialize/stepper.dispose
// （见 StepperTests 的 interop 载荷断言与 components-core.js 的 assertFresh 收口）；
// 回调通道为 dotNetRef.invokeMethodAsync('SynchronizeOrder', ids)。
// DOM 桩沿用 tabs-reorder-module.test.mjs 先例的最小实现风格：观察器实例登记与断联计数、
// 微型 closest 归属判定与可编程的子元素序列。

import assert from 'node:assert/strict';
import { test, afterEach } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/stepper.js', import.meta.url);

// MutationObserver 桩：构造即登记，observe/disconnect 计数，trigger 手动派发变异回调。
const created = [];
class FakeMutationObserver {
    constructor(callback) {
        this.callback = callback;
        this.observed = [];
        this.disconnected = 0;
        created.push(this);
    }
    observe(target, options) { this.observed.push({ target, options }); }
    disconnect() { this.disconnected++; }
    trigger() { this.callback([], this); }
}

const originalObserver = globalThis.MutationObserver;
globalThis.MutationObserver = FakeMutationObserver;

afterEach(() => {
    created.length = 0;
});

// 步骤锚点桩：dataset.stepId 供收集，closest 由归属指针决定——
// 只有 closest('[data-slot="stepper"]') 恰为 root 本身的元素才算直属步骤。
function makeStep(id, owner) {
    return {
        dataset: { stepId: id },
        closest(selector) {
            assert.equal(selector, '[data-slot="stepper"]');
            return owner;
        },
    };
}

function makeRoot() {
    const root = {
        children: [],
        querySelectorAll(selector) {
            assert.equal(selector, '[data-step-id]');
            return [...root.children];
        },
    };
    return root;
}

// dotNetRef 桩：记录每次上报；rejecting 变体模拟电路断开时的失败承诺。
function makeDotNet({ reject = false } = {}) {
    return {
        reports: [],
        invokeMethodAsync(name, ids) {
            this.reports.push([name, [...ids]]);
            return reject ? Promise.reject(new Error('circuit down')) : Promise.resolve();
        },
    };
}

// ------------------------------------------------------------------

test('导出面：initialize 与 dispose 是函数', async () => {
    const stepper = await import(moduleUrl);
    assert.equal(typeof stepper.initialize, 'function');
    assert.equal(typeof stepper.dispose, 'function');
});

test('initialize 挂 childList+subtree 观察器并立即上报直属步骤 id', async () => {
    const stepper = await import(moduleUrl);
    const root = makeRoot();
    root.children = [makeStep('a', root), makeStep('b', root)];
    const dotNet = makeDotNet();

    stepper.initialize(root, dotNet);

    assert.equal(created.length, 1);
    assert.deepEqual(created[0].observed[0], { target: root, options: { childList: true, subtree: true } });
    assert.deepEqual(dotNet.reports, [['SynchronizeOrder', ['a', 'b']]]);
});

test('仅归属本 stepper 的 data-step-id 上报，嵌套 stepper 的步骤被剔除', async () => {
    const stepper = await import(moduleUrl);
    const root = makeRoot();
    const inner = makeRoot();
    root.children = [makeStep('a', root), makeStep('n', inner), makeStep('b', root)];
    const dotNet = makeDotNet();

    stepper.initialize(root, dotNet);

    assert.deepEqual(dotNet.reports, [['SynchronizeOrder', ['a', 'b']]]);
});

test('相同顺序的变异经去重键不二次上报', async () => {
    const stepper = await import(moduleUrl);
    const root = makeRoot();
    root.children = [makeStep('a', root), makeStep('b', root)];
    const dotNet = makeDotNet();

    stepper.initialize(root, dotNet);
    created[0].trigger();
    created[0].trigger();

    assert.equal(dotNet.reports.length, 1);
});

test('顺序或成员变化的变异以新顺序再上报', async () => {
    const stepper = await import(moduleUrl);
    const root = makeRoot();
    const a = makeStep('a', root);
    const b = makeStep('b', root);
    root.children = [a, b];
    const dotNet = makeDotNet();

    stepper.initialize(root, dotNet);

    // 互换顺序（模拟 DOM 重排）后派发变异。
    root.children = [b, a];
    created[0].trigger();
    assert.deepEqual(dotNet.reports[1], ['SynchronizeOrder', ['b', 'a']]);

    // 成员减少同样触发上报。
    root.children = [b];
    created[0].trigger();
    assert.deepEqual(dotNet.reports[2], ['SynchronizeOrder', ['b']]);
});

test('initialize 重复调用先断联旧观察器再挂新的', async () => {
    const stepper = await import(moduleUrl);
    const root = makeRoot();
    root.children = [makeStep('a', root)];

    stepper.initialize(root, makeDotNet());
    stepper.initialize(root, makeDotNet());

    assert.equal(created.length, 2);
    assert.equal(created[0].disconnected, 1);
    assert.equal(created[1].disconnected, 0);
});

test('dispose 断联观察器并清除登记', async () => {
    const stepper = await import(moduleUrl);
    const root = makeRoot();
    const dotNet = makeDotNet();

    stepper.initialize(root, dotNet);
    stepper.dispose(root);

    assert.equal(created[0].disconnected, 1);
    // 清除后再初始化：旧观察器不会再被 WeakMap 命中，新观察器照常挂上。
    stepper.initialize(root, dotNet);
    assert.equal(created.length, 2);
    assert.equal(created[1].disconnected, 0);
});

test('dispose 幂等：双重释放与未初始化 root 均不抛', async () => {
    const stepper = await import(moduleUrl);
    const root = makeRoot();

    stepper.initialize(root, makeDotNet());
    stepper.dispose(root);
    assert.doesNotThrow(() => stepper.dispose(root));
    assert.doesNotThrow(() => stepper.dispose(makeRoot()));
});

test('上报承诺被拒时静默吞掉，不产生未处理拒绝', async () => {
    const stepper = await import(moduleUrl);
    const root = makeRoot();
    root.children = [makeStep('a', root)];
    const dotNet = makeDotNet({ reject: true });

    const seen = [];
    const onRejection = reason => seen.push(reason);
    process.on('unhandledRejection', onRejection);
    try {
        assert.doesNotThrow(() => stepper.initialize(root, dotNet));
        root.children = [];
        created[0].trigger();
        await new Promise(resolve => setTimeout(resolve, 0));
    } finally {
        process.off('unhandledRejection', onRejection);
    }

    assert.deepEqual(seen, []);
});
