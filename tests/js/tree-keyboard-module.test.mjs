// tree-keyboard.js 的真实文件导入与行为测试。
// 模块顶层只建 Map（零 DOM 触碰），document/getComputedStyle/MutationObserver/Element
// 均在手柄体内访问，因此可在无 DOM 的 Node 环境以 file-URL 导入；模块无相对依赖。
// C# 侧调用点为 barrel 点分标识符 treeKeyboard.initialize/dispose（见 primitives.js 与
// PrimitiveJsModuleTests 的命名空间 camelCase 规则），本文件同时守护该导出面。
// DOM 交互沿用 slider-module.test.mjs 先例的最小元素桩；树桩由 makeTree 按
// container → treeitem → role="group" → treeitem 的层级拼装（parent/children 双向链接），
// closest 以微型选择器匹配器（标签 + [attr="v"] + :not([attr="v"])）沿父链上溯，
// dotNetRef 桩以变参记录 invokeMethodAsync 调用。

import assert from 'node:assert/strict';
import { test, afterEach } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Primitives/wwwroot/js/primitives/tree-keyboard.js', import.meta.url);

// 浏览器全局的最小桩：模块在手柄体内读取它们，模块求值期不触碰。
let direction = 'ltr';
globalThis.Element = class Element {};
globalThis.getComputedStyle = () => ({ direction });
let mutationCallback = null;
globalThis.MutationObserver = class {
    constructor(cb) { mutationCallback = cb; this.observed = false; }
    observe() { this.observed = true; }
    disconnect() { this.disconnected = true; }
};

// ---- 微型选择器匹配器：标签 + [attr="v"] + :not([attr="v"])，逗号分隔多选一 ----

function matchSimple(el, part) {
    const nots = [];
    const cleaned = part.replace(/:not\(\s*\[([^\]=]+)(?:="([^"]*)")?\]\s*\)/g,
        (_, attr, value) => { nots.push([attr, value]); return ''; });
    const m = cleaned.match(/^\s*([a-zA-Z][\w-]*)?\s*(\[.+?\])?\s*$/);
    if (!m) return false;
    const [, tag, attrPart] = m;
    if (tag && el.tagName !== tag.toLowerCase()) return false;
    if (attrPart) {
        const am = attrPart.match(/^\[([^\]=]+)(?:="([^"]*)")?\]$/);
        if (am) {
            const value = el.getAttribute(am[1]);
            if (am[2] === undefined ? value === null : value !== am[2]) return false;
        }
    }
    for (const [attr, value] of nots) {
        const actual = el.getAttribute(attr);
        // 仅存在性 :not([attr])：特性存在（任意值）才命中 not
        if (value === undefined ? actual !== null : actual === value) return false;
    }
    return true;
}

// ---- 元素桩 ----

function makeEl({ tag = 'div', attrs = {}, children = [], focusCalls = null } = {}) {
    const handlers = new Map();
    const el = new globalThis.Element();
    Object.assign(el, {
        tagName: tag.toLowerCase(),
        attrs,
        handlers,
        children: [],
        parent: null,
        hidden: false,
        focusCalls,
        focus() { if (this.focusCalls) this.focusCalls.push(this); },
        getAttribute(name) { return attrs[name] ?? null; },
        setAttribute(name, value) { attrs[name] = value; },
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
        contains(other) {
            let cur = other;
            while (cur) {
                if (cur === this) return true;
                cur = cur.parent;
            }
            return false;
        },
        selectAll(selector) {
            const parts = selector.split(',').map(p => p.trim());
            const hits = [];
            const walk = node => {
                for (const child of node.children ?? []) {
                    if (parts.some(part => matchSimple(child, part))) hits.push(child);
                    walk(child);
                }
            };
            walk(this);
            return hits;
        },
        querySelectorAll(selector) { return this.selectAll(selector); },
        querySelector(selector) { return this.selectAll(selector)[0] ?? null; },
        closest(selector) {
            const parts = selector.split(',').map(p => p.trim());
            let cur = this;
            while (cur) {
                if (parts.some(part => matchSimple(cur, part))) return cur;
                cur = cur.parent;
            }
            return null;
        },
    });
    for (const child of children) {
        child.parent = el;
        child.parentElement = el;
        el.children.push(child);
    }
    return el;
}

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

function keyEvent(key, overrides = {}) {
    return { key, preventDefault() { this.defaultPrevented = true; }, ...overrides };
}

