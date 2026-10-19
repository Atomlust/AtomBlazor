// dock.js 的真实文件导入与行为测试。
// 模块顶层仅建 Map 登记簿（docks/tabOverflowObservers），document/body/ResizeObserver/
// getComputedStyle/elementsFromPoint 全部在函数体内引用，可在无 DOM 的 Node 环境以
// file-URL 导入；模块无相对依赖。
// C# 侧调用点为 barrel 点分标识符 dock.initializeDock/startTabDrag/startWindowDrag/
// startWindowResize/disposeDock 与 dock.initTabOverflow/remeasureTabOverflow/
// disposeTabOverflow（Dock.razor.cs 与 DockTabGroup.razor.cs，经
// ComponentModules.GetCoreAsync 装载 components-core.js； JsModules.GetAsync 直连
// dock.js 独立路径属已登记的有意偏差，照 Cascader／Resizable 先例）。
// 语义照源锁存：拖拽阈值 4px（未过阈值视为点击不上报）；落点在 dock 包围盒外→float
// 且 float 坐标取 clientX-rect.left-24 / clientY-rect.top-12；边缘带 26px→root；组内按
// 22% 角区→group；rtl 对水平判断整体镜像；reorder 指示器为 2px 实心 primary、root/group
// 为半透明填充；float/none 只把已建指示器 display:none 不删元素；OnWindowResized 结算读
 offsetWidth/Height（桩以 style getter 模拟浏览器即时回流）；异指针既不提交也不
// 拆会话；initTabOverflow 同 id 重复初始化先拆旧条目；溢出测量 1px 容差且测后恢复原
// display。
// DOM 桩沿用 sidebar.test.mjs / resizable.test.mjs 先例最小口径：document 监听表 +
// body 子元素台账 + 通用元素桩（attrs/style/父子链，closest/querySelector/contains 按
// 属性与标签解析），ResizeObserver 实例登记，getComputedStyle 方向开关，点栈
// elementsFromPoint 逐测试注入。共享 Map 以 dockId/groupId 分键，各测试独立 id 互不串扰。

import assert from 'node:assert/strict';
import { test } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/dock.js', import.meta.url);

// ------------------------------------------------------------------ 环境桩

// 通用元素桩：attrs + style + 父子链 + 监听表。
function makeEl(tag = 'div') {
    const el = {
        tagName: tag,
        attrs: {},
        style: {},
        children: [],
        parentNode: null,
        textContent: '',
        listeners: new Map(),
        getAttribute(name) {
            return Object.prototype.hasOwnProperty.call(el.attrs, name) ? el.attrs[name] : null;
        },
        setAttribute(name, value) { el.attrs[name] = value; },
        addEventListener(type, fn) {
            if (!el.listeners.has(type)) el.listeners.set(type, []);
            el.listeners.get(type).push(fn);
        },
        removeEventListener(type, fn) {
            const list = el.listeners.get(type) ?? [];
            const index = list.indexOf(fn);
            if (index !== -1) list.splice(index, 1);
        },
        dispatch(type, event = {}) {
            for (const fn of [...(el.listeners.get(type) ?? [])]) fn(event);
        },
        appendChild(child) { child.parentNode = el; el.children.push(child); return child; },
        removeChild(child) {
            const index = el.children.indexOf(child);
            if (index !== -1) el.children.splice(index, 1);
            child.parentNode = null;
            return child;
        },
        querySelectorAll(selector) {
            const hits = [];
            for (const child of el.children) {
                if (matches(child, selector)) hits.push(child);
                hits.push(...child.querySelectorAll(selector));
            }
            return hits;
        },
        querySelector(selector) { return el.querySelectorAll(selector)[0] ?? null; },
        contains(other) {
            for (let node = other; node; node = node.parentNode) {
                if (node === el) return true;
            }
            return false;
        },
        closest(selector) {
            for (let node = el; node; node = node.parentNode) {
                if (matches(node, selector)) return node;
            }
            return null;
        },
    };
    return el;
}

function matches(el, selector) {
    const attr = /^\[([\w-]+)(?:="?([^"\]]*)"?)?\]$/.exec(selector);
    if (attr) {
        const value = el.getAttribute(attr[1]);
        if (value === null) return false;
        return attr[2] === undefined || value === attr[2];
    }
    // 无属性选择器时按标签名解析（如 "button"）：/^[\w-]+$/ 捕获整段于 [0]。
    const tag = /^[\w-]+$/.exec(selector);
    if (tag) return (el.tagName ?? '').toLowerCase() === tag[0].toLowerCase();
    return false;
}

// document 桩：全局可换（dock.js 函数体运行时才引用 document）。
function makeDocument() {
    const handlers = new Map();
    const doc = {
        handlers,
        pointStack: [],
        body: makeEl('body'),
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
        createElement: (tag) => makeEl(tag),
        elementsFromPoint: () => doc.pointStack,
    };
    return doc;
}

