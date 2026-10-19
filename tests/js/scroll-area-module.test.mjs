// scroll-area.js 的真实文件导入与行为测试。
// 模块顶层零 DOM 触碰（只有 instances Map），document/ResizeObserver/requestAnimationFrame
// 仅在函数体内引用，因此可在无 DOM 的 Node 环境以 file-URL 导入；模块无相对依赖。
// C# 侧调用点为 barrel 点分标识符 scrollArea.initialize/scrollArea.dispose、
// scrollArea.getViewportElement（ScrollAreaScrollbar 基元挂载与释放，见 PrimitiveScrollAreaTests）。
// DOM 桩沿用 keyboard-nav/tree-keyboard 先例的最小元素桩（按类型监听登记 + 微型选择器匹配），
// 并补 style/getBoundingClientRect/scrollTo/pointer capture/ResizeObserver/rAF 记录桩。
// 不可测项登记：真实 ResizeObserver 尺寸事件、smooth 滚动动画与被动监听的原生行为语义
// 在纯桩环境下只核对调用面，不伪造浏览器级断言。

import assert from 'node:assert/strict';
import { test } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Primitives/wwwroot/js/primitives/scroll-area.js', import.meta.url);

// 微型选择器匹配：仅 [attr] 裸特性形态（本模块只用 [data-scrollarea-thumb] 一枚选择器）。
function matchSimple(el, part) {
    const m = part.match(/^\s*\[([\w-]+)\]\s*$/);
    if (!m) return false;
    return el.getAttribute(m[1]) !== null;
}

function makeEl({ attrs = {}, children = [], size = {} } = {}) {
    const handlers = new Map();
    const captures = [];
    const el = {
        attrs,
        handlers,
        children,
        captures,
        firstElementChild: children[0] ?? null,
        style: {},
        scrollCalls: [],
        ...size,
        getAttribute(name) { return attrs[name] ?? null; },
        setAttribute(name, value) { attrs[name] = String(value); },
        contains(node) {
            for (const child of el.children) {
                if (child === node || child.contains(node)) return true;
            }
            return false;
        },
        addEventListener(type, fn) {
            if (!handlers.has(type)) handlers.set(type, []);
            handlers.get(type).push(fn);
        },
        removeEventListener(type, fn) {
            const list = handlers.get(type) ?? [];
            const index = list.indexOf(fn);
            if (index !== -1) list.splice(index, 1);
        },
        dispatch(type, event = {}) {
            for (const fn of [...(handlers.get(type) ?? [])]) fn(event);
        },
        count(type) { return (handlers.get(type) ?? []).length; },
        querySelector(selector) {
            const parts = selector.split(',').map(p => p.trim());
            for (const child of el.children) {
                if (parts.some(part => matchSimple(child, part))) return child;
            }
            return null;
        },
        getBoundingClientRect() {
            return {
                top: el.rectTop ?? 0,
                left: el.rectLeft ?? 0,
                height: el.rectHeight ?? el.clientHeight ?? 0,
                width: el.rectWidth ?? el.clientWidth ?? 0,
            };
        },
        scrollTo(options) { el.scrollCalls.push(options); },
        setPointerCapture(pointerId) { captures.push(pointerId); },
        releasePointerCapture(pointerId) { captures.push(`release:${pointerId}`); },
    };
    return el;
}

// 环境桩：document / ResizeObserver / requestAnimationFrame（手动泵推进）。
function installEnv() {
    const frames = [];
    const observers = [];
    const byId = new Map();
    globalThis.document = {
        body: { style: {} },
        getElementById: id => byId.get(id) ?? null,
    };
    globalThis.ResizeObserver = class {
        constructor(cb) { this.cb = cb; this.observed = []; observers.push(this); }
        observe(el) { this.observed.push(el); }
        disconnect() { this.disconnected = true; }
    };
    globalThis.requestAnimationFrame = cb => { frames.push(cb); return frames.length; };
    return {
        byId,
        observers,
        pump() { const queued = frames.splice(0); for (const cb of queued) cb(); },
    };
}

