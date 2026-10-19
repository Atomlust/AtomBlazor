// scroll-to-top.js 的真实文件导入与行为测试。
// 模块顶层只建 Map（零 DOM 触碰），因此可在无 DOM 的 Node 环境以 file-URL 导入。
// C# 侧调用点为 barrel 点分标识符 scrollToTop.observe/scrollToTop.scrollToTop/scrollToTop.dispose
// （见 ScrollToTopTests 的 interop 载荷断言与 components-core.js 的 assertFresh 收口）；
// 回调通道为 dotNetRef.invokeMethodAsync('JsSetVisible', next)。
// DOM 桩沿用 stepper-module.test.mjs 先例的最小实现风格：可编程的 scrollTop、
// 逐类型监听器登记与移除计数、观察器实例登记与断联计数。

import assert from 'node:assert/strict';
import { test, beforeEach } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/scroll-to-top.js', import.meta.url);

// ---- 微型 DOM 桩（模块内函数只在调用时触碰这些全局） ----
function makeEvents() {
    return {
        handlers: new Map(),
        added: [],
        removed: [],
        addEventListener(type, fn, options) {
            this.added.push({ type, fn, options });
            if (!this.handlers.has(type)) this.handlers.set(type, new Set());
            this.handlers.get(type).add(fn);
        },
        removeEventListener(type, fn) {
            this.removed.push({ type, fn });
            this.handlers.get(type)?.delete(fn);
        },
        dispatch(type) {
            for (const fn of [...(this.handlers.get(type) ?? [])]) fn();
        },
        listenerCount(type) {
            return this.handlers.get(type)?.size ?? 0;
        },
    };
}

function makeElement(scrollTop = 0) {
    return {
        scrollTop,
        isConnected: true,
        attrs: new Map(),
        focusCalls: [],
        scrollCalls: [],
        ...makeEvents(),
        getAttribute(name) { return this.attrs.has(name) ? this.attrs.get(name) : null; },
        setAttribute(name, value) { this.attrs.set(name, String(value)); },
        removeAttribute(name) { this.attrs.delete(name); },
        focus(options) { this.focusCalls.push(options); },
        scrollTo(options) { this.scrollCalls.push(options); },
    };
}

const observers = [];
class FakeMutationObserver {
    constructor(callback) {
        this.callback = callback;
        this.observed = [];
        this.disconnected = 0;
        observers.push(this);
    }
    observe(target, options) { this.observed.push({ target, options }); }
    disconnect() { this.disconnected++; }
    trigger() { this.callback([], this); }
}

let document;
let window;
let rafQueue;

function installDom({ selectorHit = null, reducedMotion = false } = {}) {
    observers.length = 0;
    rafQueue = [];
    document = {
        scrollingElement: makeElement(0),
        documentElement: {},
        activeElement: null,
        querySelector(selector) { return selector === '#panel' ? selectorHit : null; },
    };
    window = {
        ...makeEvents(),
        matchMedia: query => ({ matches: query === '(prefers-reduced-motion: reduce)' && reducedMotion }),
    };
    globalThis.document = document;
    globalThis.window = window;
    globalThis.MutationObserver = FakeMutationObserver;
    globalThis.requestAnimationFrame = fn => { rafQueue.push(fn); return rafQueue.length; };
    globalThis.cancelAnimationFrame = () => { rafQueue = []; };
}

// dotNetRef 桩：记录每次 JsSetVisible 上报。
function makeRef() {
    const calls = [];
    return {
        calls,
        invokeMethodAsync(name, value) {
            assert.equal(name, 'JsSetVisible');
            calls.push(value);
            return Promise.resolve();
        },
    };
}

async function importModule(domOptions) {
    installDom(domOptions);
    return import(moduleUrl);
}

 手动排空 rAF 队列（check 每轮重新排队，到顶后收敛）。
function pump(max = 50) {
    for (let i = 0; i < max && rafQueue.length; i++) rafQueue.shift()();
}