let doc = makeDocument();
globalThis.document = doc;

// 每测试开局换一份全新 document（监听表与 body 子元素台账归零）。
function freshDoc() {
    doc = makeDocument();
    globalThis.document = doc;
    return doc;
}

// ResizeObserver 桩：实例登记表，observe/disconnect 计数。
const observers = [];
globalThis.ResizeObserver = class {
    constructor(callback) {
        this.callback = callback;
        this.observed = [];
        this.disconnectCalls = 0;
        observers.push(this);
    }

    observe(el) { this.observed.push(el); }

    disconnect() { this.disconnectCalls += 1; }
};

// getComputedStyle 桩：方向受全局开关支配。
let computedDirection = 'ltr';
globalThis.getComputedStyle = () => ({ direction: computedDirection });

function setRect(el, rect) {
    el.getBoundingClientRect = () => ({
        left: rect.left,
        top: rect.top,
        width: rect.width,
        height: rect.height,
        right: rect.left + rect.width,
        bottom: rect.top + rect.height,
    });
    return el;
}

// DotNet 引用桩：记录 invokeMethodAsync 的 (方法名, ...载荷)。
function makeRef(options = {}) {
    const calls = [];
    return {
        calls,
        invokeMethodAsync(name, ...args) {
            calls.push([name, ...args]);
            return options.reject ? Promise.reject(new Error('电路断线')) : Promise.resolve();
        },
    };
}

// 指针事件桩。
function pointer(over = {}) {
    return {
        pointerId: 7, clientX: 0, clientY: 0,
        preventDefaultCalls: 0,
        preventDefault() { this.preventDefaultCalls += 1; },
        ...over,
    };
}

const dk = await import(moduleUrl.href);

let seq = 0;
function nextId(prefix) { return `${prefix}${++seq}`; }

// 注册一个 dock：rootEl 默认包围盒 600×400 于原点。
function mountDock(rect = { left: 0, top: 0, width: 600, height: 400 }, options = {}) {
    const dockId = nextId('dock-');
    const rootEl = setRect(makeEl(), rect);
    rootEl.attrs['data-dock-root'] = dockId;
    const ref = makeRef(options);
    dk.initializeDock(dockId, rootEl, ref);
    return { dockId, rootEl, ref };
}

// 在 root 下挂标签组：groupEl → stripEl → tabEls。
function attachStrip(rootEl, groupRect) {
    const groupId = nextId('group-');
    const stripId = nextId('strip-');
    const groupEl = setRect(makeEl(), groupRect);
    groupEl.attrs['data-dock-group'] = groupId;
    rootEl.appendChild(groupEl);
    const stripEl = setRect(makeEl(), { ...groupRect, top: groupRect.top, height: 32 });
    stripEl.attrs['data-dock-tabstrip'] = stripId;
    groupEl.appendChild(stripEl);
    return { groupId, stripId, groupEl, stripEl };
}

// 挂标签：rect 依行序给定，offsetWidth 与 rect.width 同值。
function attachTabs(stripEl, specs) {
    return specs.map((spec) => {
        const tab = setRect(makeEl(), spec.rect);
        tab.attrs['data-dock-tab'] = spec.id;
        tab.offsetWidth = spec.rect.width;
        if (spec.initialDisplay !== undefined) tab.style.display = spec.initialDisplay;
        stripEl.appendChild(tab);
        return tab;
    });
}

// 起手标签拖拽并推进到激活（越过 4px 阈值）。
function startActiveDrag(dock, from = { x: 300, y: 200 }) {
    dk.startTabDrag(dock.dockId, 'Ghost', from.x, from.y, 7);
    doc.dispatch('pointermove', pointer({ clientX: from.x + 10, clientY: from.y + 10 }));
}

// ------------------------------------------------------------------ 导出面

test('导出面恰为 initializeDock/disposeDock/initTabOverflow/remeasureTabOverflow/disposeTabOverflow/startTabDrag/startWindowDrag/startWindowResize 八枚', () => {
    assert.deepEqual(Object.keys(dk).sort(), [
        'disposeDock', 'disposeTabOverflow', 'initTabOverflow', 'initializeDock',
        'remeasureTabOverflow', 'startTabDrag', 'startWindowDrag', 'startWindowResize',
    ]);
    for (const name of Object.keys(dk)) assert.equal(typeof dk[name], 'function');
});

// ------------------------------------------------------------------ initializeDock / disposeDock

test('initializeDock 缺参早退不登记：随后 startTabDrag 完全静默', () => {
    freshDoc();
    const ref = makeRef();
    const rootEl = setRect(makeEl(), { left: 0, top: 0, width: 600, height: 400 });
    dk.initializeDock(null, rootEl, ref);
    dk.initializeDock('d-missing', null, ref);
    dk.initializeDock('d-missing', rootEl, null);

    dk.startTabDrag('d-missing', 'T', 10, 10, 7);
    dk.startTabDrag('never-seen', 'T', 10, 10, 7);
    assert.equal(doc.count('pointermove'), 0);
    assert.equal(doc.body.style.userSelect, undefined);
});

