// keyboard-nav.js 的真实文件导入与行为测试。
// 模块顶层零 DOM 触碰（只有函数声明），document.activeElement 仅在 navigate* 手柄体内读取，
// 因此可在无 DOM 的 Node 环境以 file-URL 导入；模块无相对依赖。
// C# 侧调用点为 barrel 点分标识符 keyboardNav.setupKeyboardNav（TabsList 基元挂载与释放，
// 见 PrimitiveTabsTests），navigate*/setupListBox 为菜单/ListBox 侧的其余导出面。
// DOM 桩沿用 tree-keyboard-module.test.mjs 先例的最小元素桩（addEventListener/
// removeEventListener/dispatch/count + focus 记录 + 微型选择器匹配）。

import assert from 'node:assert/strict';
import { test } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Primitives/wwwroot/js/primitives/keyboard-nav.js', import.meta.url);

// 微型选择器匹配：仅 [attr="v"] 形态（本模块的选择器只有 role 特性一类）。
function matchSimple(el, part) {
    const m = part.match(/^\s*\[([^\]=]+)(?:="([^"]*)")?\]\s*$/);
    if (!m) return false;
    const value = el.getAttribute(m[1]);
    return m[2] === undefined ? value !== null : value === m[2];
}

function makeEl({ attrs = {}, children = [] } = {}) {
    const handlers = new Map();
    const focusLog = [];
    const el = {
        attrs,
        handlers,
        children,
        focusLog,
        focus() { focusLog.push(el); },
        getAttribute(name) { return attrs[name] ?? null; },
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
        querySelectorAll(selector) {
            const parts = selector.split(',').map(p => p.trim());
            const hits = [];
            const walk = node => {
                for (const child of node.children ?? []) {
                    if (parts.some(part => matchSimple(child, part))) hits.push(child);
                    walk(child);
                }
            };
            walk(el);
            return hits;
        },
    };
    return el;
}

function keyEvent(key, overrides = {}) {
    return { key, preventDefault() { this.defaultPrevented = true; }, ...overrides };
}

// 三条菜单项：b 带 aria-disabled=true（用于跳过断言）。
function makeMenu() {
    const a = makeEl({ attrs: { role: 'menuitem' } });
    const b = makeEl({ attrs: { role: 'menuitemcheckbox', 'aria-disabled': 'true' } });
    const c = makeEl({ attrs: { role: 'menuitemradio' } });
    const container = makeEl({ children: [a, b, c] });
    return { container, a, b, c };
}

// ------------------------------------------------------------------

test('keyboard-nav.js 可导入且导出面覆盖 C# 点分标识符与菜单/列表导航族', async () => {
    const mod = await import(moduleUrl.href);

    // TabsList 基元调用点：keyboardNav.setupKeyboardNav；其余为既有消费方的导出面。
    for (const name of ['setupKeyboardNav', 'navigateNext', 'navigatePrevious',
        'navigateFirst', 'navigateLast', 'setupListBox']) {
        assert.equal(typeof mod[name], 'function', `keyboard-nav.js 缺少导出 ${name}`);
    }
});

test('setupKeyboardNav 对缺失元素返回空句柄且不抛错（预渲染容错）', async () => {
    const mod = await import(moduleUrl.href);

    const handle = mod.setupKeyboardNav(null);
    assert.equal(typeof handle.dispose, 'function');
    handle.dispose(); // 幂等
});

test('setupKeyboardNav 挂 keydown 并只对导航按键阻止默认滚动', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeEl();

    mod.setupKeyboardNav(element);
    assert.equal(element.count('keydown'), 1);

    for (const key of ['ArrowDown', 'ArrowUp', 'Home', 'End', 'PageUp', 'PageDown']) {
        const event = keyEvent(key);
        element.dispatch('keydown', event);
        assert.equal(event.defaultPrevented, true, `${key} 应被阻止默认行为`);
    }

    for (const key of ['Tab', 'Enter', 'a']) {
        const event = keyEvent(key);
        element.dispatch('keydown', event);
        assert.notEqual(event.defaultPrevented, true, `${key} 不应被阻止默认行为`);
    }
});

test('setupKeyboardNav 的 dispose 摘除监听器且重复调用幂等', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeEl();

    const handle = mod.setupKeyboardNav(element);
    handle.dispose();
    assert.equal(element.count('keydown'), 0);

    handle.dispose(); // 二次 dispose 不抛错
    assert.equal(element.count('keydown'), 0);

    const event = keyEvent('Home');
    element.dispatch('keydown', event);
    assert.notEqual(event.defaultPrevented, true); // 摘除后不再拦截
});