function pointerEvent(overrides = {}) {
    const event = { preventDefaultCalls: 0, stopPropagationCalls: 0, ...overrides };
    event.preventDefault = () => { event.preventDefaultCalls++; };
    event.stopPropagation = () => { event.stopPropagationCalls++; };
    return event;
}

// 纵向下 1000/500 溢出树：viewport 内容 1000、可视 500，轨道 500，内含一枚 thumb。
function makeVertical() {
    const thumb = makeEl({ attrs: { 'data-scrollarea-thumb': '' } });
    const scrollbar = makeEl({ size: { clientHeight: 500, clientWidth: 0 }, children: [thumb] });
    const inner = makeEl();
    const viewport = makeEl({
        size: { scrollHeight: 1000, clientHeight: 500, scrollTop: 0, scrollWidth: 0, clientWidth: 0, scrollLeft: 0 },
        children: [inner],
    });
    return { viewport, scrollbar, thumb };
}

function makeDotNetRef() {
    const calls = [];
    return {
        calls,
        invokeMethodAsync(name, ...args) {
            calls.push([name, ...args]);
            return Promise.resolve();
        },
    };
}

// ------------------------------------------------------------------

test('scroll-area.js 可导入且导出面覆盖 C# 点分标识符三件套', async () => {
    const mod = await import(moduleUrl.href);
    for (const name of ['initialize', 'dispose', 'getViewportElement']) {
        assert.equal(typeof mod[name], 'function', `scroll-area.js 缺少导出 ${name}`);
    }
});

test('getViewportElement 按 document.getElementById 取元素，缺失静默返回 null', async () => {
    const mod = await import(moduleUrl.href);
    const env = installEnv();
    const element = makeEl();
    env.byId.set('atom-scrollarea-viewport-1', element);

    assert.equal(mod.getViewportElement('atom-scrollarea-viewport-1'), element);
    assert.equal(mod.getViewportElement('missing-id'), null); // 缺失静默，不抛错
});

test('initialize 对缺失 viewport/scrollbar/dotNetRef 静默返回不挂载', async () => {
    const mod = await import(moduleUrl.href);
    installEnv();
    const { viewport, scrollbar, thumb } = makeVertical();
    const dotNetRef = makeDotNetRef();

    mod.initialize('a', null, scrollbar, dotNetRef, {});
    mod.initialize('b', viewport, null, dotNetRef, {});
    mod.initialize('c', viewport, scrollbar, null, {});

    assert.equal(viewport.count('scroll'), 0);
    assert.equal(scrollbar.count('pointerdown'), 0);
    assert.equal(thumb.count('pointerdown'), 0);
    assert.equal(dotNetRef.calls.length, 0);
    mod.dispose('a'); // 未注册的实例释放同样静默
});

test('initialize 挂监听全集并观察 viewport 与首子元素', async () => {
    const mod = await import(moduleUrl.href);
    const env = installEnv();
    const { viewport, scrollbar, thumb } = makeVertical();
    const dotNetRef = makeDotNetRef();

    mod.initialize('i1', viewport, scrollbar, dotNetRef, { orientation: 'vertical' });

    assert.equal(viewport.count('scroll'), 1);
    assert.equal(scrollbar.count('pointerdown'), 1);
    assert.equal(thumb.count('pointerdown'), 1);
    assert.equal(thumb.count('pointermove'), 1);
    assert.equal(thumb.count('pointerup'), 1);
    assert.equal(thumb.count('pointercancel'), 1);
    assert.equal(env.observers.length, 1);
    assert.deepEqual(env.observers[0].observed, [viewport, viewport.children[0]]);

    mod.dispose('i1');
});

test('初始 update：无溢出落 hidden 态并以 0 上报，不动滑块尺寸', async () => {
    const mod = await import(moduleUrl.href);
    installEnv();
    const { viewport, scrollbar, thumb } = makeVertical();
    viewport.scrollHeight = 500; // 与 clientHeight 等值：无溢出
    const dotNetRef = makeDotNetRef();

    mod.initialize('i2', viewport, scrollbar, dotNetRef, { orientation: 'vertical' });

    assert.deepEqual(dotNetRef.calls, [['JsUpdateScrollState', 0, 'hidden']]);
    assert.equal(thumb.getAttribute('data-state'), 'hidden');
    assert.equal(thumb.style.height, undefined);
    mod.dispose('i2');
});

