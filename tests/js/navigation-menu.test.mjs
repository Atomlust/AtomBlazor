// navigation-menu.js 的真实文件导入与行为测试。
// 模块顶层零 DOM 触碰（纯函数集），window.getComputedStyle/document.activeElement
// 仅在函数体内引用，可在无 DOM 的 Node 环境以 file-URL 导入；模块无相对依赖。
// C# 侧调用点为 barrel 点分标识符 navigationMenu.navigateFirst/
// navigationMenu.setupKeyboardNavigation/navigationMenu.focusElement
// （NavigationMenuContent/NavigationMenuTrigger，见 NavigationMenuTests）；
// setupKeyboardNavigation 返回的清理句柄调用不点分（裸 "dispose"）。
// DOM 桩沿用 scroll-area/keyboard-nav 先例的最小元素桩（焦点记录 + 微型选择器匹配），
// 并补 window.getComputedStyle/document.activeElement 桩。
// 不可测项登记：真实样式重排与浏览器焦点转移语义在纯桩环境下只核对调用面，
// 不伪造浏览器级断言。

import assert from 'node:assert/strict';
import { test } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/navigation-menu.js', import.meta.url);

// 微型选择器匹配：仅 a[href] 与 [role="menuitem"] 两型（本模块只用这一组合）。
function matchSimple(el, part) {
    part = part.trim();
    if (part === 'a[href]') return el.tag === 'a' && el.getAttribute('href') !== null;
    const m = part.match(/^\s*\[role="([\w-]+)"\]\s*$/);
    if (m) return el.getAttribute('role') === m[1];
    return false;
}