beforeEach(() => { installDom(); });

// ---------------------------------------------------------------- 导出面

test('导出面只含 observe/scrollToTop/dispose 三个函数', async () => {
    const mod = await importModule();
    assert.deepEqual(Object.keys(mod).sort(), ['dispose', 'observe', 'scrollToTop']);
    for (const key of ['observe', 'scrollToTop', 'dispose']) {
        assert.equal(typeof mod[key], 'function', key);
    }
});

// ---------------------------------------------------------------- observe：挂摘与去重

test('无选择器时监视文档滚动元素并把监听挂在 window 上', async () => {
    const mod = await importModule();
    const ref = makeRef();
    assert.equal(mod.observe('i1', null, 100, ref), true);
    assert.equal(document.scrollingElement.listenerCount('scroll'), 0);
    assert.equal(window.listenerCount('scroll'), 1);
    assert.deepEqual(ref.calls, [false]); // 初始 0<=100：报一次 false
});

test('选择器命中元素时监听挂在元素自身，未命中返回 false 且不留监听', async () => {
    const hit = makeElement(0);
    const mod = await importModule({ selectorHit: hit });
    const ref = makeRef();
    assert.equal(mod.observe('i2', '#panel', 50, ref), true);
    assert.equal(hit.listenerCount('scroll'), 1);
    assert.equal(window.listenerCount('scroll'), 0);

    const mod2 = await importModule({ selectorHit: null });
    assert.equal(mod2.observe('i3', '#missing-panel', 50, makeRef()), false);
    assert.equal(window.listenerCount('scroll'), 0);
});

test('阈值上报按状态去重：越过与退回各报一次，中途滚动不重复报', async () => {
    const hit = makeElement(0);
    const mod = await importModule({ selectorHit: hit });
    const ref = makeRef();
    mod.observe('i4', '#panel', 100, ref);
    assert.deepEqual(ref.calls, [false]);

    hit.scrollTop = 150;
    hit.dispatch('scroll');
    hit.dispatch('scroll'); // 状态未变：不得重复上报
    hit.scrollTop = 200;
    hit.dispatch('scroll');
    assert.deepEqual(ref.calls, [false, true]);

    hit.scrollTop = 20;
    hit.dispatch('scroll');
    assert.deepEqual(ref.calls, [false, true, false]);
});

test('观察器挂在 documentElement 上且 childList+subtree', async () => {
    const mod = await importModule();
    mod.observe('i5', null, 100, makeRef());
    assert.equal(observers.length, 1);
    assert.equal(observers[0].observed[0].target, document.documentElement);
    assert.deepEqual(observers[0].observed[0].options, { childList: true, subtree: true });
});

test('同 id 重挂 observe 应先摘旧挂新（dispose 复用）', async () => {
    const first = makeElement(0);
    const mod = await importModule({ selectorHit: first });
    mod.observe('i6', '#panel', 100, makeRef());
    assert.equal(first.listenerCount('scroll'), 1);

    const second = makeElement(0);
    const mod2 = await importModule({ selectorHit: second });
    // 注意：重新导入命中模块缓存，watchers Map 跨段保留——重挂同一 id 才走 dispose 旧路。
    const ref = makeRef();
    mod2.observe('i6', '#panel', 100, ref);
    assert.equal(observers.at(-1).disconnected, 0); // 新观察器本身未断
    assert.equal(second.listenerCount('scroll'), 1);
    assert.deepEqual(ref.calls, [false]);
});

test('dispose 应断观察器并摘滚动监听，未知 id 幂等静默', async () => {
    const hit = makeElement(0);
    const mod = await importModule({ selectorHit: hit });
    mod.observe('i7', '#panel', 100, makeRef());
    const created = observers[0];

    mod.dispose('i7');
    assert.equal(created.disconnected, 1);
    assert.equal(hit.listenerCount('scroll'), 0);
    assert.equal(hit.removed.filter(entry => entry.type === 'scroll').length, 1);

    mod.dispose('i7'); // 再来一次：Map 已空，不得抛
    mod.dispose('未知-id');
});