test('初始 update：纵向溢出计算滑块高度/位移并按百分比与 visible 上报', async () => {
    const mod = await import(moduleUrl.href);
    installEnv();
    const { viewport, scrollbar, thumb } = makeVertical();
    viewport.scrollTop = 250;
    const dotNetRef = makeDotNetRef();

    // 内容 1000 / 可视 500、轨道 500：thumb=250px；(250/500)*(500-250)=125px；百分比 50。
    mod.initialize('i3', viewport, scrollbar, dotNetRef, { orientation: 'vertical' });

    assert.equal(thumb.style.height, '250px');
    assert.equal(thumb.style.transform, 'translate3d(0, 125px, 0)');
    assert.equal(thumb.getAttribute('data-state'), 'visible');
    assert.deepEqual(dotNetRef.calls, [['JsUpdateScrollState', 50, 'visible']]);
    mod.dispose('i3');
});

test('横向朝向按宽维度计算滑块并沿 x 位移', async () => {
    const mod = await import(moduleUrl.href);
    installEnv();
    const thumb = makeEl({ attrs: { 'data-scrollarea-thumb': '' } });
    const scrollbar = makeEl({ size: { clientWidth: 400, clientHeight: 0 }, children: [thumb] });
    const viewport = makeEl({
        size: { scrollWidth: 800, clientWidth: 400, scrollLeft: 400, scrollHeight: 0, clientHeight: 0, scrollTop: 0 },
    });
    const dotNetRef = makeDotNetRef();

    // 内容 800 / 可视 400、轨道 400：thumb=200px；(400/400)*(400-200)=200px；百分比 100。
    mod.initialize('i4', viewport, scrollbar, dotNetRef, { orientation: 'horizontal' });

    assert.equal(thumb.style.width, '200px');
    assert.equal(thumb.style.transform, 'translate3d(200px, 0, 0)');
    assert.equal(thumb.style.height, undefined);
    assert.deepEqual(dotNetRef.calls, [['JsUpdateScrollState', 100, 'visible']]);
    mod.dispose('i4');
});

test('scroll 事件经 rAF 延迟推进后重新上报', async () => {
    const mod = await import(moduleUrl.href);
    const env = installEnv();
    const { viewport, scrollbar, thumb } = makeVertical();
    const dotNetRef = makeDotNetRef();

    mod.initialize('i5', viewport, scrollbar, dotNetRef, { orientation: 'vertical' });
    assert.equal(dotNetRef.calls.length, 1); // 初始 update 直调，不经 rAF

    viewport.scrollTop = 500; // maxScroll=500 → 百分比 100
    viewport.dispatch('scroll', {});
    assert.equal(dotNetRef.calls.length, 1); // 尚未推进帧：不重复上报
    env.pump();
    assert.equal(dotNetRef.calls.length, 2);
    assert.deepEqual(dotNetRef.calls[1], ['JsUpdateScrollState', 100, 'visible']);
    mod.dispose('i5');
});

test('ResizeObserver 回调经 rAF 重算，轨道点击按比例 scrollTo 跳转', async () => {
    const mod = await import(moduleUrl.href);
    const env = installEnv();
    const { viewport, scrollbar, thumb } = makeVertical();
    scrollbar.rectHeight = 500;
    const dotNetRef = makeDotNetRef();

    mod.initialize('i6', viewport, scrollbar, dotNetRef, { orientation: 'vertical' });

    // 轨道空白处点击：clientY 100 / 轨道高 500 → ratio 0.2 × maxScroll 500 = 100。
    const event = pointerEvent({ clientY: 100, target: scrollbar, pointerId: 1 });
    scrollbar.dispatch('pointerdown', event);
    assert.equal(event.preventDefaultCalls, 1);
    assert.deepEqual(viewport.scrollCalls, [{ top: 100, behavior: 'smooth' }]);

    // 点在滑块上：不跳转。
    const onThumb = pointerEvent({ clientY: 300, target: thumb, pointerId: 2 });
    scrollbar.dispatch('pointerdown', onThumb);
    assert.equal(viewport.scrollCalls.length, 1);
    assert.equal(onThumb.preventDefaultCalls, 0);

    // 观察器回调推进一帧即重算上报。
    env.observers[0].cb();
    env.pump();
    assert.equal(dotNetRef.calls.length, 2);
    mod.dispose('i6');
});

