// cascader.js 的真实文件导入与行为测试。
// 模块顶层只建 Map 与选择器字符串（零 DOM 触碰），document/getComputedStyle/
// MutationObserver/requestAnimationFrame 都在调用时才触碰，因此可在无 DOM 的
// Node 环境以 file-URL 导入，调用前挂全局桩。
// C# 侧调用点为 barrel 点分标识符 cascader.initialize/connect/dispose（见
// components-core.js 与 PrimitiveJsModuleTests 的命名空间 camelCase 规则），
// 本文件同时守护该导出面。
// DOM 交互沿用 tag-input-module.test.mjs 先例的最小元素桩（补 closest/querySelector
// 与 dataset 归一），门户/状态祖先按基元 Popover 的 data-atom-portal 与 data-state 语义。

import assert from 'node:assert/strict';
import { test, beforeEach } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/cascader.js', import.meta.url);

// ── 最小 DOM 桩 ────────────────────────────────────────────────────────────

class StubEl {
    constructor(attrs = {}, children = []) {
        this.attrs = attrs;
        this.children = [];
        this.parent = null;
        for (const child of children) this.appendChild(child);
        this.handlers = new Map();
        this.tabIndex = -1;
        this.disabled = false;
        this.isConnected = true;
        this.clicks = 0;
        this.focuses = 0;
        this.scrolls = 0;
        this.scrollLeft = 0;
        this.scrollWidth = 100;
    }

    appendChild(child) {
        child.parent = this;
        this.children.push(child);
        return child;
    }

    // data-* 属性 → camelCase dataset（浏览器语义的桩化子集）
    get dataset() {
        const out = {};
        for (const [name, value] of Object.entries(this.attrs)) {
            if (!name.startsWith('data-')) continue;
            const key = name.slice(5).replace(/-(\w)/g, (_, c) => c.toUpperCase());
            out[key] = value;
        }
        return out;
    }

    getAttribute(name) { return this.attrs[name] ?? null; }
    hasAttribute(name) { return name in this.attrs; }

    addEventListener(type, fn) {
        if (!this.handlers.has(type)) this.handlers.set(type, []);
        this.handlers.get(type).push(fn);
    }

    removeEventListener(type, fn) {
        const list = this.handlers.get(type) ?? [];
        const index = list.indexOf(fn);
        if (index !== -1) list.splice(index, 1);
    }

    dispatch(type, event = {}) {
        for (const fn of [...(this.handlers.get(type) ?? [])]) fn(event);
    }

    count(type) { return (this.handlers.get(type) ?? []).length; }

    click() { this.clicks++; }
    focus() { this.focuses++; globalThis.document.activeElement = this; }
    scrollIntoView() { this.scrolls++; }

    matches(sel) {
        const m = /^\[([\w-]+)(?:="([^"]*)")?\]$/.exec(sel);
        if (!m) throw new Error(`桩不支持选择器：${sel}`);
        if (!(m[1] in this.attrs)) return false;
        return m[2] === undefined || this.attrs[m[1]] === m[2];
    }

    closest(sel) {
        for (let node = this; node; node = node.parent) {
            if (node.matches(sel)) return node;
        }
        return null;
    }

    descendants() {
        const out = [];
        for (const child of this.children) {
            out.push(child, ...child.descendants());
        }
        return out;
    }

    querySelectorAll(sel) { return this.descendants().filter(el => el.matches(sel)); }
    querySelector(sel) { return this.querySelectorAll(sel)[0] ?? null; }
}

function keyEvent(key, target, overrides = {}) {
    return {
        key,
        target,
        altKey: false,
        ctrlKey: false,
        metaKey: false,
        isComposing: false,
        preventDefault() { this.defaultPrevented = true; },
        ...overrides,
    };
}

class FakeMutationObserver {
    constructor(callback) {
        this.callback = callback;
        this.observed = [];
        this.disconnected = false;
        FakeMutationObserver.instances.push(this);
    }

    observe(target, options) { this.observed.push({ target, options }); }
    disconnect() { this.disconnected = true; }
}
FakeMutationObserver.instances = [];

let idSeq = 0;
const registry = new Map();

