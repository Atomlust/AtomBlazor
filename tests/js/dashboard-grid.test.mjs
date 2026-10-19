// 测试波：dashboard-grid.js 的真实文件导入与行为测试（ui/ 对本族零测试，全自写）。
// 模块顶层仅建 instances Map 与常量 DRAG_THRESHOLD，document/window/getComputedStyle/
// ResizeObserver/MutationObserver 全部在函数体内引用，可在无 DOM 的 Node 环境以
// file-URL 导入；模块无相对依赖。
// C# 侧调用点为 JsModules.GetAsync 直连独立路径 ./_content/AtomBlazor.Components/js/
// dashboard-grid.js（Components/DashboardGrid/DashboardGrid.razor，同 DataGrid/TreeView
// 先例，不入 components-core barrel）。
// 语义照源锁存：dotNetRef 假值与网格元素缺失均静默早退；未登记 instanceId 的
// updateGridOptions/requestCompact/setEditable/disposeDashboardGrid 全部 no-op；
// 空网格初始化即置 data-positioned=true 并首报 breakpoint large（isInitial 跳压紧回调）；
// 压紧两遍算法（先左滑再上提）直写 gridColumn/gridRow 内联样式并经 JsOnCompactComplete
// 回传 .NET；列跨度按当前 breakpoint 列数钳位（col 回拉 max(1, columns-span+1)）；
// 释放拆观察器与六路监听（网格 pointerdown/keydown/focusout + 文档 pointermove/
// pointerup/pointercancel/keydown）。
// DOM 桩沿用 sidebar.test.mjs / resizable.test.mjs / dock.test.mjs 先例最小口径：
// document 监听表 + querySelector 按 [data-dashboard-id] 解析、通用元素桩 attrs/style/
// setProperty + :scope 子代查询按 data-widget-id 台账；window.innerWidth 逐测试注入；
// 观察器桩登记 disconnect 墓碑。共享 instances 以 instanceId 分键，各测试独立 id 互不串扰。

import assert from 'node:assert/strict';
import test from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/dashboard-grid.js', import.meta.url);

// 顶层导入：此刻尚未安装任何 DOM 桩，直接证明模块加载零 DOM 依赖。
const grid = await import(moduleUrl);

// ------------------------------------------------------------------ 环境桩

let docHandlers;
let grids;           // instanceId -> 网格元素桩
let widgets;         // data-widget-id -> 控件元素桩（供文档级回退查询）
let observers;       // { resize: [], mutation: [] }
let innerWidthValue;

function makeStyle() {
    return {
        props: {},
        setProperty(name, value) { this.props[name] = value; },
    };
}

function makeWidget(id, gridColumn, gridRow) {
    const el = {
        nodeType: 1,
        attrs: { 'data-widget-id': id, 'data-static': 'false' },
        style: { gridColumn, gridRow },
        getAttribute(name) { return this.attrs[name] ?? null; },
        hasAttribute(name) { return name in this.attrs; },
    };
    widgets.set(id, el);
    return el;
}

function makeGridEl(id, children = []) {
    return {
        nodeType: 1,
        isConnected: true,
        attrs: { 'data-dashboard-id': id, 'data-positioned': 'false' },
        children,
        style: makeStyle(),
        listeners: new Map(),
        getAttribute(name) { return this.attrs[name] ?? null; },
        setAttribute(name, value) { this.attrs[name] = String(value); },
        addEventListener(type, fn) {
            if (!this.listeners.has(type)) this.listeners.set(type, []);
            this.listeners.get(type).push(fn);
        },
        removeEventListener(type, fn) {
            const list = this.listeners.get(type) ?? [];
            const index = list.indexOf(fn);
            if (index !== -1) list.splice(index, 1);
        },
        querySelectorAll(selector) {
            if (selector === ':scope > [data-widget-id]') return this.children;
            return [];
        },
        querySelector(selector) {
            // 兼容两种调用点：压紧回写的 ':scope > [data-widget-id="x"]' 与
            // updateWidgetPositions 容器查询的裸 '[data-widget-id="x"]'。
            const match = /^(?::scope >\s*)?\[data-widget-id="([^"]+)"\]$/.exec(selector);
            if (!match) return null;
            return this.children.find((w) => w.getAttribute('data-widget-id') === match[1]) ?? null;
        },
    };
}