function makeEl({ tag = 'div', attrs = {}, children = [], style = {} } = {}) {
    const handlers = new Map();
    const el = {
        tag,
        attrs,
        children,
        style,
        hidden: false,
        focusCalls: 0,
        handlers,
        getAttribute(name) { return attrs[name] ?? null; },
        focus() {
            el.focusCalls += 1;
            globalThis.document.activeElement = el;
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
        querySelectorAll(selector) {
            const parts = selector.split(',');
            const found = [];
            const visit = node => {
                for (const child of node.children ?? []) {
                    if (parts.some(p => matchSimple(child, p))) found.push(child);
                    visit(child);
                }
            };
            visit(el);
            return found;
        },
    };
    return el;
}

function link(attrs = {}) { return makeEl({ tag: 'a', attrs: { href: '/x', ...attrs } }); }
function makeContainer(items) { return makeEl({ children: items }); }

globalThis.document = { activeElement: null };
globalThis.window = {
    getComputedStyle: el => ({ display: el.style.display ?? 'block', visibility: el.style.visibility ?? 'visible' }),
};

const nav = await import(moduleUrl.href);

test('模块导出面齐全（六个函数）', () => {
    for (const n of ['navigateNext', 'navigatePrevious', 'navigateFirst', 'navigateLast',
        'focusElement', 'setupKeyboardNavigation']) {
        assert.equal(typeof nav[n], 'function', `${n} 应为函数`);
    }
});

test('navigateFirst 聚焦首个可聚焦项，空容器返回 null', () => {
    const a = link(), b = link();
    assert.equal(nav.navigateFirst(makeContainer([a, b])), a);
    assert.equal(a.focusCalls, 1);
    assert.equal(nav.navigateFirst(makeContainer([])), null);
    assert.equal(nav.navigateFirst(null), null);
});

test('navigateLast 聚焦末位可聚焦项', () => {
    const a = link(), b = link();
    assert.equal(nav.navigateLast(makeContainer([a, b])), b);
    assert.equal(b.focusCalls, 1);
    assert.equal(nav.navigateLast(makeContainer([])), null);
});

test('navigateNext 前进并自末位回环；无激活项时落首位', () => {
    const a = link(), b = link(), c = link();
    const box = makeContainer([a, b, c]);
    document.activeElement = null;
    assert.equal(nav.navigateNext(box), a);
    assert.equal(nav.navigateNext(box), b);
    assert.equal(nav.navigateNext(box), c);
    assert.equal(nav.navigateNext(box), a, '末位应回环到首位');
});

test('navigatePrevious 后退并自首位回环；无激活项时落末位', () => {
    const a = link(), b = link(), c = link();
    const box = makeContainer([a, b, c]);
    document.activeElement = null;
    assert.equal(nav.navigatePrevious(box), c);
    assert.equal(nav.navigatePrevious(box), b);
    assert.equal(nav.navigatePrevious(box), a);
    assert.equal(nav.navigatePrevious(box), c, '首位应回环到末位');
});

test('aria-disabled、hidden 与不可见项被过滤', () => {
    const a = link();
    const disabled = link({ 'aria-disabled': 'true' });
    const hidden = link();
    hidden.hidden = true;
    const offscreen = link();
    offscreen.style.display = 'none';
    const box = makeContainer([a, disabled, hidden, offscreen]);
    // 三枚不合格项在导航面上全部被过滤：首末都只剩 a。
    assert.equal(nav.navigateFirst(box), a);
    assert.equal(nav.navigateLast(box), a);
    document.activeElement = a;
    assert.equal(nav.navigateNext(box), a, '环回应只命中合格项自身');
});

test('role="menuitem" 元素与锚标签同列可聚焦面', () => {
    const item = makeEl({ attrs: { role: 'menuitem' } });
    const a = link();
    const box = makeContainer([item, a]);
    document.activeElement = null;
    assert.equal(nav.navigateFirst(box), item);
    assert.equal(nav.navigateNext(box), a);
});

test('focusElement 对 null 安全，对元素转调 focus', () => {
    const a = link();
    nav.focusElement(a);
    assert.equal(a.focusCalls, 1);
    assert.doesNotThrow(() => nav.focusElement(null));
    assert.doesNotThrow(() => nav.focusElement(undefined));
});

test('setupKeyboardNavigation 方向键/Home/End 派发预期导航', () => {
    const a = link(), b = link(), c = link();
    const box = makeContainer([a, b, c]);
    document.activeElement = null;
    const cleanup = nav.setupKeyboardNavigation(box, null);
    const key = k => [...box.handlers.get('keydown')].forEach(fn => fn({ key: k, preventDefault() { } }));
    // 首帧无激活：ArrowDown 落首位，再 ArrowDown 前进；无激活时 ArrowUp 落末位。
    key('ArrowDown');
    assert.equal(document.activeElement, a);
    key('ArrowDown');
    assert.equal(document.activeElement, b);
    key('ArrowUp');
    assert.equal(document.activeElement, a);
    key('End');
    assert.equal(document.activeElement, c);
    key('Home');
    assert.equal(document.activeElement, a);
    cleanup.dispose();
});

test('Escape 回调 DotNet 的 HandleEscapeKey，其余键不回调', () => {
    const a = link();
    const box = makeContainer([a]);
    const calls = [];
    const dotNetRef = { invokeMethodAsync: (name) => { calls.push(name); return Promise.resolve(); } };
    const cleanup = nav.setupKeyboardNavigation(box, dotNetRef);
    const key = k => [...box.handlers.get('keydown')].forEach(fn => fn({ key: k, preventDefault() { } }));
    key('Escape');
    key('Tab');
    assert.deepEqual(calls, ['HandleEscapeKey']);
    cleanup.dispose();
});

test('cleanup.dispose 摘除 keydown 监听', () => {
    const a = link();
    const box = makeContainer([a]);
    const cleanup = nav.setupKeyboardNavigation(box, null);
    assert.equal(box.handlers.get('keydown').length, 1);
    cleanup.dispose();
    assert.equal(box.handlers.get('keydown').length, 0);
});

test('未识别按键不引发导航', () => {
    const a = link(), b = link();
    const box = makeContainer([a, b]);
    document.activeElement = null;
    const cleanup = nav.setupKeyboardNavigation(box, null);
    const key = k => [...box.handlers.get('keydown')].forEach(fn => fn({ key: k, preventDefault() { } }));
    key('Tab');
    assert.equal(document.activeElement, null);
    cleanup.dispose();
});

test('components-core.js barrel 已收口 navigationMenu 命名空间', async () => {
    const { readFile } = await import('node:fs/promises');
    const core = new URL('../../src/AtomBlazor.Components/wwwroot/js/components-core.js', import.meta.url);
    const text = await readFile(core, 'utf8');
    assert.match(text, /^import \* as navigationMenu from '\.\/navigation-menu\.js\?assets=1';$/m);
    assert.match(text, /^assertFresh\('navigation-menu\.js', navigationMenu, 'navigateNext'\);$/m);
    assert.match(text, /^export \{[\s\S]*\bnavigationMenu,[\s\S]*\};$/m);
});
