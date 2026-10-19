// signature-pad.js 的真实文件导入与行为测试。
// 模块顶层只建 Map 与 TextEncoder（零 DOM 触碰），canvas/document/window/
// getComputedStyle/ResizeObserver/MutationObserver 均在手柄函数体内访问，
// 因此可在无 DOM 的 Node 环境以 file-URL 导入；模块无相对依赖。
// C# 侧调用点为 barrel 点分标识符 signaturePad.initialize/dispose/undo/isEmpty/
// toSvgStream/toPngStream/toStrokesStream/setStrokes（见 primitives.js 与
// PrimitiveJsModuleTests 的命名空间 camelCase 规则），本文件同时守护该导出面。
// DOM 桩沿用 slider-module.test.mjs / tree-keyboard-module.test.mjs 的最小元素桩惯例：
// canvas 桩记录监听器、指针捕获与 2D 上下文算子；dotNetRef 桩以变参记录
// invokeMethodAsync 调用；toPngStream 的离屏画布经 document.createElement 桩给出。

import assert from 'node:assert/strict';
import { test, afterEach } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Primitives/wwwroot/js/primitives/signature-pad.js', import.meta.url);

// ---- 浏览器全局的最小桩：模块在手柄体内读取它们，模块求值期不触碰 ----

globalThis.window = { devicePixelRatio: 2 };
globalThis.getComputedStyle = () => ({ color: 'rgb(0, 0, 0)' });

const resizeObservers = [];
const mutationObservers = [];

globalThis.ResizeObserver = class {
    constructor(cb) { this.cb = cb; this.disconnected = false; resizeObservers.push(this); }
    observe() { this.observed = true; }
    disconnect() { this.disconnected = true; }
};
globalThis.MutationObserver = class {
    constructor(cb) { this.cb = cb; this.disconnected = false; mutationObservers.push(this); }
    observe() { this.observed = true; }
    disconnect() { this.disconnected = true; }
};

// ---- 2D 上下文桩：记录算子调用供断言 ----

function makeCtx() {
    return {
        fillStyle: '',
        curves: 0,   // beginPath+fill 成对出现的绘制批次
        clears: 0,
        transforms: 0,
        fills: 0,    // fillRect 次数（导出底色的填充沿）
        beginPath() { this.curves++; },
        moveTo() { },
        arc() { },
        fill() { },
        clearRect() { this.clears++; },
        setTransform() { this.transforms++; },
        fillRect() { this.fills++; },
    };
}

// ---- canvas 元素桩 ----

const RECT = { left: 0, top: 0, right: 100, bottom: 100, width: 100, height: 100 };

function makeCanvas(attrs = {}) {
    const handlers = new Map();
    const captured = new Set();
    const canvas = {
        attrs,
        handlers,
        ctx: makeCtx(),
        width: 0,
        height: 0,
        getBoundingClientRect() { return RECT; },
        getAttribute(name) { return canvas.attrs[name] ?? null; },
        setAttribute(name, value) { canvas.attrs[name] = value; },
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
        setPointerCapture(id) { captured.add(id); },
        releasePointerCapture(id) { captured.delete(id); },
        hasPointerCapture(id) { return captured.has(id); },
        getContext: () => canvas.ctx,
        // 离屏副本用：toBlob 交回一个带固定字节的 blob 桩
        toBlob(resolve) { resolve({ arrayBuffer: async () => new Uint8Array([1, 2, 3]).buffer }); },
    };
    return canvas;
}

// renderForExport 经 document.createElement('canvas') 取离屏副本；记录副本桩。
const exportCopies = [];
globalThis.document = {
    documentElement: {},
    createElement() {
        const copy = makeCanvas();
        exportCopies.push(copy);
        return copy;
    },
};

// ---- dotNetRef 桩 ----

function makeDotNet() {
    const calls = [];
    return {
        calls,
        invokeMethodAsync(name, ...args) {
            calls.push([name, ...args]);
            return Promise.resolve();
        },
    };
}

// ---- 指针事件桩 ----

function pe(typeId, x, y, t, overrides = {}) {
    return {
        pointerId: 1,
        isPrimary: true,
        button: 0,
        clientX: x,
        clientY: y,
        timeStamp: t,
        preventDefault() { this.defaultPrevented = true; },
        ...overrides,
    };
}

// 完成一笔：down -> moves -> up。pts 为 [x, y, t] 三元组数组。
function drawStroke(canvas, pts) {
    const [first, ...rest] = pts;
    canvas.dispatch('pointerdown', pe('down', first[0], first[1], first[2]));
    for (const [x, y, t] of rest) {
        canvas.dispatch('pointermove', pe('move', x, y, t));
    }
    canvas.dispatch('pointerup', pe('up', rest.at(-1)[0], rest.at(-1)[1], rest.at(-1)[2]));
}