class StubResizeObserver {
    constructor(cb) { this.cb = cb; this.disconnected = false; observers.resize.push(this); }
    observe() { this.observed = true; }
    disconnect() { this.disconnected = true; }
}

class StubMutationObserver {
    constructor(cb) { this.cb = cb; this.disconnected = false; observers.mutation.push(this); }
    observe() { this.observed = true; }
    disconnect() { this.disconnected = true; }
}

function installEnv({ innerWidth = 1920 } = {}) {
    docHandlers = new Map();
    grids = new Map();
    widgets = new Map();
    observers = { resize: [], mutation: [] };
    innerWidthValue = innerWidth;

    globalThis.document = {
        body: {},
        addEventListener(type, fn) {
            if (!docHandlers.has(type)) docHandlers.set(type, []);
            docHandlers.get(type).push(fn);
        },
        removeEventListener(type, fn) {
            const list = docHandlers.get(type) ?? [];
            const index = list.indexOf(fn);
            if (index !== -1) list.splice(index, 1);
        },
        querySelector(selector) {
            const byDashboard = /^\[data-dashboard-id="([^"]+)"\]$/.exec(selector);
            if (byDashboard) return grids.get(byDashboard[1]) ?? null;
            const byWidget = /^\[data-widget-id="([^"]+)"\]$/.exec(selector);
            if (byWidget) return widgets.get(byWidget[1]) ?? null;
            return null;
        },
    };
    globalThis.window = { get innerWidth() { return innerWidthValue; } };
    globalThis.getComputedStyle = () => ({
        paddingTop: '0px',
        paddingRight: '0px',
        paddingBottom: '0px',
        paddingLeft: '0px',
        gridTemplateColumns: '120px 120px',
    });
    globalThis.ResizeObserver = StubResizeObserver;
    globalThis.MutationObserver = StubMutationObserver;
}

function makeDotNetRef() {
    const calls = [];
    return {
        calls,
        invokeMethodAsync(method, ...args) {
            calls.push({ method, args });
            return Promise.resolve();
        },
    };
}

// invokeSafely 的 .catch 沿是微任务；setImmediate 把微任务队列整段冲刷完。
const flush = () => new Promise((resolve) => setImmediate(resolve));

function defaultOptions(overrides = {}) {
    return {
        columns: 4,
        rowHeight: 40,
        gap: 8,
        allowDrag: true,
        allowResize: true,
        editable: true,
        compact: true,
        showGridGuide: true,
        mediumBreakpoint: 1024,
        smallBreakpoint: 768,
        mediumColumns: 3,
        smallColumns: 2,
        ...overrides,
    };
}

// 注册并装载一个网格：返回 { el, ref }。
function initGrid(instanceId, children = [], options = {}, env = {}) {
    installEnv(env);
    const el = makeGridEl(instanceId, children);
    grids.set(instanceId, el);
    const ref = makeDotNetRef();
    grid.initializeDashboardGrid(ref, instanceId, defaultOptions(options));
    return { el, ref };
}

// ------------------------------------------------------------------ 装载面

test('模块无DOM可导入且六路导出齐备', async () => {
    for (const name of [
        'initializeDashboardGrid', 'updateGridOptions', 'updateWidgetPositions',
        'requestCompact', 'setEditable', 'disposeDashboardGrid',
    ]) {
        assert.equal(typeof grid[name], 'function', `${name} 应为导出函数`);
    }
});