// container → item-a（展开，有子） → group → item-b、item-c(禁用)
function makeTree({ itemCDisabled = true } = {}) {
    const itemB = makeEl({ tag: 'div', attrs: { role: 'treeitem', 'data-value': 'b', 'aria-level': '2' } });
    const itemC = makeEl({ tag: 'div', attrs: { role: 'treeitem', 'data-value': 'c', 'aria-disabled': itemCDisabled ? 'true' : null, 'aria-level': '2' } });
    const group = makeEl({ tag: 'div', attrs: { role: 'group' }, children: [itemB, itemC] });
    const itemA = makeEl({
        tag: 'div',
        attrs: { role: 'treeitem', 'data-value': 'a', 'aria-expanded': 'true', 'data-has-children': 'true', 'aria-level': '1' },
        children: [group],
    });
    const container = makeEl({ tag: 'div', attrs: { role: 'tree' }, children: [itemA] });
    return { container, itemA, itemB, itemC, group };
}

const ids = [];
let currentMod = null;

async function freshInitialize(container, dotNet, id) {
    currentMod ??= await import(moduleUrl.href);
    currentMod.initialize(container, dotNet, id);
    ids.push(id);
    return currentMod;
}

afterEach(() => {
    while (ids.length) currentMod.dispose(ids.pop());
    direction = 'ltr';
    mutationCallback = null;
});

// ------------------------------------------------------------------

test('tree-keyboard.js 可导入且导出面与 treeKeyboard. 点分标识符一一对应', async () => {
    const mod = await import(moduleUrl.href);

    // C# 侧调用点：treeKeyboard.initialize / treeKeyboard.dispose。
    for (const name of ['initialize', 'dispose']) {
        assert.equal(typeof mod[name], 'function', `tree-keyboard.js 缺少导出 ${name}`);
    }
});

test('initialize 对缺失入参静默返回（预渲染/半程释放容错）', async () => {
    const mod = await import(moduleUrl.href);

    mod.initialize(null, makeDotNet(), 'x');
    mod.initialize(makeEl(), null, 'x');

    mod.dispose('未知id'); // 不抛错即通过
});

test('initialize 挂载 keydown/click 监听器并注册 MutationObserver 与实例', async () => {
    const { container } = makeTree();
    const id = `tree-${ids.length}`;

    await freshInitialize(container, makeDotNet(), id);

    assert.equal(container.count('keydown'), 1);
    assert.equal(container.count('click'), 1);
});

test('dispose 摘除全部监听器并断开观察器', async () => {
    const mod = await import(moduleUrl.href);
    const { container } = makeTree();
    const id = 'tree-dispose';

    mod.initialize(container, makeDotNet(), id);
    assert.equal(container.count('keydown'), 1);

    mod.dispose(id);

    assert.equal(container.count('keydown'), 0);
    assert.equal(container.count('click'), 0);
});

test('ArrowDown/ArrowUp 在可见条目间移动焦点并跳过禁用项', async () => {
    const { container, itemA, itemB, itemC } = makeTree();
    const focusCalls = [];
    itemA.focusCalls = itemB.focusCalls = itemC.focusCalls = focusCalls;
    const dotNet = makeDotNet();
    await freshInitialize(container, dotNet, 'tree-arrowdown');

    container.dispatch('keydown', keyEvent('ArrowDown', { target: itemA }));
    assert.deepEqual(focusCalls, [itemB]); // a → b

    focusCalls.length = 0;
    container.dispatch('keydown', keyEvent('ArrowDown', { target: itemB }));
    assert.deepEqual(focusCalls, []); // b 的下一项 c 禁用，不再前进

    focusCalls.length = 0;
    container.dispatch('keydown', keyEvent('ArrowUp', { target: itemB }));
    assert.deepEqual(focusCalls, [itemA]); // b → a

    // 方向键一律 preventDefault
    const prevented = keyEvent('ArrowDown', { target: itemA });
    container.dispatch('keydown', prevented);
    assert.equal(prevented.defaultPrevented, true);
});

test('ArrowRight 展开折叠节点；已展开时移到第一个子节点', async () => {
    const { container, itemA, itemB } = makeTree();
    const dotNet = makeDotNet();
    await freshInitialize(container, dotNet, 'tree-arrowright');

    itemA.attrs['aria-expanded'] = 'false';
    container.dispatch('keydown', keyEvent('ArrowRight', { target: itemA }));
    assert.deepEqual(dotNet.calls, [['JsOnNodeExpand', 'a']]);

    dotNet.calls.length = 0;
    itemA.attrs['aria-expanded'] = 'true';
    const focusCalls = [];
    itemB.focusCalls = focusCalls;
    container.dispatch('keydown', keyEvent('ArrowRight', { target: itemA }));
    assert.deepEqual(dotNet.calls, []); // 已展开不再上报展开
    assert.deepEqual(focusCalls, [itemB]); // 落到第一个子节点
});