// ---- 注册与清理 ----

const ids = [];
let currentMod = null;

async function load() {
    currentMod ??= await import(moduleUrl.href);
    return currentMod;
}

async function fresh(canvas, dotNet, id) {
    const mod = await load();
    mod.initialize(canvas, dotNet ?? makeDotNet(), id);
    ids.push(id);
    return mod;
}

afterEach(() => {
    while (ids.length) currentMod.dispose(ids.pop());
    resizeObservers.length = 0;
    mutationObservers.length = 0;
    exportCopies.length = 0;
});

// ------------------------------------------------------------------

test('signature-pad.js 可导入且导出面与 signaturePad. 点分标识符一一对应', async () => {
    const mod = await import(moduleUrl.href);

    // C# 侧调用点：initialize/clear/undo/isEmpty/toSvg/getStrokes/setStrokes/
    // toSvgStream/toStrokesStream/toPngStream/dispose。
    for (const name of [
        'initialize', 'clear', 'undo', 'isEmpty', 'toSvg', 'getStrokes', 'setStrokes',
        'toSvgStream', 'toStrokesStream', 'toPngStream', 'dispose',
    ]) {
        assert.equal(typeof mod[name], 'function', `signature-pad.js 缺少导出 ${name}`);
    }
});

test('initialize 对缺失入参静默返回；未知 id 的操作沿安全回落', async () => {
    const mod = await import(moduleUrl.href);

    mod.initialize(null, makeDotNet(), 'x');
    mod.initialize(makeCanvas(), null, 'x');

    mod.dispose('未知id'); // 不抛错即通过
    assert.equal(mod.isEmpty('未知id'), true);
    assert.equal(mod.undo('未知id'), false);
    assert.equal(mod.toSvg('未知id'), null);
    assert.equal(mod.toSvgStream('未知id'), null);
    assert.deepEqual(mod.getStrokes('未知id'), []);
    mod.clear('未知id');
    mod.setStrokes('未知id', []); // 早于 initialize 的调用不得抛错
});

test('initialize 挂载五个指针监听器与两个观察器，并按设备像素比配置后备存储', async () => {
    const canvas = makeCanvas();
    await fresh(canvas, null, `pad-init-${ids.length}`);

    for (const type of ['pointerdown', 'pointermove', 'pointerup', 'pointercancel', 'lostpointercapture']) {
        assert.equal(canvas.count(type), 1, `缺少 ${type} 监听器`);
    }
    assert.equal(resizeObservers.length, 1);
    assert.equal(mutationObservers.length, 1);
    // RECT 100x100、devicePixelRatio=2 -> 后备存储 200x200，且缩放变换落定。
    assert.equal(canvas.width, 200);
    assert.equal(canvas.height, 200);
    assert.equal(canvas.ctx.transforms, 1);
});

test('同 id 重复 initialize 先摘除旧实例（监听器与观察器不翻倍）', async () => {
    const mod = await load();
    const first = makeCanvas();
    const second = makeCanvas();

    mod.initialize(first, makeDotNet(), 'pad-reinit');
    mod.initialize(second, makeDotNet(), 'pad-reinit');
    ids.push('pad-reinit');

    assert.equal(first.count('pointerdown'), 0); // dispose(padId) 先行摘除
    assert.equal(second.count('pointerdown'), 1);
    assert.equal(resizeObservers.length, 2);
    assert.equal(resizeObservers[0].disconnected, true);
});

test('指针事件沿：down/move/up 成笔并经 dotNetRef 上报一次 JsSignatureChanged', async () => {
    const canvas = makeCanvas();
    const dotNet = makeDotNet();
    await fresh(canvas, dotNet, 'pad-events');

    drawStroke(canvas, [[10, 10, 0], [20, 15, 16], [30, 20, 32], [40, 25, 48]]);

    assert.equal(dotNet.calls.length, 1); // 只在笔尾上报，绘制过程零调用
    assert.deepEqual(dotNet.calls[0], ['JsSignatureChanged', false]);
    assert.equal(canvas.ctx.curves > 0, true); // 曲线条例被描画过

    // 笔中的事件一律 preventDefault
    const down = pe('down', 5, 5, 100);
    canvas.dispatch('pointerdown', down);
    assert.equal(down.defaultPrevented, true);
    canvas.dispatch('pointerup', pe('up', 6, 6, 120));
});