test('dotNetRef假值早退且不触碰文档查询', async () => {
    installEnv();
    let queries = 0;
    const inner = globalThis.document.querySelector;
    globalThis.document.querySelector = (sel) => { queries++; return inner.call(globalThis.document, sel); };

    grid.initializeDashboardGrid(null, 'g-null', defaultOptions());
    assert.equal(queries, 0);
    grid.initializeDashboardGrid(undefined, 'g-undef', defaultOptions());
    assert.equal(queries, 0);
});

test('网格元素缺失静默早退且后续请求全部无操作', async () => {
    installEnv();
    const ref = makeDotNetRef();
    grid.initializeDashboardGrid(ref, 'g-absent', defaultOptions());
    assert.equal(ref.calls.length, 0);
    assert.doesNotThrow(() => grid.requestCompact('g-absent'));
    assert.doesNotThrow(() => grid.updateGridOptions('g-absent', defaultOptions()));
});

test('未登记实例的四个更新入口一律静默', async () => {
    installEnv();
    assert.doesNotThrow(() => grid.updateGridOptions('ghost', defaultOptions()));
    assert.doesNotThrow(() => grid.requestCompact('ghost'));
    assert.doesNotThrow(() => grid.setEditable('ghost', false));
    assert.doesNotThrow(() => grid.disposeDashboardGrid('ghost'));
});

// ------------------------------------------------------------------ 初始化行为