test('disposeDock 摘登记与未知 id 静默：双释不抛，释放后指针手势不再可起', () => {
    freshDoc();
    const dock = mountDock();
    dk.disposeDock(dock.dockId);
    dk.disposeDock(dock.dockId); // 双释同 id 静默
    dk.disposeDock('ghost-dock'); // 未知 id 静默

    dk.startTabDrag(dock.dockId, 'T', 100, 100, 7);
    assert.equal(doc.count('pointermove'), 0);
});

test('disposeDock 中止进行中的拖拽：幽灵与指示器清除、零上报、监听全摘', () => {
    freshDoc();
    const dock = mountDock();
    const { stripEl } = attachStrip(dock.rootEl, { left: 100, top: 0, width: 400, height: 400 });
    attachTabs(stripEl, [{ id: 'a', rect: { left: 100, top: 0, width: 100, height: 32 } }]);
    doc.pointStack = [stripEl.children[0]];
    startActiveDrag(dock, { x: 300, y: 100 }); // 幽灵 + reorder 指示器已入 body
    assert.equal(doc.count('pointermove'), 1);
    assert.equal(doc.body.children.length, 2);

    dk.disposeDock(dock.dockId); // cancelSession 中止：手势不留孤儿
    assert.equal(doc.count('pointermove'), 0);
    assert.equal(doc.body.children.length, 0); // 幽灵/指示器不留孤儿
    assert.deepEqual(dock.ref.calls, []);

    doc.dispatch('pointerup', pointer()); // 释放后指针再松开：零上报
    assert.deepEqual(dock.ref.calls, []);
});

// ------------------------------------------------------------------ initTabOverflow

test('initTabOverflow 缺参早退：不建观察器不上报', () => {
    freshDoc();
    const before = observers.length;
    const stripEl = makeEl();
    dk.initTabOverflow(null, stripEl, makeRef());
    dk.initTabOverflow('g-bad', null, makeRef());
    dk.initTabOverflow('g-bad', stripEl, null);
    assert.equal(observers.length, before);
});

test('initTabOverflow 正常链：observe 标签带、keydown 挂带、首报溢出 id 且测后恢复原 display', () => {
    freshDoc();
    const dock = mountDock();
    const { stripEl } = attachStrip(dock.rootEl, { left: 0, top: 0, width: 600, height: 400 });
    stripEl.clientWidth = 250;
    const tabs = attachTabs(stripEl, [
        { id: 'a', rect: { left: 0, top: 0, width: 100, height: 32 }, initialDisplay: 'flex' },
        { id: 'b', rect: { left: 100, top: 0, width: 100, height: 32 } },
        { id: 'c', rect: { left: 200, top: 0, width: 100, height: 32 } },
    ]);
    const ref = makeRef();
    dk.initTabOverflow('g1', stripEl, ref);

    assert.equal(observers.at(-1).observed[0], stripEl);
    assert.deepEqual(ref.calls, [['OnTabOverflowChanged', ['c']]]); // 300 > 250+1 溢出 c
    assert.deepEqual(tabs.map((t) => t.style.display), ['flex', undefined, undefined]); // 原状恢复
    assert.equal(stripEl.listeners.get('keydown').length, 1);
    dk.disposeTabOverflow('g1');
});

test('溢出测量 1px 容差：used 恰等于 available+1 不溢出，再多 1 字节才溢出', () => {
    freshDoc();
    const dock = mountDock();
    const { stripEl } = attachStrip(dock.rootEl, { left: 0, top: 0, width: 600, height: 400 });
    const tabs = attachTabs(stripEl, [
        { id: 'a', rect: { left: 0, top: 0, width: 100, height: 32 } },
        { id: 'b', rect: { left: 100, top: 0, width: 100, height: 32 } },
    ]);
    const ref = makeRef();

    stripEl.clientWidth = 199; // 200 === 199+1 → 恰容下放
    dk.initTabOverflow('g-tol', stripEl, ref);
    assert.deepEqual(ref.calls.at(-1), ['OnTabOverflowChanged', []]);

    stripEl.clientWidth = 198; // 200 > 199 → b 溢出
    dk.remeasureTabOverflow('g-tol');
    assert.deepEqual(ref.calls.at(-1), ['OnTabOverflowChanged', ['b']]);
    assert.equal(tabs[1].style.display, undefined);
    dk.disposeTabOverflow('g-tol');
});