test('navigateNext 按 DOM 序前进并跳过禁用项，末位回绕、loop=false 钳住', async () => {
    const mod = await import(moduleUrl.href);
    const { container, a, c } = makeMenu();
    globalThis.document = { activeElement: null };

    // activeElement 不在集合内 → 前进到第一项。
    assert.equal(mod.navigateNext(container), a);

    globalThis.document.activeElement = a;
    // b 禁用：a 的下一可用项是 c。
    assert.equal(mod.navigateNext(container), c);

    // c 已是末位：默认回绕到 a。
    globalThis.document.activeElement = c;
    assert.equal(mod.navigateNext(container), a);

    // loop=false 且不在末位仍前进；到末位后钳住。
    globalThis.document.activeElement = a;
    assert.equal(mod.navigateNext(container, false), c);
    globalThis.document.activeElement = c;
    assert.equal(mod.navigateNext(container, false), c);

    // 空容器返回 null。
    assert.equal(mod.navigateNext(makeEl()), null);
});

test('navigatePrevious 首位回绕到末位，loop=false 钳住，未命中取末位', async () => {
    const mod = await import(moduleUrl.href);
    const { container, a, c } = makeMenu();
    globalThis.document = { activeElement: null };

    // activeElement 不在集合内 → 上一项取末位。
    assert.equal(mod.navigatePrevious(container), c);

    globalThis.document.activeElement = c;
    assert.equal(mod.navigatePrevious(container), a); // 跳过禁用的 b

    // a 是首位：默认回绕到 c。
    globalThis.document.activeElement = a;
    assert.equal(mod.navigatePrevious(container), c);

    globalThis.document.activeElement = a;
    assert.equal(mod.navigatePrevious(container, false), a); // 钳住首位

    assert.equal(mod.navigatePrevious(makeEl()), null);
});

test('navigateFirst 与 navigateLast 直达首末可用项', async () => {
    const mod = await import(moduleUrl.href);
    const { container, a, c } = makeMenu();
    globalThis.document = { activeElement: null };

    assert.equal(mod.navigateFirst(container), a);
    assert.equal(mod.navigateLast(container), c);
    assert.equal(mod.navigateFirst(makeEl()), null);
    assert.equal(mod.navigateLast(makeEl()), null);

    // 焦点确实落到了返回的元素上。
    assert.deepEqual(a.focusLog, [a]);
    assert.deepEqual(c.focusLog, [c]);
});

test('setupListBox 拦截导航与选择键，Ctrl+A 仅在多选集合允许', async () => {
    const mod = await import(moduleUrl.href);

    const single = makeEl();
    const handle = mod.setupListBox(single);
    for (const key of ['ArrowDown', 'ArrowUp', 'Home', 'End', 'PageUp', 'PageDown', ' ', 'Enter']) {
        const event = keyEvent(key);
        single.dispatch('keydown', event);
        assert.equal(event.defaultPrevented, true, `单选列表应阻止 ${key}`);
    }
    const plainA = keyEvent('a', { ctrlKey: true });
    single.dispatch('keydown', plainA);
    assert.notEqual(plainA.defaultPrevented, true); // 单选时 Ctrl+A 不接管

    const multi = makeEl({ attrs: { 'aria-multiselectable': 'true' } });
    mod.setupListBox(multi);
    const selectAll = keyEvent('a', { ctrlKey: true });
    multi.dispatch('keydown', selectAll);
    assert.equal(selectAll.defaultPrevented, true);
    const metaSelectAll = keyEvent('a', { metaKey: true });
    multi.dispatch('keydown', metaSelectAll);
    assert.equal(metaSelectAll.defaultPrevented, true);

    handle.dispose();
    assert.equal(single.count('keydown'), 0);
});

test('setupListBox 对 aria-disabled 容器完全不拦截，dispose 后不再挂键', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeEl({ attrs: { 'aria-disabled': 'true' } });

    const handle = mod.setupListBox(element);
    const event = keyEvent('ArrowDown');
    element.dispatch('keydown', event);
    assert.notEqual(event.defaultPrevented, true);

    handle.dispose();
    handle.dispose(); // 幂等
    assert.equal(element.count('keydown'), 0);

    // 缺失元素同样返回空句柄。
    mod.setupListBox(null).dispose();
});