test('非主指针、次键与禁用态吞掉 down', async () => {
    const canvas = makeCanvas();
    const dotNet = makeDotNet();
    await fresh(canvas, dotNet, 'pad-guards');

    canvas.dispatch('pointerdown', pe('down', 10, 10, 0, { isPrimary: false }));
    canvas.dispatch('pointerdown', pe('down', 10, 10, 0, { button: 2 }));
    assert.equal(dotNet.calls.length, 0);

    // 已有活动指针时第二个 down 被忽略；异 id 的 up 不收尾
    drawStroke(canvas, [[10, 10, 0], [20, 15, 16], [30, 20, 32]]);
    const afterFirst = dotNet.calls.length;
    canvas.dispatch('pointerdown', pe('down', 40, 40, 50));
    canvas.dispatch('pointerdown', pe('down', 41, 41, 55, { pointerId: 2 }));
    canvas.dispatch('pointerup', pe('up', 41, 41, 60, { pointerId: 2 }));
    assert.equal(dotNet.calls.length, afterFirst);
    canvas.dispatch('pointerup', pe('up', 40, 40, 70)); // 收掉这支笔
    const before = dotNet.calls.length;

    // data-disabled=true：完全不采集
    canvas.setAttribute('data-disabled', 'true');
    canvas.dispatch('pointerdown', pe('down', 50, 50, 70));
    canvas.dispatch('pointerup', pe('up', 50, 50, 80));
    assert.equal(dotNet.calls.length, before);
});

test('轻点成点：少于三个采样按圆点入库并仍上报非空', async () => {
    const canvas = makeCanvas();
    const dotNet = makeDotNet();
    await fresh(canvas, dotNet, 'pad-dot');

    canvas.dispatch('pointerdown', pe('down', 12, 34, 0));
    canvas.dispatch('pointerup', pe('up', 12, 34, 5));

    assert.equal(dotNet.calls.length, 1);
    assert.deepEqual(dotNet.calls[0], ['JsSignatureChanged', false]);
    const strokes = currentMod.getStrokes('pad-dot');
    assert.equal(strokes.length, 1);
    assert.equal(strokes[0].points.length, 1);
});

test('isEmpty/undo/clear 的笔集语义', async () => {
    const canvas = makeCanvas();
    const mod = await fresh(canvas, null, 'pad-ops');

    assert.equal(mod.isEmpty('pad-ops'), true);

    drawStroke(canvas, [[10, 10, 0], [20, 15, 16], [30, 20, 32], [40, 25, 48]]);
    drawStroke(canvas, [[60, 60, 100], [70, 65, 116], [80, 70, 132], [90, 75, 148]]);
    assert.equal(mod.isEmpty('pad-ops'), false);
    assert.equal(mod.getStrokes('pad-ops').length, 2);

    assert.equal(mod.undo('pad-ops'), true);
    assert.equal(mod.getStrokes('pad-ops').length, 1);
    assert.equal(mod.undo('pad-ops'), true);
    assert.equal(mod.isEmpty('pad-ops'), true);
    assert.equal(mod.undo('pad-ops'), false); // 空画板无可撤销

    drawStroke(canvas, [[10, 10, 0], [20, 15, 16], [30, 20, 32], [40, 25, 48]]);
    const clearsBefore = canvas.ctx.clears;
    mod.clear('pad-ops');
    assert.equal(mod.isEmpty('pad-ops'), true);
    assert.deepEqual(mod.getStrokes('pad-ops'), []);
    assert.ok(canvas.ctx.clears > clearsBefore); // clear 伴随重绘清屏
});

test('toSvg 形状：空为 null；圆点为 circle；曲线为 g 包裹的 path 且坐标一位小数', async () => {
    const mod = await load();
    const id = 'pad-svg';
    mod.initialize(makeCanvas(), makeDotNet(), id);
    ids.push(id);
    assert.equal(mod.toSvg(id), null);

    mod.setStrokes(id, [
        { points: [{ x: 1.23, y: 4.56, t: 0 }] }, // 单采样 -> 圆点
        { points: [{ x: 0, y: 0, t: 0 }, { x: 10, y: 2, t: 16 }, { x: 20, y: 0, t: 32 }, { x: 30, y: 2, t: 48 }] },
    ]);
    ids.push(id);

    const svg = mod.toSvg(id);
    assert.ok(svg.startsWith('<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"'));
    assert.ok(svg.includes('viewBox="0 0 100 100"'));
    assert.ok(svg.includes('style="max-width:100%;height:auto"'));
    assert.ok(svg.includes('<g fill="none" stroke="currentColor" stroke-linecap="round">'));
    // 圆点覆盖 fill，坐标四舍五入到一位小数。
    assert.ok(svg.includes('<circle cx="1.2" cy="4.6" r="1.5" fill="currentColor"/>'));
    // 曲线是 M...C... 的 path，且带自身 stroke-width。
    const path = svg.match(/<path d="M[^"]*" stroke-width="[^"]*"\/>/);
    assert.ok(path, `未找到 path：${svg}`);
    assert.match(path[0], /^<path d="M[0-9. -]+C[0-9. -]+"/);
});