test('initTabOverflow 同 id 重复初始化：先拆旧条目（disconnect + 摘 keydown），会话不叠加', () => {
    freshDoc();
    const dock = mountDock();
    const first = attachStrip(dock.rootEl, { left: 0, top: 0, width: 600, height: 400 });
    first.stripEl.clientWidth = 1000;
    const ref1 = makeRef();
    dk.initTabOverflow('g2', first.stripEl, ref1);
    const firstObserver = observers.at(-1);

    const second = attachStrip(dock.rootEl, { left: 0, top: 0, width: 600, height: 400 });
    second.stripEl.clientWidth = 1000;
    const ref2 = makeRef();
    dk.initTabOverflow('g2', second.stripEl, ref2);

    assert.equal(firstObserver.disconnectCalls, 1);
    assert.equal(first.stripEl.listeners.get('keydown').length, 0); // 旧 keydown 摘净
    assert.equal(second.stripEl.listeners.get('keydown').length, 1);
    assert.equal(ref1.calls.length, 1); // 旧条目拆除后不再收报
    dk.disposeTabOverflow('g2');
});

test('标签带 keydown 只拦标签非按钮目标：Space/方向/Home/End preventDefault，Tab 放行', () => {
    freshDoc();
    const dock = mountDock();
    const { stripEl } = attachStrip(dock.rootEl, { left: 0, top: 0, width: 600, height: 400 });
    stripEl.clientWidth = 1000;
    const [tab] = attachTabs(stripEl, [{ id: 'a', rect: { left: 0, top: 0, width: 100, height: 32 } }]);
    const closeBtn = makeEl('button');
    tab.appendChild(closeBtn);
    const ref = makeRef();
    dk.initTabOverflow('g-key', stripEl, ref);

    const blockedKeys = [' ', 'ArrowLeft', 'ArrowRight', 'Home', 'End'];
    for (const key of blockedKeys) {
        const event = pointer({ key, target: tab });
        stripEl.dispatch('keydown', event);
        assert.equal(event.preventDefaultCalls, 1, `键 ${key} 应 preventDefault`);
    }

    const pass = pointer({ key: 'Tab', target: tab });
    stripEl.dispatch('keydown', pass);
    assert.equal(pass.preventDefaultCalls, 0);

    const onButton = pointer({ key: ' ', target: closeBtn });
    stripEl.dispatch('keydown', onButton);
    assert.equal(onButton.preventDefaultCalls, 0); // 按钮内 Space 留给原生点击

    const outside = pointer({ key: ' ', target: makeEl() }); // closest 落空
    stripEl.dispatch('keydown', outside);
    assert.equal(outside.preventDefaultCalls, 0);

    const noClosest = { key: ' ', target: {}, preventDefaultCalls: 0 }; // 目标无 closest 方法
    stripEl.dispatch('keydown', noClosest);
    assert.equal(noClosest.preventDefaultCalls, 0);
    dk.disposeTabOverflow('g-key');
});

test('remeasureTabOverflow 未知 id 静默；disposeTabOverflow 摘净后同 id 重测无报', () => {
    freshDoc();
    dk.remeasureTabOverflow('g-nowhere'); // 不抛即通过
    const dock = mountDock();
    const { stripEl } = attachStrip(dock.rootEl, { left: 0, top: 0, width: 600, height: 400 });
    stripEl.clientWidth = 1000;
    const ref = makeRef();
    dk.initTabOverflow('g3', stripEl, ref);
    assert.equal(ref.calls.length, 1);

    dk.disposeTabOverflow('g3');
    dk.disposeTabOverflow('g3'); // 双释静默
    const callsAfterDispose = ref.calls.length;
    dk.remeasureTabOverflow('g3');
    assert.equal(ref.calls.length, callsAfterDispose); // 释放后不再上报
    assert.equal(stripEl.listeners.get('keydown').length, 0);
});

// ------------------------------------------------------------------ startTabDrag 判定与上报

test('位移未过 4px 阈值视为点击：幽灵不建、pointerup 零上报但会话照常全拆', () => {
    freshDoc();
    const dock = mountDock();
    dk.startTabDrag(dock.dockId, 'T', 300, 200, 7);
    doc.dispatch('pointermove', pointer({ clientX: 302, clientY: 202 })); // hypot≈2.83 < 4
    assert.equal(doc.body.children.length, 0);
    assert.equal(doc.body.style.cursor, undefined); // 未进激活态不写 cursor

    doc.dispatch('pointerup', pointer());
    assert.deepEqual(dock.ref.calls, []);
    assert.equal(doc.count('pointermove'), 0);
    assert.equal(doc.body.style.userSelect, '');
});

test('dock 包围盒外落位 → float：坐标取 落点-root 原点-24/-12，零指示器、幽灵摘除', () => {
    freshDoc();
    const dock = mountDock();
    dk.startTabDrag(dock.dockId, 'T', 300, 200, 7);
    doc.dispatch('pointermove', pointer({ clientX: 700, clientY: 500 })); // 出右界
    assert.equal(doc.body.children.length, 1); // 仅幽灵，无指示器
    doc.dispatch('pointerup', pointer({ clientX: 700, clientY: 500 }));

    assert.deepEqual(dock.ref.calls, [['OnTabDropped', 'float', null, 'center', 676, 488, -1]]);
    assert.equal(doc.body.children.length, 0);
});