/// 挂全局桩：document / getComputedStyle / MutationObserver / requestAnimationFrame。
beforeEach(() => {
    FakeMutationObserver.instances.length = 0;
    registry.clear();
    globalThis.document = {
        activeElement: null,
        getElementById: id => registry.get(id) ?? null,
    };
    globalThis.getComputedStyle = () => ({ direction: 'ltr' });
    globalThis.MutationObserver = FakeMutationObserver;
    globalThis.requestAnimationFrame = fn => fn();
});

/// 构造 popup（id 已注册）+ 门户/状态祖先 + 指定列与项的滚动视口。
function makePopup({ items = [], columns = null, search = false, selected = null, state = 'open' }) {
    const id = `atom-cascader-${idSeq++}`;
    const portal = new StubEl({ 'data-atom-portal': 'true', 'data-state': state });
    const popup = new StubEl({ id });
    portal.appendChild(popup);
    registry.set(id, popup);

    const viewport = new StubEl({ 'data-cascader-scroll': '' });
    popup.appendChild(viewport);
    if (search) {
        popup.appendChild(new StubEl({ 'data-cascader-search': 'true' }));
    }

    const built = columns ?? [{ parent: null, items }];
    built.forEach((column, depth) => {
        const attrs = { 'data-cascader-column': String(depth) };
        if (column.parent) attrs['data-cascader-parent'] = column.parent;
        const el = new StubEl(attrs);
        viewport.appendChild(el);
        for (const spec of column.items) {
            const itemAttrs = { 'data-cascader-item': spec.key };
            if (spec.expanded !== undefined) itemAttrs['aria-expanded'] = spec.expanded;
            if (spec.key === selected) itemAttrs['aria-current'] = 'true';
            el.appendChild(new StubEl(itemAttrs));
        }
    });

    return { id, popup, portal, viewport, searchEl: popup.querySelector('[data-cascader-search]') };
}

// ── 导出面 ────────────────────────────────────────────────────────────────

test('cascader.js 可导入且导出面与 cascader. 点分标识符一一对应', async () => {
    const mod = await import(moduleUrl);
    // C# 侧调用点：cascader.initialize / connect / dispose。
    for (const name of ['initialize', 'connect', 'dispose']) {
        assert.equal(typeof mod[name], 'function', `cascader.js 缺少导出 ${name}`);
    }
});

// ── initialize：触发器键盘沿 ──────────────────────────────────────────────

test('initialize 给触发器挂 keydown；方向键 preventDefault 并点击触发器', async () => {
    const mod = await import(moduleUrl);
    const trigger = new StubEl({ 'data-cascader-trigger': 'true' });
    const container = new StubEl({});
    container.appendChild(trigger);

    mod.initialize(container, 'init-1');
    assert.equal(trigger.count('keydown'), 1);

    const down = keyEvent('ArrowDown', trigger);
    trigger.dispatch('keydown', down);
    assert.ok(down.defaultPrevented);
    assert.equal(trigger.clicks, 1);

    const up = keyEvent('ArrowUp', trigger);
    trigger.dispatch('keydown', up);
    assert.equal(trigger.clicks, 2);
});

test('initialize 放行无关按键/修饰键/禁用态，缺失触发器静默返回', async () => {
    const mod = await import(moduleUrl);
    const trigger = new StubEl({ 'data-cascader-trigger': 'true' });
    const container = new StubEl({});
    container.appendChild(trigger);
    mod.initialize(container, 'init-2');

    trigger.dispatch('keydown', keyEvent('Enter', trigger));
    assert.equal(trigger.clicks, 0);

    trigger.dispatch('keydown', keyEvent('ArrowDown', trigger, { ctrlKey: true }));
    assert.equal(trigger.clicks, 0);

    trigger.disabled = true;
    trigger.dispatch('keydown', keyEvent('ArrowDown', trigger));
    assert.equal(trigger.clicks, 0);

    // 容器里没有触发器：不抛错即可
    mod.initialize(new StubEl({}), 'init-3');
});

// ── connect：漫游 tabindex 与列内导航 ─────────────────────────────────────