test('getStrokes/setStrokes 往返：采样逐字段拷贝，空点笔与 null 笔被过滤', async () => {
    const mod = await load();
    const id = 'pad-roundtrip';
    const source = [
        { points: [{ x: 1, y: 2, t: 3 }, { x: 4, y: 5, t: 6 }, { x: 7, y: 8, t: 9 }] },
        { points: [] },          // 过滤
        { points: null },        // 过滤
        {},                       // 过滤
    ];

    mod.initialize(makeCanvas(), makeDotNet(), id);
    ids.push(id);
    mod.setStrokes(id, source);

    assert.deepEqual(mod.getStrokes(id), [
        { points: [{ x: 1, y: 2, t: 3 }, { x: 4, y: 5, t: 6 }, { x: 7, y: 8, t: 9 }] },
    ]);
    // 返回的是拷贝而非引用。
    const strokes = mod.getStrokes(id);
    strokes[0].points[0].x = 999;
    assert.equal(mod.getStrokes(id)[0].points[0].x, 1);
});

test('toSvgStream/toStrokesStream 返回 UTF-8 字节形态；toPngStream 经 toBlob 交回字节', async () => {
    const mod = await load();
    const id = 'pad-streams';
    mod.initialize(makeCanvas(), makeDotNet(), id);
    ids.push(id);

    assert.equal(mod.toSvgStream(id), null); // 空画板无 SVG 可流
    assert.ok(mod.toStrokesStream(id) instanceof Uint8Array);
    assert.equal(new TextDecoder().decode(mod.toStrokesStream(id)), '[]');

    mod.setStrokes(id, [
        { points: [{ x: 0, y: 0, t: 0 }, { x: 10, y: 2, t: 16 }, { x: 20, y: 0, t: 32 }, { x: 30, y: 2, t: 48 }] },
    ]);

    const svgBytes = mod.toSvgStream(id);
    assert.ok(svgBytes instanceof Uint8Array);
    assert.equal(new TextDecoder().decode(svgBytes), mod.toSvg(id));
    assert.equal(new TextDecoder().decode(mod.toStrokesStream(id)), JSON.stringify(mod.getStrokes(id)));

    const png = await mod.toPngStream(id, null, '#000000');
    assert.ok(png instanceof Uint8Array);
    assert.deepEqual([...png], [1, 2, 3]); // 副本桩 toBlob 的固定字节
    // background 沿：底色经 fillRect 铺出。
    exportCopies.length = 0;
    await mod.toPngStream(id, '#ffffff', '#000000');
    assert.equal(exportCopies.length, 1);
    assert.equal(exportCopies[0].ctx.fills, 1);
    // 空画板无 PNG。
    mod.clear(id);
    assert.equal(await mod.toPngStream(id, null, '#000000'), null);
});

test('dispose 摘除全部监听器与观察器且幂等；dispose 后句柄不再上报', async () => {
    const mod = await load();
    const canvas = makeCanvas();
    const dotNet = makeDotNet();
    const id = 'pad-dispose';

    mod.initialize(canvas, dotNet, id);
    canvas.dispatch('pointerdown', pe('down', 10, 10, 0)); // 活动指针待释放
    assert.equal(canvas.hasPointerCapture(1), true);

    mod.dispose(id);
    mod.dispose(id); // 幂等：二次调用不抛错

    for (const type of ['pointerdown', 'pointermove', 'pointerup', 'pointercancel', 'lostpointercapture']) {
        assert.equal(canvas.count(type), 0, `${type} 监听器未摘除`);
    }
    assert.equal(canvas.hasPointerCapture(1), false); // 活动捕获被释放
    assert.equal(resizeObservers[0].disconnected, true);
    assert.equal(mutationObservers[0].disconnected, true);

    // 摘除后画布事件不再抵达引擎：无任何 dotNet 上报。
    canvas.dispatch('pointerup', pe('up', 10, 10, 5));
    assert.equal(dotNet.calls.length, 0);
    assert.equal(mod.isEmpty(id), true);
});
