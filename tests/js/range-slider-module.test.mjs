// range-slider.js 的真实文件导入与行为测试。
// 模块静态导入 ../../AtomBlazor.Primitives/js/drag-updates.js?assets=1 ——该相对路径
// 按 _content/ 服务布局解析（发布后在浏览器里成立），仓库布局下 Node 解析不到，
// 故沿用 module-loading-harness.test.mjs 的"data: URI base64 再导入"模式，导入前把
// 该说明符改写为 drag-updates.js 的真实 file URL；其余代码逐行照常求值。
// C# 侧调用点为 barrel 点分标识符 rangeSlider.initializeRangeSlider/startDrag/
// disposeRangeSlider（见 components-core.js 与 PrimitiveJsModuleTests 的命名空间
// camelCase 规则），本文件同时守护该导出面。
// DOM 交互沿用 slider-module.test.mjs 先例的最小元素桩（增加 contains/querySelector/
// target.closest 桩），dotNetRef 桩以变参记录 invokeMethodAsync 调用。

import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test, afterEach } from 'node:test';

const modulePath = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/range-slider.js', import.meta.url);
const dragUpdatesPath = new URL(
    '../../src/AtomBlazor.Primitives/wwwroot/js/drag-updates.js', import.meta.url);

// 把跨包说明符改写为真实 file URL 后经 data: URI 导入（单例，模块级 Map 随 afterEach 清理）。
const moduleUrl = await (async () => {
    const source = await readFile(modulePath, 'utf8');
    const rewritten = source.replace(
        "'../../AtomBlazor.Primitives/js/drag-updates.js?assets=1'",
        JSON.stringify(dragUpdatesPath.href));
    assert.notEqual(rewritten, source, '跨包 drag-updates 导入说明符改写失败——模块源码已变更？');
    return `data:text/javascript;base64,${Buffer.from(rewritten).toString('base64')}`;
})();