test('无溢出时轨道点击不跳转（maxScroll 钳制早退）', async () => {
    const mod = await import(moduleUrl.href);
    installEnv();
    const { viewport, scrollbar, thumb } = makeVertical();
    viewport.scrollHeight = 500;
    const dotNetRef = makeDotNetRef();

    mod.initialize('i7', viewport, scrollbar, dotNetRef, { orientation: 'vertical' });

    scrollbar.dispatch('pointerdown', pointerEvent({ clientY: 100, target: scrollbar, pointerId: 1 }));
    assert.deepEqual(viewport.scrollCalls, []);
    mod.dispose('i7');
});

test('滑块拖拽：按下捕获指针并锁文选，移动按比例滚动，抬起释放复位', async () => {
    const mod = await import(moduleUrl.href);
    installEnv();
    const { viewport, scrollbar, thumb } = makeVertical();
    viewport.scrollTop = 100;
    const dotNetRef = makeDotNetRef();

    mod.initialize('i8', viewport, scrollbar, dotNetRef, { orientation: 'vertical' });

    const down = pointerEvent({ pointerId: 7, clientY: 300 });
    thumb.dispatch('pointerdown', down);
    assert.equal(down.preventDefaultCalls, 1);
    assert.equal(down.stopPropagationCalls, 1);
    assert.deepEqual(thumb.captures, [7]);
    assert.equal(globalThis.document.body.style.userSelect, 'none');
    assert.equal(globalThis.document.body.style.cursor, 'grabbing');

    // 再按一次：拖拽中被忽略。
    const second = pointerEvent({ pointerId: 8, clientY: 400 });
    thumb.dispatch('pointerdown', second);
    assert.equal(second.preventDefaultCalls, 0);

    // 下移 40px：scrollableTrack=500-250=250 → 滚动增量 (40/250)*500=80 → 起点 100+80。
    thumb.dispatch('pointermove', pointerEvent({ pointerId: 7, clientY: 340 }));
    assert.deepEqual(viewport.scrollCalls, [{ top: 180 }]);

    // 其它指针的 move 被忽略。
    thumb.dispatch('pointermove', pointerEvent({ pointerId: 99, clientY: 500 }));
    assert.equal(viewport.scrollCalls.length, 1);

    thumb.dispatch('pointerup', pointerEvent({ pointerId: 7 }));
    assert.deepEqual(thumb.captures, [7, 'release:7']);
    assert.equal(globalThis.document.body.style.userSelect, '');
    assert.equal(globalThis.document.body.style.cursor, '');

    // cancel 路径：重新按下后取消同样复位。
    thumb.dispatch('pointerdown', pointerEvent({ pointerId: 5, clientY: 300 }));
    thumb.dispatch('pointercancel', pointerEvent({ pointerId: 5 }));
    assert.equal(globalThis.document.body.style.cursor, '');
    mod.dispose('i8');
});

test('dispose 摘除全部监听并断联观察器，实例移除后重放为静默', async () => {
    const mod = await import(moduleUrl.href);
    const env = installEnv();
    const { viewport, scrollbar, thumb } = makeVertical();
    const dotNetRef = makeDotNetRef();

    mod.initialize('i9', viewport, scrollbar, dotNetRef, { orientation: 'vertical' });
    mod.dispose('i9');

    assert.equal(viewport.count('scroll'), 0);
    assert.equal(scrollbar.count('pointerdown'), 0);
    assert.equal(thumb.count('pointerdown'), 0);
    assert.equal(thumb.count('pointermove'), 0);
    assert.equal(thumb.count('pointerup'), 0);
    assert.equal(thumb.count('pointercancel'), 0);
    assert.equal(env.observers[0].disconnected, true);

    const before = dotNetRef.calls.length;
    viewport.dispatch('scroll', {});
    env.pump();
    assert.equal(dotNetRef.calls.length, before); // 摘除后不再上报

    mod.dispose('i9'); // 幂等：重复释放未知实例不抛错
});