test('ArrowLeft 折叠已展开节点；折叠态移到父节点', async () => {
    const { container, itemA, itemB } = makeTree();
    const dotNet = makeDotNet();
    await freshInitialize(container, dotNet, 'tree-arrowleft');

    container.dispatch('keydown', keyEvent('ArrowLeft', { target: itemA }));
    assert.deepEqual(dotNet.calls, [['JsOnNodeCollapse', 'a']]);

    dotNet.calls.length = 0;
    const focusCalls = [];
    itemA.focusCalls = itemB.focusCalls = focusCalls;
    container.dispatch('keydown', keyEvent('ArrowLeft', { target: itemB }));
    assert.deepEqual(dotNet.calls, []);
    assert.deepEqual(focusCalls, [itemA]); // b → 父节点 a
});

test('RTL 下左右方向键互换：ArrowLeft 变为展开', async () => {
    direction = 'rtl';
    const { container, itemA } = makeTree();
    const dotNet = makeDotNet();
    await freshInitialize(container, dotNet, 'tree-rtl');

    itemA.attrs['aria-expanded'] = 'false';
    container.dispatch('keydown', keyEvent('ArrowLeft', { target: itemA }));

    assert.deepEqual(dotNet.calls, [['JsOnNodeExpand', 'a']]);
});

test('Enter 激活节点并携带 hasChildren 载荷上报', async () => {
    const { container, itemA, itemB } = makeTree();
    const dotNet = makeDotNet();
    await freshInitialize(container, dotNet, 'tree-enter');

    container.dispatch('keydown', keyEvent('Enter', { target: itemA }));
    assert.deepEqual(dotNet.calls, [['JsOnNodeActivate', 'a', true]]);

    dotNet.calls.length = 0;
    container.dispatch('keydown', keyEvent('Enter', { target: itemB }));
    assert.deepEqual(dotNet.calls, [['JsOnNodeActivate', 'b', false]]);

    // 禁用项吞掉激活
    dotNet.calls.length = 0;
    container.dispatch('keydown', keyEvent('Enter', { target: makeEl({ attrs: { role: 'treeitem', 'data-value': 'd', 'aria-disabled': 'true' } }) }));
    assert.deepEqual(dotNet.calls, []);
});

test('空格在可勾选条目上切换复选框，否则激活', async () => {
    const { container, itemA, itemB } = makeTree();
    const dotNet = makeDotNet();
    await freshInitialize(container, dotNet, 'tree-space');

    itemA.attrs['aria-checked'] = 'false';
    container.dispatch('keydown', keyEvent(' ', { target: itemA }));
    assert.deepEqual(dotNet.calls, [['JsOnNodeCheck', 'a']]);

    dotNet.calls.length = 0;
    container.dispatch('keydown', keyEvent(' ', { target: itemB }));
    assert.deepEqual(dotNet.calls, [['JsOnNodeActivate', 'b', false]]);
});

test('TreeSelect 模式下空格只在分支上导航开合、Enter 提交勾选', async () => {
    const { container, itemA, itemB } = makeTree();
    container.attrs['data-tree-select'] = 'true';
    const dotNet = makeDotNet();
    await freshInitialize(container, dotNet, 'tree-picker');

    itemB.attrs['aria-checked'] = 'false';
    // 展开的分支：空格折叠
    container.dispatch('keydown', keyEvent(' ', { target: itemA }));
    assert.deepEqual(dotNet.calls, [['JsOnNodeCollapse', 'a']]);
    dotNet.calls.length = 0;

    // 折叠的分支：空格展开
    itemA.attrs['aria-expanded'] = 'false';
    container.dispatch('keydown', keyEvent(' ', { target: itemA }));
    assert.deepEqual(dotNet.calls, [['JsOnNodeExpand', 'a']]);
    dotNet.calls.length = 0;

    // 叶子（无子）：空格不动值
    container.dispatch('keydown', keyEvent(' ', { target: itemB }));
    assert.deepEqual(dotNet.calls, []);
    dotNet.calls.length = 0;

    // Enter 在可勾选条目上提交勾选
    container.dispatch('keydown', keyEvent('Enter', { target: itemB }));
    assert.deepEqual(dotNet.calls, [['JsOnNodeCheck', 'b']]);
});

