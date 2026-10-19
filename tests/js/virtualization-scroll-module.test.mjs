// virtualization-scroll.js 的真实文件导入与行为测试。
// 模块顶层零 DOM 触碰，document.getElementById 在手柄体内访问，因此可在无 DOM 的
// Node 环境以 file-URL 导入；模块无相对依赖。
// C# 侧调用点为 barrel 点分标识符 virtualizationScroll.scrollToVirtualizedIndex /
// virtualizationScroll.scrollElementIntoView（命名空间＝camelCase(文件名)，见
// PrimitiveJsModuleTests 规则），本文件同时守护该导出面。
// DOM 桩只需最小形状：list 提供 offsetTop/scrollHeight/clientHeight 且 scrollTop 可写，
// container 提供 offsetTop，元素记录 scrollIntoView 载荷。

import assert from 'node:assert/strict';
import { test, beforeEach } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/virtualization-scroll.js', import.meta.url);

// ---- document / 元素桩 ----

const elements = new Map();

function makeList({ offsetTop = 0, scrollHeight = 1000, clientHeight = 400 } = {}) {
    return { offsetTop, scrollHeight, clientHeight, scrollTop: null };
}

function makeContainer(offsetTop) {
    return { offsetTop };
}

function makeScrollable({ block = null } = {}) {
    return {
        calls: [],
        scrollIntoView(options) { this.calls.push(options); },
    };
}

beforeEach(() => {
    elements.clear();
    globalThis.document = { getElementById: id => elements.get(id) ?? null };
});

// ------------------------------------------------------------------

test('virtualization-scroll.js 可导入且导出面与 virtualizationScroll. 点分标识符一一对应', async () => {
    const mod = await import(moduleUrl.href);

    // C# 侧调用点：virtualizationScroll.scrollToVirtualizedIndex / .scrollElementIntoView。
    for (const name of ['scrollToVirtualizedIndex', 'scrollElementIntoView']) {
        assert.equal(typeof mod[name], 'function', `virtualization-scroll.js 缺少导出 ${name}`);
    }
});

test('scrollToVirtualizedIndex 对缺失列表或容器静默返回', async () => {
    const mod = await import(moduleUrl.href);

    elements.set('list', makeList());
    mod.scrollToVirtualizedIndex('list', 'ghost', 0); // 容器缺失

    elements.set('container', makeContainer(100));
    mod.scrollToVirtualizedIndex('ghost', 'container', 0); // 列表缺失
    assert.equal(elements.get('list').scrollTop, null); // 两次都未写入滚动位置
});

test('scrollToVirtualizedIndex 首项滚到容器顶（露出分组标题），不含标题与项高', async () => {
    const mod = await import(moduleUrl.href);
    const list = makeList({ offsetTop: 60, scrollHeight: 1000, clientHeight: 400 });
    const container = makeContainer(260); // containerTop = 260 - 60 = 200
    elements.set('list', list);
    elements.set('container', container);

    mod.scrollToVirtualizedIndex('list', 'container', 0);

    assert.equal(list.scrollTop, 200);
});

test('scrollToVirtualizedIndex 非首项加标题高与项乘积，默认各 32', async () => {
    const mod = await import(moduleUrl.href);
    const list = makeList({ offsetTop: 0, scrollHeight: 5000, clientHeight: 400 });
    elements.set('list', list);
    elements.set('container', makeContainer(100));

    // targetScroll = 100 + 32 + 3*32 = 228
    mod.scrollToVirtualizedIndex('list', 'container', 3);
    assert.equal(list.scrollTop, 228);

    // 显式传参覆盖默认：100 + 40 + 3*48 = 284
    mod.scrollToVirtualizedIndex('list', 'container', 3, 40, 48);
    assert.equal(list.scrollTop, 284);
});

test('scrollToVirtualizedIndex 钳位到有效滚动区间 [0, scrollHeight-clientHeight]', async () => {
    const mod = await import(moduleUrl.href);
    const list = makeList({ offsetTop: 0, scrollHeight: 1000, clientHeight: 400 });
    elements.set('list', list);

    // 上界：容器远在深处，目标 900+32+96=1028 超过 maxScroll=600 → 钳到 600
    elements.set('container', makeContainer(900));
    mod.scrollToVirtualizedIndex('list', 'container', 3);
    assert.equal(list.scrollTop, 600);

    // 下界：列表起点在容器之下（containerTop 为负）→ 钳到 0
    const negList = makeList({ offsetTop: 100, scrollHeight: 1000, clientHeight: 400 });
    elements.set('neg-list', negList);
    elements.set('container', makeContainer(10));
    mod.scrollToVirtualizedIndex('neg-list', 'container', 0);
    assert.equal(negList.scrollTop, 0);
});

test('scrollElementIntoView 对缺失元素静默返回', async () => {
    const mod = await import(moduleUrl.href);

    mod.scrollElementIntoView('ghost'); // 不抛错即通过
});

test('scrollElementIntoView 默认 nearest、instant 行为并原样透传块对齐', async () => {
    const mod = await import(moduleUrl.href);
    const el = makeScrollable();
    elements.set('item-1', el);

    mod.scrollElementIntoView('item-1');
    assert.deepEqual(el.calls, [{ block: 'nearest', behavior: 'instant' }]);

    mod.scrollElementIntoView('item-1', 'start');
    assert.equal(el.calls.at(-1).block, 'start');

    mod.scrollElementIntoView('item-1', 'center');
    assert.equal(el.calls.at(-1).block, 'center');

    // behavior 恒为 instant，不随调用方变化
    for (const call of el.calls) assert.equal(call.behavior, 'instant');
});