test('标签带上落位 → reorder：groupId 与中点插入序随行，指示器为 2px 实心 primary 插入线', () => {
    freshDoc();
    const dock = mountDock();
    const { stripId, stripEl } = attachStrip(dock.rootEl, { left: 100, top: 0, width: 400, height: 400 });
    attachTabs(stripEl, [
        { id: 'a', rect: { left: 100, top: 0, width: 100, height: 32 } },
        { id: 'b', rect: { left: 200, top: 0, width: 100, height: 32 } },
    ]);
    doc.pointStack = [stripEl.children[1]]; // 指针压在 b 标签上

    dk.startTabDrag(dock.dockId, 'T', 300, 100, 7);
    doc.dispatch('pointermove', pointer({ clientX: 220, clientY: 10 })); // b 中点 250 之前 → 槽 1
    const indicator = doc.body.children.at(-1);
    assert.equal(indicator.style.background, 'var(--primary)');
    assert.equal(indicator.style.border, 'none');
    assert.equal(indicator.style.left, '199px'); // b 前缘 200-1
    assert.equal(indicator.style.width, '2px');

    doc.dispatch('pointerup', pointer({ clientX: 220, clientY: 10 }));
    assert.deepEqual(dock.ref.calls, [['OnTabDropped', 'reorder', stripId, 'center', 0, 0, 1]]);
});

test('reorder 槽位三态：首标签前 0、两标签之间 1、全部之后取标签数 2', () => {
    freshDoc();
    const dock = mountDock();
    const { stripEl } = attachStrip(dock.rootEl, { left: 100, top: 0, width: 400, height: 400 });
    attachTabs(stripEl, [
        { id: 'a', rect: { left: 100, top: 0, width: 100, height: 32 } },
        { id: 'b', rect: { left: 200, top: 0, width: 100, height: 32 } },
    ]);
    doc.pointStack = [stripEl.children[0]];
    const cases = [[120, 0], [220, 1], [500, 2]]; // a 中点 150 前 / b 中点 250 前 / 全过
    for (const [x, expected] of cases) {
        dk.startTabDrag(dock.dockId, 'T', 300, 100, 7);
        doc.dispatch('pointermove', pointer({ clientX: x, clientY: 10 }));
        doc.dispatch('pointerup', pointer({ clientX: x, clientY: 10 }));
        assert.equal(dock.ref.calls.at(-1)[6], expected, `x=${x}`);
    }
});

test('边缘带 26px → root 四落点：left/right/top/bottom 与 zoneBox 0.3 比例盒', () => {
    freshDoc();
    const dock = mountDock(); // 600×400 于原点
    const { groupEl } = attachStrip(dock.rootEl, { left: 100, top: 100, width: 400, height: 200 });
    doc.pointStack = [groupEl]; // 组底子上：无标签带命中

    const cases = [
        [/* x */ 10, /* y */ 200, 'left', ['0px', '0px', '180px', '400px']],
        [590, 200, 'right', ['420px', '0px', '180px', '400px']],
        [300, 10, 'top', ['0px', '0px', '600px', '120px']],
        [300, 390, 'bottom', ['0px', '280px', '600px', '120px']],
    ];
    for (const [x, y, zone, box] of cases) {
        dk.startTabDrag(dock.dockId, 'T', 300, 200, 7);
        doc.dispatch('pointermove', pointer({ clientX: x, clientY: y }));
        const indicator = doc.body.children.at(-1); // 结算前取证：pointerup 会 removeEl
        doc.dispatch('pointerup', pointer({ clientX: x, clientY: y }));
        assert.deepEqual(dock.ref.calls.at(-1), ['OnTabDropped', 'root', null, zone, 0, 0, -1]);
        assert.deepEqual(
            [indicator.style.left, indicator.style.top, indicator.style.width, indicator.style.height],
            box);
    }
});