test('connect 建立漫游 tabindex：aria-current 优先，否则首个项', async () => {
    const mod = await import(moduleUrl);
    const a = { key: 'a' };
    const b = { key: 'b' };
    const { id, popup } = makePopup({ items: [a, b], selected: 'b' });

    mod.connect(id);
    const items = popup.querySelectorAll('[data-cascader-item]');
    assert.equal(items[1].tabIndex, 0); // aria-current="true" 的 b 为入口
    assert.equal(items[0].tabIndex, -1);

    const second = makePopup({ items: [a, b] });
    mod.connect(second.id);
    const items2 = second.popup.querySelectorAll('[data-cascader-item]');
    assert.equal(items2[0].tabIndex, 0); // 无选中：首个项为入口
    assert.equal(items2[1].tabIndex, -1);
});

test('connect 注册 MutationObserver 并按 aria-expanded/aria-current 属性过滤', async () => {
    const mod = await import(moduleUrl);
    const { id, popup } = makePopup({ items: [{ key: 'a' }] });
    mod.connect(id);

    const observer = FakeMutationObserver.instances.at(-1);
    assert.equal(observer.observed.length, 1);
    assert.equal(observer.observed[0].target, popup);
    assert.deepEqual(observer.observed[0].options.attributeFilter, ['aria-expanded', 'aria-current']);
    assert.equal(popup.count('keydown'), 1);
    assert.equal(popup.count('focusin'), 1);
    // 门户（data-atom-portal 祖先）接 atom:visible 以在浮层显现时定位滚动
    assert.equal(popup.parent.hasAttribute('data-atom-portal'), true);
    assert.equal(popup.parent.count('atom:visible'), 1);
});

test('列内 ArrowDown/ArrowUp 环绕移焦，Home/End 直达首末', async () => {
    const mod = await import(moduleUrl);
    const { id, popup } = makePopup({ items: [{ key: 'a' }, { key: 'b' }, { key: 'c' }] });
    mod.connect(id);
    const items = popup.querySelectorAll('[data-cascader-item]');

    const down = keyEvent('ArrowDown', items[2]);
    popup.dispatch('keydown', down);
    assert.ok(down.defaultPrevented);
    assert.equal(items[0].focuses, 1); // 末项下移环绕回首项

    const end = keyEvent('End', items[0]);
    popup.dispatch('keydown', end);
    assert.equal(items[2].focuses, 1);

    const home = keyEvent('Home', items[2]);
    popup.dispatch('keydown', home);
    assert.equal(items[0].focuses, 2);
});

test('Enter/空格：叶子直接点击，分支激活进入', async () => {
    const mod = await import(moduleUrl);
    const { id, popup } = makePopup({
        items: [{ key: 'branch', expanded: 'true' }, { key: 'leaf' }],
    });
    mod.connect(id);
    const [branch, leaf] = popup.querySelectorAll('[data-cascader-item]');

    const enter = keyEvent('Enter', leaf);
    popup.dispatch('keydown', enter);
    assert.ok(enter.defaultPrevented);
    assert.equal(leaf.clicks, 1);

    // 分支（aria-expanded="true"）：activate=true 一律点击一次，并设 pendingChild 等待子列
    const enterBranch = keyEvent(' ', branch);
    popup.dispatch('keydown', enterBranch);
    assert.ok(enterBranch.defaultPrevented);
    assert.equal(branch.clicks, 1);
});

test('ArrowRight 进入未展开分支（点击），ArrowLeft 回焦父列或搜索框', async () => {
    const mod = await import(moduleUrl);
    const { id, popup, searchEl } = makePopup({
        items: [{ key: 'branch', expanded: 'false' }, { key: 'leaf' }],
        search: true,
    });
    mod.connect(id);
    const [branch, leaf] = popup.querySelectorAll('[data-cascader-item]');

    const right = keyEvent('ArrowRight', branch);
    popup.dispatch('keydown', right);
    assert.ok(right.defaultPrevented);
    assert.equal(branch.clicks, 1); // 未展开：点击进入

    const rightLeaf = keyEvent('ArrowRight', leaf);
    popup.dispatch('keydown', rightLeaf);
    assert.equal(leaf.clicks, 0); // 叶子无右侧语义

    // 叶子在父键不匹配的列里：回焦落到搜索框
    const back = keyEvent('ArrowLeft', leaf);
    popup.dispatch('keydown', back);
    assert.ok(back.defaultPrevented);
    assert.equal(searchEl.focuses, 1);
});

