// tabs-reorder.js 的真实文件导入与行为测试。
// 模块顶层只建 Map（零 DOM 触碰）；document.createElement 与各事件手柄仅在函数体内访问，
// 因此可在无 DOM 的 Node 环境以 file-URL 导入；模块无相对依赖。
// C# 侧调用点为 barrel 点分标识符 tabsReorder.initialize/locate/selectAll/dispose
// （见 TabsTests 的 interop 载荷断言与 components-core.js 的 assertFresh 收口）；
// 回调通道为 dotNetRef.invokeMethodAsync('OnTabDropped', value, from, target)。
// DOM 桩沿用 tree-keyboard-module.test.mjs 先例：监听器挂摘计数、微型选择器匹配，
// 另补 getBoundingClientRect/closest/contains/appendChild 与指示条 remove 的最小实现。

import assert from 'node:assert/strict';
import { test, afterEach } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/tabs-reorder.js', import.meta.url);

// 微型选择器匹配：仅 [attr="v"][attr2] 连缀特性形态（本模块选择器只有 role+data 一类）。
function matchSimple(el, part) {
    const groups = part.match(/\[[^\]=]+(?:="[^"]*")?\]/g);
    if (!groups || groups.join('') !== part.trim()) return false;
    return groups.every(group => {
        const m = group.match(/^\[([^\]=]+)(?:="([^"]*)")?\]$/);
        const value = el.getAttribute(m[1]);
        return m[2] === undefined ? value !== null : value === m[2];
    });
}

function makeEl({ attrs = {}, rect = { left: 0, right: 0, width: 0 } } = {}) {
    const handlers = new Map();
    const el = {
        attrs,
        handlers,
        children: [],
        parentNode: null,
        style: {},
        rect,
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
        getBoundingClientRect() { return rect; },
        closest(selector) {
            const parts = selector.split(',').map(p => p.trim());
            return parts.some(part => matchSimple(el, part)) ? el : null;
        },
        contains(other) {
            for (const child of el.children) {
                if (child === other || (child.contains && child.contains(other))) return true;
            }
            return false;
        },
        appendChild(child) {
            child.parentNode = el;
            el.children.push(child);
            return child;
        },
        remove() {
            if (el.parentNode) {
                el.parentNode.children.splice(el.parentNode.children.indexOf(el), 1);
                el.parentNode = null;
            }
        },
        querySelectorAll(selector) {
            const parts = selector.split(',').map(p => p.trim());
            return el.children.filter(child => parts.some(part => matchSimple(child, part)));
        },
    };
    return el;
}

// document.createElement 桩：指示条是一个带 style 的 div，remove 时从父容器出清。
const created = [];
globalThis.document = {
    createElement() {
        const bar = {
            style: {},
            parentNode: null,
            remove() {
                if (bar.parentNode) {
                    bar.parentNode.children.splice(bar.parentNode.children.indexOf(bar), 1);
                    bar.parentNode = null;
                }
            },
        };
        created.push(bar);
        return bar;
    },
};

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

function makeDataTransfer() {
    const data = {};
    return {
        data,
        setData(type, value) { data[type] = value; },
    };
}

// 三个标签：one/two/three，横向排布 0-100/100-200/200-300。
function makeList() {
    const tab = (value, left) => makeEl({
        attrs: { role: 'tab', 'data-tab-value': value },
        rect: { left, right: left + 100, width: 100 },
    });
    const one = tab('one', 0);
    const two = tab('two', 100);
    const three = tab('three', 200);
    const root = makeEl({ rect: { left: 0, right: 300, width: 300 } });
    for (const t of [one, two, three]) root.appendChild(t);
    return { root, one, two, three };
}

const liveIds = [];
let currentMod = null;

async function freshInitialize(root, dotNet, id) {
    currentMod ??= await import(moduleUrl.href);
    currentMod.initialize(root, dotNet, id);
    liveIds.push(id);
    return currentMod;
}

afterEach(async () => {
    const mod = currentMod ?? await import(moduleUrl.href);
    currentMod ??= mod;
    while (liveIds.length) mod.dispose(liveIds.pop());
    liveIds.length = 0;
    created.length = 0;
});

// ------------------------------------------------------------------

test('tabs-reorder.js 可导入且导出面与 tabsReorder. 点分标识符一一对应', async () => {
    const mod = await import(moduleUrl.href);

    // C# 侧调用点：tabsReorder.initialize / locate / selectAll / dispose。
    for (const name of ['initialize', 'locate', 'selectAll', 'dispose']) {
        assert.equal(typeof mod[name], 'function', `tabs-reorder.js 缺少导出 ${name}`);
    }
});

test('initialize 对缺失入参静默返回；同一 id 重复初始化不二次挂监听', async () => {
    const mod = await import(moduleUrl.href);
    const { root } = makeList();

    mod.initialize(null, makeDotNet(), 'tr-null');
    mod.initialize(root, null, 'tr-nullref');
    assert.equal(root.count('dragstart'), 0);

    mod.initialize(root, makeDotNet(), 'tr-twice');
    liveIds.push('tr-twice');
    mod.initialize(root, makeDotNet(), 'tr-twice'); // states.has(id) → 直接 return
    assert.equal(root.count('dragstart'), 1);
});

test('initialize 在容器挂齐五个拖拽监听器，dispose 全数摘除且幂等', async () => {
    const mod = await import(moduleUrl.href);
    const { root } = makeList();

    mod.initialize(root, makeDotNet(), 'tr-listeners');
    for (const type of ['dragstart', 'dragover', 'drop', 'dragend', 'dragleave']) {
        assert.equal(root.count(type), 1, `${type} 应挂载一个监听器`);
    }

    mod.dispose('tr-listeners');
    for (const type of ['dragstart', 'dragover', 'drop', 'dragend', 'dragleave']) {
        assert.equal(root.count(type), 0, `${type} 应被摘除`);
    }

    mod.dispose('tr-listeners'); // 幂等
    mod.dispose('未知id');
});

test('dragstart 命中标签时记下其值并写入 dataTransfer（Firefox 空数据不拖）', async () => {
    const { root, two } = makeList();
    const dotNet = makeDotNet();
    await freshInitialize(root, dotNet, 'tr-dragstart');

    const transfer = makeDataTransfer();
    root.dispatch('dragstart', { target: two, dataTransfer: transfer });
    assert.equal(transfer.effectAllowed, 'move');
    assert.equal(transfer.data['text/plain'], 'two');

    // 非标签目标：不写传输数据。
    const other = makeEl({ attrs: { role: 'presentation' } });
    const quiet = makeDataTransfer();
    root.dispatch('dragstart', { target: other, dataTransfer: quiet });
    assert.equal(quiet.data['text/plain'], undefined);
});

test('dragover：未拖拽不拦截；拖拽经过标签时阻止默认并落指示条', async () => {
    const { root, one, three } = makeList();
    await freshInitialize(root, makeDotNet(), 'tr-dragover');

    // dragValue 仍为 null：不 preventDefault。
    let event = { target: three, clientX: 260, preventDefault() { this.defaultPrevented = true; } };
    root.dispatch('dragover', event);
    assert.notEqual(event.defaultPrevented, true);

    root.dispatch('dragstart', { target: one, dataTransfer: makeDataTransfer() });

    event = { target: three, clientX: 260, dataTransfer: {}, preventDefault() { this.defaultPrevented = true; } };
    root.dispatch('dragover', event);
    assert.equal(event.defaultPrevented, true);
    assert.equal(event.dataTransfer.dropEffect, 'move');

    // 指示条：首次显示才创建并挂到容器，越过 midline（250）摆到 three 的右缘。
    assert.equal(created.length, 1);
    const bar = created[0];
    assert.equal(bar.style.display, 'block');
    assert.equal(bar.style.left, '300px');
    assert.equal(root.style.position, 'relative');
});

test('drop 越过中线先算插入点再解最终下标，避开 remove+insert 的 off-by-one', async () => {
    const { root, one, three } = makeList();
    const dotNet = makeDotNet();
    await freshInitialize(root, dotNet, 'tr-drop-forward');

    root.dispatch('dragstart', { target: one, dataTransfer: makeDataTransfer() });

    // one(from=0) 拖到 three 的后半（overIndex=2 → 插入点 3 → 解出终值 2）。
    const event = {
        target: three, clientX: 260, dataTransfer: {},
        preventDefault() { this.defaultPrevented = true; },
    };
    root.dispatch('drop', event);
    assert.equal(event.defaultPrevented, true);
    assert.deepEqual(dotNet.calls, [['OnTabDropped', 'one', 0, 2]]);

    // 落下即清 dragValue：同一事件流再来一次 drop 不再上报。
    root.dispatch('drop', {
        target: three, clientX: 260, dataTransfer: {}, preventDefault() {},
    });
    assert.equal(dotNet.calls.length, 1);
});

test('drop 原地落点与未拖拽情形都不上报移动', async () => {
    const { root, one, two } = makeList();
    const dotNet = makeDotNet();
    await freshInitialize(root, dotNet, 'tr-drop-noop');

    // 未拖拽：drop 整个静默。
    const quiet = { target: two, clientX: 120, preventDefault() { this.defaultPrevented = true; } };
    root.dispatch('drop', quiet);
    assert.notEqual(quiet.defaultPrevented, true);
    assert.deepEqual(dotNet.calls, []);

    root.dispatch('dragstart', { target: one, dataTransfer: makeDataTransfer() });
    // one(0) 落在自己前半（overIndex=0 → target=0 == from）：不上报。
    root.dispatch('drop', { target: one, clientX: 40, dataTransfer: {}, preventDefault() {} });
    assert.deepEqual(dotNet.calls, []);
});

test('dragend 与离开容器的 dragleave 收起指示条', async () => {
    const { root, one, two, three } = makeList();
    await freshInitialize(root, makeDotNet(), 'tr-hide');


    root.dispatch('dragstart', { target: one, dataTransfer: makeDataTransfer() });
    root.dispatch('dragover', { target: two, clientX: 120, dataTransfer: {}, preventDefault() {} });
    const bar = created[0];
    assert.equal(bar.style.display, 'block');

    root.dispatch('dragend', {});
    assert.equal(bar.style.display, 'none');

    // 重新拖拽后再显示，relatedTarget 在容器外的 dragleave 再收起。
    root.dispatch('dragstart', { target: one, dataTransfer: makeDataTransfer() });
    root.dispatch('dragover', { target: two, clientX: 180, dataTransfer: {}, preventDefault() {} });
    assert.equal(bar.style.display, 'block');
    root.dispatch('dragleave', { relatedTarget: makeEl() });
    assert.equal(bar.style.display, 'none');

    // relatedTarget 还在容器内时不收（同列表标签间抖动）。
    root.dispatch('dragover', { target: two, clientX: 180, dataTransfer: {}, preventDefault() {} });
    root.dispatch('dragleave', { relatedTarget: three });
    assert.equal(bar.style.display, 'block');

});

test('locate 以 DOM 为唯一事实源报告位置与总数', async () => {
    const mod = await import(moduleUrl.href);
    const { root } = makeList();

    // 未注册 id：{-1, 0}。
    assert.deepEqual(mod.locate('tr-unknown', 'one'), { index: -1, count: 0 });

    mod.initialize(root, makeDotNet(), 'tr-locate');
    liveIds.push('tr-locate');
    assert.deepEqual(mod.locate('tr-locate', 'two'), { index: 1, count: 3 });
    // 值不在列表：index -1，count 仍为真实数。
    assert.deepEqual(mod.locate('tr-locate', 'absent'), { index: -1, count: 3 });
});

test('selectAll 只在带 select 方法的输入框上调用（重命名预填通路）', async () => {
    const mod = await import(moduleUrl.href);

    let selected = 0;
    mod.selectAll({ select() { selected++; } });
    assert.equal(selected, 1);

    mod.selectAll(null);
    mod.selectAll({}); // 无 select 方法：静默
});

test('dispose 顺带移除遗留在容器上的指示条', async () => {
    const mod = await import(moduleUrl.href);
    const { root, one, two } = makeList();

    mod.initialize(root, makeDotNet(), 'tr-indicator');
    root.dispatch('dragstart', { target: one, dataTransfer: makeDataTransfer() });
    root.dispatch('dragover', { target: two, clientX: 180, dataTransfer: {}, preventDefault() {} });
    const bar = created[0];
    assert.equal(root.children.includes(bar), true); // 指示条挂在容器上

    mod.dispose('tr-indicator');
    assert.equal(root.children.includes(bar), false);
});