test('组内 22% 角区 → group 落点：左角 left、中心 center 与全幅默认盒', () => {
    freshDoc();
    const dock = mountDock();
    const { groupId, groupEl } = attachStrip(dock.rootEl, { left: 100, top: 100, width: 400, height: 200 });
    doc.pointStack = [groupEl];

    // 组矩形 400×200：m=0.22。左角 (110,200) rx=0.025 ry=0.5 → left。
    dk.startTabDrag(dock.dockId, 'T', 300, 150, 7);
    doc.dispatch('pointermove', pointer({ clientX: 110, clientY: 200 }));
    doc.dispatch('pointerup', pointer({ clientX: 110, clientY: 200 }));
    assert.deepEqual(dock.ref.calls.at(-1), ['OnTabDropped', 'group', groupId, 'left', 0, 0, -1]);

    // 正上角 (110,110)：rx=0.025 与 ry=0.05 同越角阈、inMid 双假 → 四判皆空归 center。
    dk.startTabDrag(dock.dockId, 'T', 300, 200, 7);
    doc.dispatch('pointermove', pointer({ clientX: 110, clientY: 110 }));
    doc.dispatch('pointerup', pointer({ clientX: 110, clientY: 110 }));
    assert.deepEqual(dock.ref.calls.at(-1), ['OnTabDropped', 'group', groupId, 'center', 0, 0, -1]);

    // 中心 (300,200) rx=0.5 ry=0.5 → center；zoneBox('center') 落 default 分支取组全幅盒。
    dk.startTabDrag(dock.dockId, 'T', 300, 150, 7);
    doc.dispatch('pointermove', pointer({ clientX: 300, clientY: 200 }));
    const indicator = doc.body.children.at(-1); // 结算前取证
    doc.dispatch('pointerup', pointer({ clientX: 300, clientY: 200 }));
    assert.deepEqual(dock.ref.calls.at(-1), ['OnTabDropped', 'group', groupId, 'center', 0, 0, -1]);
    assert.deepEqual(
        [indicator.style.left, indicator.style.top, indicator.style.width, indicator.style.height],
        ['100px', '100px', '400px', '200px']);
});

test('rtl 镜像：边缘带左右换向、reorder 中点判据反向、root 落位 zone 随 isRtl 翻转', () => {
    computedDirection = 'rtl';
    try {
        freshDoc();
        const dock = mountDock();
        const { groupEl } = attachStrip(dock.rootEl, { left: 100, top: 100, width: 400, height: 200 });
        doc.pointStack = [groupEl];
        dk.startTabDrag(dock.dockId, 'T', 300, 200, 7);
        doc.dispatch('pointermove', pointer({ clientX: 10, clientY: 200 }));
        doc.dispatch('pointerup', pointer({ clientX: 10, clientY: 200 }));
        assert.deepEqual(dock.ref.calls.at(-1), ['OnTabDropped', 'root', null, 'right', 0, 0, -1]);

        // 组 rx 取 (right-x)/w：x=490 距组右缘 10px → 模型 left（rtl 下贴右即排前）。
        dk.startTabDrag(dock.dockId, 'T', 300, 200, 7);
        doc.dispatch('pointermove', pointer({ clientX: 490, clientY: 200 }));
        doc.dispatch('pointerup', pointer({ clientX: 490, clientY: 200 }));
        assert.equal(dock.ref.calls.at(-1)[3], 'left');
    } finally {
        computedDirection = 'ltr';
    }
});

test('rtl reorder：past 取 x>中点，向左扫过首标签即槽 0', () => {
    computedDirection = 'rtl';
    try {
        freshDoc();
        const dock = mountDock();
        const { stripId, stripEl } = attachStrip(dock.rootEl, { left: 100, top: 0, width: 400, height: 400 });
        attachTabs(stripEl, [
            { id: 'a', rect: { left: 300, top: 0, width: 100, height: 32 } },
            { id: 'b', rect: { left: 200, top: 0, width: 100, height: 32 } },
        ]);
        doc.pointStack = [stripEl.children[0]];
        dk.startTabDrag(dock.dockId, 'T', 100, 100, 7);
        doc.dispatch('pointermove', pointer({ clientX: 480, clientY: 10 })); // > a 中点 350
        doc.dispatch('pointerup', pointer({ clientX: 480, clientY: 10 }));
        assert.deepEqual(dock.ref.calls.at(-1), ['OnTabDropped', 'reorder', stripId, 'center', 0, 0, 0]);
    } finally {
        computedDirection = 'ltr';
    }
});

test('异指针全程忽略：move 不激活、pointerup 不提交也不拆会话，本指针照常结算', () => {
    freshDoc();
    const dock = mountDock();
    dk.startTabDrag(dock.dockId, 'T', 300, 200, 7);

    doc.dispatch('pointermove', pointer({ pointerId: 99, clientX: 700, clientY: 500 }));
    assert.equal(doc.body.children.length, 0); // 幽灵未建

    doc.dispatch('pointerup', pointer({ pointerId: 99 }));
    assert.equal(doc.count('pointermove'), 1); // 会话未拆
    assert.deepEqual(dock.ref.calls, []);

    doc.dispatch('pointermove', pointer({ pointerId: 7, clientX: 700, clientY: 500 }));
    assert.equal(doc.body.children.length, 1);
    doc.dispatch('pointerup', pointer({ pointerId: 7, clientX: 700, clientY: 500 }));
    assert.equal(dock.ref.calls.at(-1)[1], 'float');
});