test('ArrowLeft 在带父键的子列回焦父项', async () => {
    const mod = await import(moduleUrl);
    const { id, popup } = makePopup({
        columns: [
            { parent: null, items: [{ key: 'p', expanded: 'true' }] },
            { parent: 'p', items: [{ key: 'c' }] },
        ],
    });
    mod.connect(id);
    const parent = popup.querySelector('[data-cascader-item="p"]');
    const child = popup.querySelector('[data-cascader-item="c"]');

    popup.dispatch('keydown', keyEvent('ArrowLeft', child));
    assert.equal(parent.focuses, 1);
});

test('搜索框 ArrowDown 进入首列、ArrowUp 进末项、Enter 点击首项', async () => {
    const mod = await import(moduleUrl);
    const { id, popup, searchEl } = makePopup({
        items: [{ key: 'a' }, { key: 'b' }],
        search: true,
    });
    mod.connect(id);
    const items = popup.querySelectorAll('[data-cascader-item]');

    // 事件在真实 DOM 里冒泡到 popup（监听器挂在 popup 上），桩以 popup.dispatch 模拟
    popup.dispatch('keydown', keyEvent('ArrowDown', searchEl));
    assert.equal(items[0].focuses, 1);

    popup.dispatch('keydown', keyEvent('ArrowUp', searchEl));
    assert.equal(items[1].focuses, 1);

    const enter = keyEvent('Enter', searchEl);
    popup.dispatch('keydown', enter);
    assert.ok(enter.defaultPrevented); // 不提交外层表单
    assert.equal(items[0].clicks, 1);
});

test('focusin 重排漫游 tabindex', async () => {
    const mod = await import(moduleUrl);
    const { id, popup } = makePopup({ items: [{ key: 'a' }, { key: 'b' }] });
    mod.connect(id);
    const [a, b] = popup.querySelectorAll('[data-cascader-item]');

    popup.dispatch('focusin', { target: b });
    assert.equal(b.tabIndex, 0);
    assert.equal(a.tabIndex, -1);
});

test('浮层未开启（data-state 非 open 或未连接）时导航全部吞下', async () => {
    const mod = await import(moduleUrl);
    const { id, popup } = makePopup({ items: [{ key: 'a' }, { key: 'b' }] });
    mod.connect(id);
    const items = popup.querySelectorAll('[data-cascader-item]');

    popup.parent.attrs['data-state'] = 'closed';
    const down = keyEvent('ArrowDown', items[0]);
    popup.dispatch('keydown', down);
    assert.ok(!down.defaultPrevented);
    assert.equal(items[1].focuses, 0);
});

test('dispose 摘除监听、断开观察器并清空实例状态', async () => {
    const mod = await import(moduleUrl);
    const trigger = new StubEl({ 'data-cascader-trigger': 'true' });
    const container = new StubEl({});
    container.appendChild(trigger);
    mod.initialize(container, 'disp-1');

    const { id, popup } = makePopup({ items: [{ key: 'a' }, { key: 'b' }] });
    mod.connect(id);
    const items = popup.querySelectorAll('[data-cascader-item]');

    mod.dispose(id);
    assert.equal(popup.count('keydown'), 0);
    assert.equal(popup.count('focusin'), 0);
    assert.equal(popup.parent.count('atom:visible'), 0); // 门户监听同步摘除
    assert.ok(FakeMutationObserver.instances.at(-1).disconnected);

    // 触发器监听也被释放：键盘事件不再触发点击
    mod.dispose('disp-1');
    trigger.dispatch('keydown', keyEvent('ArrowDown', trigger));
    assert.equal(trigger.clicks, 0);

    // 释放后再按箭头不报错、不移焦
    popup.dispatch('keydown', keyEvent('ArrowDown', items[0]));
    assert.equal(items[1].focuses, 0);

    // 未知 id 的 dispose 无害
    mod.dispose('不存在');
});