function makeElement(attrs = {}, rect = { left: 0, top: 0, right: 100, bottom: 100, width: 100, height: 100 }) {
    const handlers = new Map();
    const thumbs = attrs.thumbs ?? {};
    delete attrs.thumbs;
    return {
        handlers,
        attrs,
        thumbs,
        style: { props: new Map(), setProperty(name, value) { this.props.set(name, value); }, removeProperty(name) { this.props.delete(name); } },
        getBoundingClientRect() { return rect; },
        getAttribute(name) { return attrs[name] ?? null; },
        contains() { return true; },
        // begin() 显式聚焦活动手柄；桩返回 null 走可选链即安全。
        querySelector() { return null; },
        addEventListener(type, fn) {
            if (!handlers.has(type)) handlers.set(type, []);
            handlers.get(type).push(fn);
        },
        removeEventListener(type, fn) {
            const list = handlers.get(type) ?? [];
            const index = list.indexOf(fn);
            if (index !== -1) list.splice(index, 1);
        },
        setPointerCapture() { this.captured = true; },
        releasePointerCapture() { this.captured = false; },
        hasPointerCapture() { return this.captured === true; },
        dispatch(type, event = {}) {
            event.target ??= {
                // 默认模拟按在起点手柄上；用例可覆写 closest 返回 null（轨道点击）。
                closest: selector => (selector === '[data-thumb]' ? { getAttribute: name => thumbs[name] ?? 'start' } : null),
            };
            for (const fn of [...(handlers.get(type) ?? [])]) fn(event);
        },
        count(type) { return (handlers.get(type) ?? []).length; },
    };
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

// 浏览器全局的最小桩：模块在手柄体内读取它们，模块求值期不触碰。
globalThis.document = { body: { style: {} } };
globalThis.getComputedStyle = () => ({ direction: 'ltr' });
globalThis.requestAnimationFrame = callback => callback();

let nextId = 0;
const ids = [];

function freshId() {
    const id = `range-${nextId++}`;
    ids.push(id);
    return id;
}

afterEach(async () => {
    const mod = await import(moduleUrl);
    while (ids.length) mod.disposeRangeSlider(ids.pop());
});

// drag-updates 的节流窗口为 50ms：两次不同的取整值要等第二个窗口跨越 interop。
const settle = () => new Promise(resolve => setTimeout(resolve, 120));

const trackAttrs = {
    'data-min': '0',
    'data-max': '100',
    'data-step': '1',
    'data-min-range': '0',
    'data-start': '25',
    'data-end': '75',
};

test('range-slider.js 可导入且导出面与 rangeSlider. 点分标识符一一对应', async () => {
    const mod = await import(moduleUrl);

    // C# 侧调用点：rangeSlider.initializeRangeSlider / startDrag / disposeRangeSlider。
    for (const name of ['initializeRangeSlider', 'startDrag', 'disposeRangeSlider']) {
        assert.equal(typeof mod[name], 'function', `range-slider.js 缺少导出 ${name}`);
    }
});

test('initializeRangeSlider 挂载五类监听器（键盘由 C# 处理，无 keydown）', async () => {
    const mod = await import(moduleUrl);
    const element = makeElement({ ...trackAttrs });

    mod.initializeRangeSlider(element, makeDotNet(), freshId());

    for (const type of ['pointerdown', 'pointermove', 'pointerup', 'pointercancel', 'lostpointercapture']) {
        assert.equal(element.count(type), 1, `缺少 ${type} 监听器`);
    }
    assert.equal(element.count('keydown'), 0);
});

test('initializeRangeSlider 对缺失入参静默返回；dispose 未知 id 无害', async () => {
    const mod = await import(moduleUrl);

    mod.initializeRangeSlider(null, makeDotNet(), freshId());
    mod.initializeRangeSlider(makeElement(), null, freshId());
    mod.disposeRangeSlider('未知id'); // 不抛错即通过
});

test('拖拽起点手柄应上报 start 回调并写起点预览变量', async () => {
    const mod = await import(moduleUrl);
    const dotNet = makeDotNet();
    const element = makeElement({ ...trackAttrs });
    const id = freshId();
    mod.initializeRangeSlider(element, dotNet, id);

    element.dispatch('pointerdown', { pointerId: 1, button: 0, clientX: 25, clientY: 0, preventDefault() {} });
    element.dispatch('pointermove', { pointerId: 1, clientX: 50, clientY: 0, preventDefault() {} });
    await settle();

    // 按在起点手柄上仅 begin（不重算），移动到 50% 才上报。
    assert.deepEqual(dotNet.calls, [['UpdateValueFromPercentage', 0.5, 'start']]);
    assert.equal(element.style.props.get('--atom-range-start'), '50%');
    assert.equal(element.style.props.get('--atom-range-end'), '75%');
    assert.equal(element.style.props.get('--atom-range-width'), '25%');
});

test('终点手柄更新受 MinRange 钳制且同档重复上报被滤除', async () => {
    const mod = await import(moduleUrl);
    const dotNet = makeDotNet();
    const element = makeElement({ ...trackAttrs, 'data-min-range': '10' });
    const id = freshId();
    mod.initializeRangeSlider(element, dotNet, id);

    // 按在终点手柄上，拖到 10%：想拉到 10，钳到 25 + 10 = 35。
    element.dispatch('pointerdown', {
        pointerId: 1, button: 0, clientX: 10, clientY: 0, preventDefault() {},
        target: { closest: selector => (selector === '[data-thumb]' ? { getAttribute: () => 'end' } : null) },
    });
    element.dispatch('pointermove', { pointerId: 1, clientX: 8, clientY: 0, preventDefault() {} });
    await settle();

    assert.deepEqual(dotNet.calls, [['UpdateValueFromPercentage', 0.35, 'end']]);
    assert.equal(element.style.props.get('--atom-range-end'), '35%');
    assert.equal(element.style.props.get('--atom-range-width'), '10%');
});

test('轨道点击应选中更近的手柄并即刻上报', async () => {
    const mod = await import(moduleUrl);
    const dotNet = makeDotNet();
    const element = makeElement({ ...trackAttrs });
    const id = freshId();
    mod.initializeRangeSlider(element, dotNet, id);

    // target.closest 返回 null（点在轨道上），点击 0.2 → 值 20，距起点 25 更近 → start。
    element.dispatch('pointerdown', {
        pointerId: 1, button: 0, clientX: 20, clientY: 0, preventDefault() {},
        target: { closest: () => null },
    });
    await settle();

    assert.deepEqual(dotNet.calls, [['UpdateValueFromPercentage', 0.2, 'start']]);
});

test('startDrag 保留为兼容入口并转发 begin', async () => {
    const mod = await import(moduleUrl);
    const dotNet = makeDotNet();
    const element = makeElement({ ...trackAttrs });
    const id = freshId();
    mod.initializeRangeSlider(element, dotNet, id);

    // 模拟既有 interop 入口：不经 pointerdown 直接开始拖拽终点手柄。
    mod.startDrag(id, 'end', 7);
    element.dispatch('pointermove', { pointerId: 7, clientX: 90, clientY: 0, preventDefault() {} });
    await settle();

    assert.deepEqual(dotNet.calls, [['UpdateValueFromPercentage', 0.9, 'end']]);
});

test('起点手柄不可越过终点（minRange=0 时钳到终点）', async () => {
    const mod = await import(moduleUrl);
    const dotNet = makeDotNet();
    const element = makeElement({ ...trackAttrs });
    const id = freshId();
    mod.initializeRangeSlider(element, dotNet, id);

    mod.startDrag(id, 'start', 7);
    element.dispatch('pointermove', { pointerId: 7, clientX: 95, clientY: 0, preventDefault() {} });
    element.dispatch('pointermove', { pointerId: 7, clientX: 100, clientY: 0, preventDefault() {} });
    await settle();

    assert.deepEqual(dotNet.calls, [['UpdateValueFromPercentage', 0.75, 'start']]);
    // start 钳到 end（75）：两 thumb 不交叉，区间宽度归零。
    assert.equal(element.style.props.get('--atom-range-start'), '75%');
    assert.equal(element.style.props.get('--atom-range-width'), '0%');
});

test('禁用态拒绝手势且不上报', async () => {
    const mod = await import(moduleUrl);
    const dotNet = makeDotNet();
    const element = makeElement({ ...trackAttrs, 'data-disabled': 'true' });
    const id = freshId();
    mod.initializeRangeSlider(element, dotNet, id);

    element.dispatch('pointerdown', { pointerId: 1, button: 0, clientX: 20, clientY: 0, preventDefault() {} });
    element.dispatch('pointermove', { pointerId: 1, clientX: 50, clientY: 0, preventDefault() {} });
    await settle();

    assert.deepEqual(dotNet.calls, []);
    assert.equal(element.style.props.size, 0);
});

test('disposeRangeSlider 摘除监听器并清理预览变量', async () => {
    const mod = await import(moduleUrl);
    const element = makeElement({ ...trackAttrs });
    const id = freshId();
    mod.initializeRangeSlider(element, makeDotNet(), id);

    mod.disposeRangeSlider(id);

    for (const type of ['pointerdown', 'pointermove', 'pointerup']) {
        assert.equal(element.count(type), 0, `${type} 监听器未被摘除`);
    }
    assert.equal(element.style.props.size, 0);
});