test('pointercancel 中止拖拽：幽灵指示器清除、零上报、会话全拆', () => {
    freshDoc();
    const dock = mountDock();
    const { stripEl } = attachStrip(dock.rootEl, { left: 100, top: 0, width: 400, height: 400 });
    attachTabs(stripEl, [{ id: 'a', rect: { left: 100, top: 0, width: 100, height: 32 } }]);
    doc.pointStack = [stripEl.children[0]];
    startActiveDrag(dock, { x: 300, y: 100 });
    doc.dispatch('pointermove', pointer({ clientX: 220, clientY: 10 })); // reorder 态
    assert.equal(doc.body.children.length, 2); // 幽灵 + 指示器

    doc.dispatch('pointercancel', pointer());
    assert.equal(doc.body.children.length, 0);
    assert.deepEqual(dock.ref.calls, []);
    assert.equal(doc.count('pointermove'), 0);
    assert.equal(doc.body.style.userSelect, '');
});

test('已建指示器遇 float 落位只 display:none 不删元素（照源锁存），结算时统一摘除', () => {
    freshDoc();
    const dock = mountDock();
    const { stripEl } = attachStrip(dock.rootEl, { left: 100, top: 0, width: 400, height: 400 });
    attachTabs(stripEl, [{ id: 'a', rect: { left: 100, top: 0, width: 100, height: 32 } }]);
    doc.pointStack = [stripEl.children[0]];
    startActiveDrag(dock, { x: 300, y: 100 });
    doc.dispatch('pointermove', pointer({ clientX: 120, clientY: 10 })); // 建指示器（reorder）
    const indicator = doc.body.children.at(-1);
    assert.equal(indicator.style.display, 'block');

    doc.dispatch('pointermove', pointer({ clientX: 700, clientY: 500 })); // 出界 → float
    assert.equal(indicator.style.display, 'none'); // 只隐藏不删除

    doc.dispatch('pointerup', pointer({ clientX: 700, clientY: 500 }));
    assert.equal(dock.ref.calls.at(-1)[1], 'float');
    assert.equal(doc.body.children.length, 0); // removeEl 统一摘除
});

test('invokeMethodAsync 拒绝被 catch 吞：OnTabDropped 与溢出上报断线不炸', async () => {
    freshDoc();
    const dock = mountDock({ left: 0, top: 0, width: 600, height: 400 }, { reject: true });
    dk.startTabDrag(dock.dockId, 'T', 300, 200, 7);
    doc.dispatch('pointermove', pointer({ clientX: 700, clientY: 500 }));
    doc.dispatch('pointerup', pointer({ clientX: 700, clientY: 500 }));
    assert.equal(dock.ref.calls.length, 1);

    const { stripEl } = attachStrip(dock.rootEl, { left: 0, top: 0, width: 600, height: 400 });
    stripEl.clientWidth = 100;
    attachTabs(stripEl, [{ id: 'a', rect: { left: 0, top: 0, width: 200, height: 32 } }]);
    const rejecting = makeRef({ reject: true });
    dk.initTabOverflow('g-reject', stripEl, rejecting);
    await Promise.resolve(); // 让 microtask 拒绝面走 catch
    dk.disposeTabOverflow('g-reject');
});

// ------------------------------------------------------------------ 浮动窗口

 在 root 下挂浮动窗口元素；offsetWidth/Height 以 style getter 模拟即时回流。
function attachWindow(rootEl, windowId, startWidth, startHeight, style = {}) {
    const winEl = makeEl();
    winEl.attrs['data-dock-window'] = windowId;
    winEl.style = { left: '0px', top: '0px', ...style };
    Object.defineProperty(winEl, 'offsetWidth', {
        get: () => (winEl.style.width ? parseFloat(winEl.style.width) : startWidth),
    });
    Object.defineProperty(winEl, 'offsetHeight', {
        get: () => (winEl.style.height ? parseFloat(winEl.style.height) : startHeight),
    });
    rootEl.appendChild(winEl);
    return winEl;
}

test('手势起手静面：未登记 dock、窗口元素缺失皆静默不挂会话不写 body', () => {
    freshDoc();
    const dock = mountDock();
    dk.startWindowDrag('ghost-dock', 'w1', 10, 10, 7);
    dk.startWindowDrag(dock.dockId, 'w-missing', 10, 10, 7);
    dk.startWindowResize(dock.dockId, 'no-such-window', 10, 10, 7, 0, 0, 0, 0);
    assert.equal(doc.count('pointermove'), 0);
    assert.equal(doc.body.style.userSelect, undefined);
    dk.disposeDock(dock.dockId);
});