test('Home/End 分别聚焦第一个与最后一个可见可用条目', async () => {
    const { container, itemA, itemB, itemC } = makeTree();
    const focusCalls = [];
    itemA.focusCalls = itemB.focusCalls = itemC.focusCalls = focusCalls;
    await freshInitialize(container, makeDotNet(), 'tree-homeend');

    container.dispatch('keydown', keyEvent('Home', { target: itemB }));
    assert.deepEqual(focusCalls, [itemA]);

    // 末项 c 禁用，End 回落到 b
    focusCalls.length = 0;
    container.dispatch('keydown', keyEvent('End', { target: itemA }));
    assert.deepEqual(focusCalls, [itemB]);

    // c 启用后 End 直达末项
    focusCalls.length = 0;
    itemC.attrs['aria-disabled'] = null;
    container.dispatch('keydown', keyEvent('End', { target: itemA }));
    assert.deepEqual(focusCalls, [itemC]);
});

test('星号键上报展开兄弟节点', async () => {
    const { container, itemB } = makeTree();
    const dotNet = makeDotNet();
    await freshInitialize(container, dotNet, 'tree-star');

    container.dispatch('keydown', keyEvent('*', { target: itemB }));

    assert.deepEqual(dotNet.calls, [['JsOnExpandSiblings', 'b']]);
});

test('来自交互后代（含展开开关按钮）的按键不被拦截', async () => {
    const { container, itemA } = makeTree();
    const dotNet = makeDotNet();
    await freshInitialize(container, dotNet, 'tree-interactive');

    // 展开/折叠按钮本身带 data-tree-toggle，不属于交互黑名单——但这里
    // 验证 button 黑名单分支：普通按钮内的按键原样放行。
    const button = makeEl({ tag: 'button', attrs: {} });
    button.parent = itemA;
    itemA.children.push(button);

    const e = keyEvent('Enter', { target: button });
    container.dispatch('keydown', e);
    assert.deepEqual(dotNet.calls, []);
    assert.equal(e.defaultPrevented, undefined);
});

test('点击展开开关按当前状态上报展开/折叠', async () => {
    const { container, itemA } = makeTree();
    const dotNet = makeDotNet();
    await freshInitialize(container, dotNet, 'tree-click-toggle');

    const toggle = makeEl({ tag: 'button', attrs: { 'data-tree-toggle': 'true' } });
    toggle.parent = itemA;

    itemA.attrs['aria-expanded'] = 'true';
    container.dispatch('click', { target: toggle });
    assert.deepEqual(dotNet.calls, [['JsOnNodeCollapse', 'a']]);

    dotNet.calls.length = 0;
    itemA.attrs['aria-expanded'] = 'false';
    container.dispatch('click', { target: toggle });
    assert.deepEqual(dotNet.calls, [['JsOnNodeExpand', 'a']]);
});

test('点击复选框上报勾选切换；点击行主体上报激活并转移焦点', async () => {
    const { container, itemA, itemB } = makeTree();
    const dotNet = makeDotNet();
    await freshInitialize(container, dotNet, 'tree-click');
    const focusCalls = [];
    itemB.focusCalls = focusCalls;

    const checkbox = makeEl({ tag: 'button', attrs: { 'data-tree-checkbox': 'true' } });
    checkbox.parent = itemA;
    container.dispatch('click', { target: checkbox });
    assert.deepEqual(dotNet.calls, [['JsOnNodeCheck', 'a']]);

    dotNet.calls.length = 0;
    container.dispatch('click', { target: itemB });
    assert.deepEqual(dotNet.calls, [['JsOnNodeActivate', 'b', false]]);
    assert.deepEqual(focusCalls, [itemB]); // roving tabindex 转移到被点条目

    // 禁用条目吞掉点击
    dotNet.calls.length = 0;
    focusCalls.length = 0;
    container.dispatch('click', { target: makeEl({ attrs: { role: 'treeitem', 'data-value': 'd', 'aria-disabled': 'true' } }) });
    assert.deepEqual(dotNet.calls, []);
});

test('MutationObserver 回调在无可 Tab 条目时把首个可用可见项置为 tab 停靠点', async () => {
    const { container, itemA, itemB, itemC } = makeTree();
    await freshInitialize(container, makeDotNet(), 'tree-mutation');

    // a 是首个可用根，初始 syncTabStop 已把它置为唯一 tab 停靠点
    assert.equal(container.children[0].getAttribute('tabindex'), '0');

    // 模拟过滤把 a 的 tab 停靠点抹掉：全部条目 tabindex=-1 后触发观察器回调
    itemA.setAttribute('tabindex', '-1');
    itemB.setAttribute('tabindex', '-1');
    itemC.setAttribute('tabindex', '-1');

    assert.notEqual(mutationCallback, null);
    mutationCallback();

    assert.equal(itemA.getAttribute('tabindex'), '0'); // 首个可用可见项被重新立为停靠点
    assert.equal(itemB.getAttribute('tabindex'), '-1');
    assert.equal(itemC.getAttribute('tabindex'), '-1');
});
