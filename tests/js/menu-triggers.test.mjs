// menu-triggers.js 的真实文件导入与行为测试。
// 模块顶层只有 WeakMap 登记簿，零 DOM 触碰；document/MutationObserver/getComputedStyle
// 仅在 initialize 体内引用，可在无 DOM 的 Node 环境以 file-URL 导入；模块无相对依赖。
// 本模块按仓内 drag-updates.js/theme-scope.js 先例落 Primitives wwwroot/js 根级、
// 不入 primitives.js barrel：C# 侧为根级模块句柄式调用（initialize/dispose 裸标识符，
// 见 Menubar.razor 与基元层 NavigationMenu.razor），因此不做点分收口。
// DOM 桩沿用 scroll-area/keyboard-nav 先例的最小元素桩（父子链 + closest 上行 +
// 微型选择器匹配），并补 document/MutationObserver/getComputedStyle 桩。
// 不可测项登记：真实 MutationObserver 回调时序与原生焦点转移语义在纯桩环境下
// 只核对调用面，不伪造浏览器级断言。

import assert from 'node:assert/strict';
import { test } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Primitives/wwwroot/js/menu-triggers.js', import.meta.url);

// 微型选择器：支持 tag、[attr]、[attr="value"] 与 tag[attr="value"] 四型。
function parseSelector(sel) {
    const m = sel.match(/^(?:(?<tag>[a-zA-Z]+))?((?:\[[^\]=]+(?:="[^"]*")?\])*)$/);
    if (!m) throw new Error(`桩不支持选择器：${sel}`);
    const attrs = [...m[2].matchAll(/\[([^\]=]+)(?:="([^"]*)")?\]/g)];
    return { tag: m.groups.tag, attrs: attrs.map(a => [a[1], a[2]]) };
}

function matches(el, sel) {
    const { tag, attrs } = parseSelector(sel);
    if (tag && el.tag !== tag) return false;
    for (const [name, value] of attrs) {
        const actual = el.getAttribute(name);
        if (actual === null) return false;
        if (value !== undefined && actual !== value) return false;
    }
    return true;
}

function makeNode({ tag = 'div', attrs = {}, children = [], disabled = false } = {}) {
    const handlers = new Map();
    const el = {
        tag,
        attrs,
        children,
        disabled,
        parent: null,
        clickCalls: 0,
        focusCalls: 0,
        stopPropagationCalls: 0,
        getAttribute(name) { return attrs[name] ?? null; },
        setAttribute(name, value) { attrs[name] = String(value); },
        click() { el.clickCalls += 1; },
        focus() { el.focusCalls += 1; },
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
        closest(sel) {
            let node = el;
            while (node) {
                if (matches(node, sel)) return node;
                node = node.parent;
            }
            return null;
        },
        contains(node) {
            for (const child of el.children) {
                if (child === node || child.contains(node)) return true;
            }
            return false;
        },
        querySelectorAll(sel) {
            const found = [];
            const visit = node => {
                for (const child of node.children) {
                    if (matches(child, sel)) found.push(child);
                    visit(child);
                }
            };
            visit(el);
            return found;
        },
        querySelector(sel) { return el.querySelectorAll(sel)[0] ?? null; },
    };
    for (const child of children) child.parent = el;
    return el;
}

// 构建一枚导航菜单树：nav 根 + 两枚 li[data-nav-item]（各带 trigger 与 panel），
// 或 menubar 根 + 若干 aria-haspopup 按钮。
function buildNav({ rtl = false } = {}) {
    const mkItem = (value, items) => {
        const trigger = makeNode({ tag: 'button', attrs: { 'data-nav-trigger': '', 'aria-expanded': 'false' } });
        const panel = makeNode({ tag: 'div', attrs: { role: 'menu' }, children: items });
        return makeNode({ tag: 'li', attrs: { 'data-nav-item': value }, children: [trigger, panel] });
    };
    const menuItemsA = [
        makeNode({ tag: 'a', attrs: { role: 'menuitem' } }),
        makeNode({ tag: 'a', attrs: { role: 'menuitem' } }),
        makeNode({ tag: 'a', attrs: { role: 'menuitem', 'aria-disabled': 'true' } }),
    ];
    const menuItemsB = [makeNode({ tag: 'a', attrs: { role: 'menuitem' } })];
    const root = makeNode({ tag: 'nav', children: [mkItem('docs', menuItemsA), mkItem('blog', menuItemsB)] });
    globalThis.getComputedStyle = () => ({ direction: rtl ? 'rtl' : 'ltr' });
    return { root, menuItemsA, menuItemsB };
}

// 环境桩：document + MutationObserver（记录 disconnect）。
const observers = [];
globalThis.MutationObserver = class {
    constructor(cb) { this.cb = cb; observers.push(this); }
    observe() { this.observed = true; }
    disconnect() { this.disconnected = true; }
};

function makeDocument() {
    const handlers = new Map();
    return {
        handlers,
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
    };
}

globalThis.document = makeDocument();

const mt = await import(moduleUrl.href);

test('模块导出面：initialize 与 dispose 两枚函数', () => {
    assert.equal(typeof mt.initialize, 'function');
    assert.equal(typeof mt.dispose, 'function');
});

function keyOn(root, target, key) {
    const event = { key, target, preventDefaultCalls: 0, stopPropagationCalls: 0,
        stopPropagation() { this.stopPropagationCalls += 1; },
        preventDefault() { this.preventDefaultCalls += 1; } };
    root.dispatch('keydown', event);
    return event;
}

test('导航模式方向键在触发器间环绕，Home/End 落两端', () => {
    const { root } = buildNav();
    const triggers = root.querySelectorAll('[data-nav-trigger]');
    mt.initialize(root, true, true, null);

    keyOn(root, triggers[0], 'ArrowRight');
    assert.equal(triggers[1].focusCalls, 1);
    keyOn(root, triggers[1], 'ArrowRight');
    assert.equal(triggers[0].focusCalls, 1, '末位右移应环绕回首位');
    keyOn(root, triggers[0], 'ArrowLeft');
    assert.equal(triggers[1].focusCalls >= 2, true, '首位左移应环绕回末位');
    keyOn(root, triggers[1], 'Home');
    assert.equal(triggers[0].focusCalls >= 2, true);
    keyOn(root, triggers[0], 'End');
    assert.equal(triggers[1].focusCalls >= 3, true);
    mt.dispose(root);
});

test('RTL 下方向键左右语义互换', () => {
    const { root } = buildNav({ rtl: true });
    const triggers = root.querySelectorAll('[data-nav-trigger]');
    mt.initialize(root, true, true, null);
    keyOn(root, triggers[0], 'ArrowRight');
    assert.equal(triggers[1].focusCalls, 1, 'RTL 首位右移环绕到末位');
    mt.dispose(root);
});

test('enabled=false 时导航模式对方向键保持沉默', () => {
    const { root } = buildNav();
    const triggers = root.querySelectorAll('[data-nav-trigger]');
    mt.initialize(root, true, false, null);
    const event = keyOn(root, triggers[0], 'ArrowRight');
    assert.equal(event.preventDefaultCalls, 0);
    assert.equal(triggers[1].focusCalls, 0);
    mt.dispose(root);
});

test('ArrowDown 未展开先点击展开，再聚焦面板首个可用 menuitem', () => {
    const { root, menuItemsA } = buildNav();
    const trigger = root.querySelectorAll('[data-nav-trigger]')[0];
    mt.initialize(root, true, true, null);
    keyOn(root, trigger, 'ArrowDown');
    assert.equal(trigger.clickCalls, 1, '未展开时应点击展开');
    assert.equal(menuItemsA[0].focusCalls, 1, '应聚焦面板首个非禁用项');
    // 模拟点击生效后的展开沿：再次 ArrowDown 不应重复点击。
    trigger.attrs['aria-expanded'] = 'true';
    keyOn(root, trigger, 'ArrowDown');
    assert.equal(trigger.clickCalls, 1);
    mt.dispose(root);
});

test('ArrowUp 展开时直接落末位 menuitem', () => {
    const { root, menuItemsA } = buildNav();
    const trigger = root.querySelectorAll('[data-nav-trigger]')[0];
    trigger.attrs['aria-expanded'] = 'true';
    mt.initialize(root, true, true, null);
    keyOn(root, trigger, 'ArrowUp');
    assert.equal(trigger.clickCalls, 0, '已展开不应再点');
    assert.equal(menuItemsA[1].focusCalls, 1, '末位非禁用项应为第二枚（第三枚禁用）');
    mt.dispose(root);
});

test('面板内 Escape 回焦属主触发器并回调 JsCloseMenus', () => {
    const { root, menuItemsA } = buildNav();
    const trigger = root.querySelectorAll('[data-nav-trigger]')[0];
    const calls = [];
    const dotNetRef = { invokeMethodAsync: (name) => { calls.push(name); return Promise.resolve(); } };
    mt.initialize(root, true, true, dotNetRef);
    const event = keyOn(root, menuItemsA[0], 'Escape');
    assert.equal(event.preventDefaultCalls, 1);
    assert.equal(event.stopPropagationCalls, 1);
    assert.equal(trigger.focusCalls, 1);
    assert.deepEqual(calls, ['JsCloseMenus']);
    mt.dispose(root);
});

test('面板内方向键在 menuitem 间环绕并跳过禁用项之外沿', () => {
    const { root, menuItemsA } = buildNav();
    mt.initialize(root, true, true, null);
    keyOn(root, menuItemsA[1], 'ArrowDown');
    assert.equal(menuItemsA[0].focusCalls, 1, '第二位下移环绕回首位');
    keyOn(root, menuItemsA[0], 'ArrowUp');
    assert.equal(menuItemsA[1].focusCalls >= 1, true, '首位上移环绕回末位非禁用项');
    mt.dispose(root);
});

test('outside pointerdown 在导航模式回调 JsCloseMenus，根内目标不动作', () => {
    const { root, menuItemsA } = buildNav();
    const calls = [];
    const dotNetRef = { invokeMethodAsync: (name) => { calls.push(name); return Promise.resolve(); } };
    mt.initialize(root, true, true, dotNetRef);
    document.dispatch('pointerdown', { target: menuItemsA[0] });
    assert.deepEqual(calls, []);
    document.dispatch('pointerdown', { target: makeNode({}) });
    assert.deepEqual(calls, ['JsCloseMenus']);
    mt.dispose(root);
});

test('同一根重复 initialize 只更新 enabled 不重复挂监听', () => {
    const { root } = buildNav();
    mt.initialize(root, true, true, null);
    const before = root.count('keydown');
    mt.initialize(root, true, false, null);
    assert.equal(root.count('keydown'), before, '不应再挂第二枚 keydown');
    const triggers = root.querySelectorAll('[data-nav-trigger]');
    const event = keyOn(root, triggers[0], 'ArrowRight');
    assert.equal(event.preventDefaultCalls, 0, '更新后 enabled=false 应生效');
    mt.dispose(root);
});

test('menubar 模式展开态 Escape 点击并回焦触发器，且面板键由自家控制器管', () => {
    const itemBtn = makeNode({ tag: 'button', attrs: { 'aria-haspopup': 'menu', 'aria-expanded': 'true' } });
    const root = makeNode({ tag: 'div', attrs: { role: 'menubar' }, children: [itemBtn] });
    globalThis.document = makeDocument();
    mt.initialize(root, false, true, null);
    const event = keyOn(root, itemBtn, 'Escape');
    assert.equal(event.preventDefaultCalls, 1);
    assert.equal(itemBtn.clickCalls, 1, '展开态 Escape 应点击收起');
    assert.equal(itemBtn.focusCalls, 1);
    // 面板内键（目标不在触发器上）：menubar 模式直接 return。
    const stray = makeNode({ tag: 'div', attrs: { role: 'menu' } });
    const ev2 = keyOn(root, stray, 'ArrowDown');
    assert.equal(ev2.preventDefaultCalls, 0);
    mt.dispose(root);
});

test('menubar 模式 outside pointerdown 点击已展开的触发器', () => {
    const itemBtn = makeNode({ tag: 'button', attrs: { 'aria-haspopup': 'menu', 'aria-expanded': 'true' } });
    const root = makeNode({ tag: 'div', attrs: { role: 'menubar' }, children: [itemBtn] });
    globalThis.document = makeDocument();
    mt.initialize(root, false, true, null);
    document.dispatch('pointerdown', { target: makeNode({}) });
    assert.equal(itemBtn.clickCalls, 1);
    mt.dispose(root);
});

test('disabled 触发器不进入方向键列表', () => {
    const disabledBtn = makeNode({ tag: 'button', attrs: { 'data-nav-trigger': '' }, disabled: true });
    const liveBtn = makeNode({ tag: 'button', attrs: { 'data-nav-trigger': '', 'aria-expanded': 'false' } });
    const li1 = makeNode({ tag: 'li', attrs: { 'data-nav-item': 'a' }, children: [disabledBtn] });
    const li2 = makeNode({ tag: 'li', attrs: { 'data-nav-item': 'b' }, children: [liveBtn] });
    const root = makeNode({ tag: 'nav', children: [li1, li2] });
    mt.initialize(root, true, true, null);
    const event = keyOn(root, disabledBtn, 'ArrowRight');
    assert.equal(event.preventDefaultCalls, 0, '禁用触发器不在列表内，键应落空');
    mt.dispose(root);
});

test('dispose 摘除 keydown/pointerdown 监听并断开 MutationObserver', () => {
    const { root } = buildNav();
    const observerCountBefore = observers.length;
    mt.initialize(root, true, true, null);
    assert.equal(observers.length, observerCountBefore + 1);
    const observer = observers.at(-1);
    assert.equal(root.count('keydown'), 1);
    assert.equal(document.count('pointerdown'), 1);
    mt.dispose(root);
    assert.equal(root.count('keydown'), 0);
    assert.equal(document.count('pointerdown'), 0);
    assert.equal(observer.disconnected, true);
 // 对未登记根 dispose 是安全的空操作。
    mt.dispose(makeNode({}));
});

test('C# 侧根级句柄调用点照旧非点分（登记：Menubar.razor 与基元层 NavigationMenu.razor）', () => {
    // 本模块不入 primitives.js barrel（根级独立文件，照 drag-updates.js/theme-scope.js
    // 先例）；调用点以句柄式裸标识符 "initialize"/"dispose" 派发。此处仅锁定
    // primitives.js barrel 未把它误收——若误收，本仓 PrimitiveJsModuleTests 亦会因
    // TheBundleReExportsEveryPrimitiveModule 目录界定而放行（根级不在扫描面），
 // 故在此登记双向核对。
    return import('node:fs/promises').then(({ readFile }) =>
        readFile(new URL('../../src/AtomBlazor.Primitives/wwwroot/js/primitives/primitives.js', import.meta.url), 'utf8')
            .then(text => assert.doesNotMatch(text, /menu-triggers/, 'menu-triggers.js 不应入 primitives.js barrel')));
});