test('startWindowDrag 主链：偏移保持起拖相对、越负钳 0、up 提交 OnWindowMoved 读回 style', () => {
    freshDoc();
    const dock = mountDock(); // root 于原点
    const winEl = attachWindow(dock.rootEl, 'w1', 300, 200, { left: '50px', top: '30px' });
    dk.startWindowDrag(dock.dockId, 'w1', 60, 40, 7); // offsetX=10 offsetY=10
    assert.equal(doc.body.style.cursor, 'move');

    doc.dispatch('pointermove', pointer({ clientX: 80, clientY: 100 }));
    assert.equal(winEl.style.left, '70px');
    assert.equal(winEl.style.top, '90px');

    doc.dispatch('pointermove', pointer({ clientX: 0, clientY: 0 })); // 越负钳 0
    assert.equal(winEl.style.left, '0px');
    assert.equal(winEl.style.top, '0px');

    doc.dispatch('pointerup', pointer());
    assert.deepEqual(dock.ref.calls, [['OnWindowMoved', 'w1', 0, 0]]);
    assert.equal(doc.body.style.userSelect, '');
    dk.disposeDock(dock.dockId);
});

test('startWindowDrag cancel：窗口回位起拖坐标、零提交', () => {
    freshDoc();
    const dock = mountDock();
    const winEl = attachWindow(dock.rootEl, 'w1', 300, 200, { left: '50px', top: '30px' });
    dk.startWindowDrag(dock.dockId, 'w1', 60, 40, 7);
    doc.dispatch('pointermove', pointer({ clientX: 200, clientY: 200 }));
    doc.dispatch('pointercancel', pointer());
    assert.equal(winEl.style.left, '50px');
    assert.equal(winEl.style.top, '30px');
    assert.deepEqual(dock.ref.calls, []);
    dk.disposeDock(dock.dockId);
});

test('startWindowResize 缺省下限 180×120、max 0 不设上限：钳位写入、up 读 offsetWidth/Height 结算、cancel 回原尺寸', () => {
    freshDoc();
    const dock = mountDock();
    const winEl = attachWindow(dock.rootEl, 'w1', 300, 200);
    dk.startWindowResize(dock.dockId, 'w1', 400, 300, 7, 0, 0, 0, 0);
    assert.equal(doc.body.style.cursor, 'nwse-resize');

    doc.dispatch('pointermove', pointer({ clientX: 350, clientY: 280 })); // -50/-20
    assert.equal(winEl.style.width, '250px');
    assert.equal(winEl.style.height, '180px');

    doc.dispatch('pointermove', pointer({ clientX: 100, clientY: 100 })); // 越下限钳死
    assert.equal(winEl.style.width, '180px');
    assert.equal(winEl.style.height, '120px');

    doc.dispatch('pointermove', pointer({ clientX: 1000, clientY: 900 })); // 无上界放开
    assert.equal(winEl.style.width, '900px');
    assert.equal(winEl.style.height, '800px');

    doc.dispatch('pointerup', pointer());
    assert.deepEqual(dock.ref.calls, [['OnWindowResized', 'w1', 900, 800]]);

    // cancel 面：回原尺寸、零提交。
    const win2 = attachWindow(dock.rootEl, 'w2', 222, 111);
    dk.startWindowResize(dock.dockId, 'w2', 500, 500, 8, 0, 0, 0, 0);
    doc.dispatch('pointermove', pointer({ pointerId: 8, clientX: 600, clientY: 600 }));
    doc.dispatch('pointercancel', pointer({ pointerId: 8 }));
    assert.equal(win2.style.width, '222px');
    assert.equal(win2.style.height, '111px');
    assert.equal(dock.ref.calls.length, 1);
    dk.disposeDock(dock.dockId);
});

test('startWindowResize 声明上下限生效：min 取声明值、max 钳到上限不再越写', () => {
    freshDoc();
    const dock = mountDock();
    const winEl = attachWindow(dock.rootEl, 'w1', 300, 200);
    dk.startWindowResize(dock.dockId, 'w1', 400, 300, 7, 220, 150, 330, 240);

    doc.dispatch('pointermove', pointer({ clientX: 100, clientY: 100 })); // 越下限 → 220/150
    assert.equal(winEl.style.width, '220px');
    assert.equal(winEl.style.height, '150px');

    doc.dispatch('pointermove', pointer({ clientX: 900, clientY: 900 })); // 越上限 → 330/240
    assert.equal(winEl.style.width, '330px');
    assert.equal(winEl.style.height, '240px');

    doc.dispatch('pointerup', pointer());
    assert.deepEqual(dock.ref.calls, [['OnWindowResized', 'w1', 330, 240]]);
    dk.disposeDock(dock.dockId);
});

// ------------------------------------------------------------------ 桶接线

test('components-core.js barrel 已收口 dock 命名空间', async () => {
    const { readFile } = await import('node:fs/promises');
    const core = new URL('../../src/AtomBlazor.Components/wwwroot/js/components-core.js', import.meta.url);
    const text = await readFile(core, 'utf8');
    assert.match(text, /^import \* as dock from '\.\/dock\.js\?assets=1';$/m);
    assert.match(text, /^assertFresh\('dock\.js', dock, 'initializeDock'\);$/m);
    assert.match(text, /^export \{[\s\S]*\bdock,[\s\S]*\};$/m);
    assert.match(text, /（dock）已交付的模块。/);
});