test('空网格初始化置positioned标记并首报large断点且不回传压缩', async () => {
    const { el, ref } = initGrid('g-empty');
    await flush();

    assert.equal(el.getAttribute('data-positioned'), 'true');
    const bp = ref.calls.find((c) => c.method === 'JsOnBreakpointChanged');
    assert.deepEqual([bp.method, bp.args], ['JsOnBreakpointChanged', ['large']]);
    // isInitial 沿：首报跳过 recompact，空网格无压缩回传。
    assert.equal(ref.calls.filter((c) => c.method === 'JsOnCompactComplete').length, 0);
    // 导引背景 SVG 已写入自定义属性（syncGridGuide）。
    assert.match(el.style.props['--atom-dashboard-guide-svg'], /^url\("data:image\/svg\+xml,/);
});

test('初始化两遍压紧左滑上提并回传压缩位', async () => {
    const a = makeWidget('wa', '1 / span 1', '1 / span 1');
    const b = makeWidget('wb', '1 / span 1', '3 / span 1');
    const { el, ref } = initGrid('g-compact', [a, b]);
    await flush();

    // a 原地不动；b 第一遍左滑（列 1 在行 3 不冲突、保持 1），第二遍上提至行 2（行 1 撞 a）。
    assert.equal(a.style.gridColumn, '1 / span 1');
    assert.equal(a.style.gridRow, '1 / span 1');
    assert.equal(b.style.gridColumn, '1 / span 1');
    assert.equal(b.style.gridRow, '2 / span 1');

    const complete = ref.calls.find((c) => c.method === 'JsOnCompactComplete');
    assert.ok(complete, '应回传压缩完成');
    assert.deepEqual(complete.args[0], [
        { widgetId: 'wa', col: 1, row: 1, colSpan: 1, rowSpan: 1 },
        { widgetId: 'wb', col: 1, row: 2, colSpan: 1, rowSpan: 1 },
    ]);
});

test('窄视口初检落small断点且列跨度按小列数钳位', async () => {
    const wide = makeWidget('wc', '1 / span 6', '1 / span 2');
    const { el, ref } = initGrid('g-small', [wide], { columns: 12 }, { innerWidth: 500 });
    await flush();

    const bp = ref.calls.find((c) => c.method === 'JsOnBreakpointChanged');
    assert.deepEqual([bp.method, bp.args], ['JsOnBreakpointChanged', ['small']]);
    // smallColumns=2：colSpan 6 钳到 2，col 回拉 max(1, 2-2+1)=1。
    assert.equal(wide.style.gridColumn, '1 / span 2');
    assert.equal(wide.style.gridRow, '1 / span 2');
    assert.equal(el.getAttribute('data-positioned'), 'true');
});

// ------------------------------------------------------------------ 更新入口

test('updateWidgetPositions按容器命中写内联样式', async () => {
 // 先装环境再建控件桩：installEnv 会重置文档级控件台账（回退查询依赖它）。
    installEnv();
    const w = makeWidget('wd', '1 / span 1', '1 / span 1');
    const el = makeGridEl('g-sync', [w]);
    grids.set('g-sync', el);
    grid.initializeDashboardGrid(makeDotNetRef(), 'g-sync', defaultOptions());
    await flush();

    grid.updateWidgetPositions('g-sync', [
        { widgetId: 'wd', column: 3, row: 4, columnSpan: 2, rowSpan: 5 },
        { widgetId: 'unhit', column: 9, row: 9, columnSpan: 1, rowSpan: 1 },
    ]);
    assert.equal(w.style.gridColumn, '3 / span 2');
    assert.equal(w.style.gridRow, '4 / span 5');

 // null 位表早退；未登记实例走文档级回退查询。
    grid.updateWidgetPositions('g-sync', null);
    grid.updateWidgetPositions('g-elsewhere', [{ widgetId: 'wd', column: 1, row: 1, columnSpan: 1, rowSpan: 1 }]);
    assert.equal(w.style.gridColumn, '1 / span 1');
    assert.ok(el);
});

test('requestCompact重跑压紧并改写内联摆放', async () => {
    const w = makeWidget('we', '2 / span 1', '4 / span 1');
    initGrid('g-recompact', [w]);
    await flush();

    assert.equal(w.style.gridColumn, '1 / span 1'); // 初检已左滑到列 1、上提到行 1
    assert.equal(w.style.gridRow, '1 / span 1');

    // 模拟 .NET 迟到的摆放覆写，再请求压缩。
    w.style.gridColumn = '2 / span 1';
    w.style.gridRow = '4 / span 1';
    grid.requestCompact('g-recompact');
    assert.equal(w.style.gridColumn, '1 / span 1');
    assert.equal(w.style.gridRow, '1 / span 1');
});

// ------------------------------------------------------------------ 释放面

test('释放拆观察器与六路监听且后续请求无操作', async () => {
    const { el, ref } = initGrid('g-dispose');
    await flush();
    assert.equal(observers.resize.length, 1);
    assert.equal(observers.mutation.length, 1);

    el.attrs['data-positioned'] = 'false';
    grid.disposeDashboardGrid('g-dispose');

    assert.equal(observers.resize[0].disconnected, true);
    assert.equal(observers.mutation[0].disconnected, true);
    for (const type of ['pointerdown', 'keydown', 'focusout']) {
        assert.equal(el.listeners.get(type).length, 0, `${type} 监听应摘净`);
    }
    for (const type of ['pointermove', 'pointerup', 'pointercancel', 'keydown']) {
        assert.equal((docHandlers.get(type) ?? []).length, 0, `文档 ${type} 监听应摘净`);
    }

    // 实例已注销：requestCompact 不再重跑（data-positioned 保持人为 false）。
    grid.requestCompact('g-dispose');
    await flush();
    assert.equal(el.getAttribute('data-positioned'), 'false');
    assert.ok(ref);
});

test('同id重复初始化先释放旧实例不留悬挂观察器', async () => {
    const first = initGrid('g-reinit');
    await flush();
    assert.equal(observers.resize.length, 1);

    // 同 instanceId 再初始化：既有的 disposeDashboardGrid 先行、观察器成对重建。
    const el2 = makeGridEl('g-reinit');
    grids.set('g-reinit', el2);
    grid.initializeDashboardGrid(makeDotNetRef(), 'g-reinit', defaultOptions());
    await flush();

    assert.equal(observers.resize.length, 2);
    assert.equal(observers.resize[0].disconnected, true, '旧观察器应已拆');
    assert.equal(first.el.listeners.get('pointerdown').length, 0, '旧元素监听应已摘净');
    assert.equal(el2.listeners.get('pointerdown').length, 1);
});