test('面板迟到出现时 MutationObserver 同步应补挂监听并按新目标上报', async () => {
    const mod = await importModule({ selectorHit: null });
    const ref = makeRef();
    assert.equal(mod.observe('i8', '#panel', 10, ref), false);
    assert.deepEqual(ref.calls, [false]);

    const late = makeElement(42);
    installDomKeepModule(mod, late);
    observers[0].trigger(); // 变异回调触发 sync：重新 querySelector
    assert.equal(late.listenerCount('scroll'), 1);
    assert.deepEqual(ref.calls, [false, true]); // 42>10：越阈值上报 true
});

// installDom 会换掉全局 document；此处只替换 querySelector 的命中目标，保留模块闭包一致性。
function installDomKeepModule(_mod, element) {
    document.querySelector = selector => (selector === '#panel' ? element : null);
    document.scrollingElement = element;
}

// ---------------------------------------------------------------- scrollToTop

test('scrollToTop 未命中目标直接返回 false', async () => {
    const mod = await importModule({ selectorHit: null });
    assert.equal(await mod.scrollToTop('#panel', true), false);
});

test('scrollToTop 平滑请求与 reduced-motion 回退 auto', async () => {
    const hit = makeElement(120);
    const mod = await importModule({ selectorHit: hit });
    const pending = mod.scrollToTop('#panel', true);
    assert.deepEqual(hit.scrollCalls[0], { top: 0, behavior: 'smooth' });
    hit.scrollTop = 0;
    pump();
    assert.equal(await pending, true);  check 到顶收敛

    const calm = makeElement(120);
    const mod2 = await importModule({ selectorHit: calm, reducedMotion: true });
    const pending2 = mod2.scrollToTop('#panel', true);
    assert.deepEqual(calm.scrollCalls[0], { top: 0, behavior: 'auto' });
    calm.scrollTop = 0;
    pump();
    assert.equal(await pending2, true);
});

test('scrollToTop 以 scrollend 收敛，迟到的旧 scrollend 不得误判', async () => {
    const hit = makeElement(80);
    const mod = await importModule({ selectorHit: hit });
    const pending = mod.scrollToTop('#panel', false);
    hit.scrollTop = 40;  scrollend 到达但尚未到顶：end 判定不收敛
    hit.dispatch('scrollend');
    rafQueue.shift()?.();  check：40>0 且仍在连接上，重新排队
    assert.equal(rafQueue.length, 1);
    hit.scrollTop = 0;
    rafQueue.shift()?.();  check：到顶收敛
    assert.equal(await pending, true);
    assert.equal(hit.listenerCount('scrollend'), 0); // 收敛后摘掉 scrollend 监听
});

test('键盘激活时先移动焦点到目标再滚动，tabindex 原样恢复或移除', async () => {
    const hit = makeElement(60);
    hit.attrs.set('tabindex', '2');
    const mod = await importModule({ selectorHit: hit });
    document.activeElement = {
        closest: selector => (selector === '[data-slot="scroll-to-top"]' ? {} : null),
    };
    const pending = mod.scrollToTop('#panel', true);
    assert.deepEqual(hit.focusCalls, [{ preventScroll: true }]);
    assert.equal(hit.attrs.get('tabindex'), '2'); // 恢复原值
    hit.scrollTop = 0;
    pump();
    assert.equal(await pending, true);

    const bare = makeElement(60);
    const mod2 = await importModule({ selectorHit: bare });
    document.activeElement = {
        closest: selector => (selector === '[data-slot="scroll-to-top"]' ? {} : null),
    };
    const pending2 = mod2.scrollToTop('#panel', true);
    assert.equal(bare.attrs.has('tabindex'), false); // 原无 tabindex：用完移除
    bare.scrollTop = 0;
    pump();
    assert.equal(await pending2, true);
});
