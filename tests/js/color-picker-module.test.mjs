// color-picker.js 的真实文件导入与行为测试。
// 模块静态导入 ../../AtomBlazor.Primitives/js/drag-updates.js?assets=1 ——该相对路径
// 按 _content/ 服务布局解析（发布后在浏览器里成立），仓库布局下 Node 解析不到，
// 故沿用 module-loading-harness/range-slider 的"data: URI base64 再导入"模式，导入前把
// 该说明符改写为 drag-updates.js 的真实 file URL；其余代码逐行照常求值。
// C# 侧调用点为 barrel 点分标识符 colorPicker.initializeArea/initializeSlider/dispose
// （见 components-core.js 与 PrimitiveJsModuleTests 的命名空间 camelCase 规则），
// 本文件同时守护该导出面。模块顶层只建 Map（零 DOM 触碰），document/requestAnimationFrame
// 仅在事件沿内使用，桩面照 range-slider-module.test.mjs 先例。
// bindDrag 经 element.closest('[data-atom-color-picker]') 找根：root 桩提供 style
// （setProperty/removeProperty）与 getAttribute('data-disabled')；预览变量写在 root 上，
// pointerup 后经 releaseDragPreview 交还（requestAnimationFrame 桩即时执行）。
// dotNetRef 桩以变参记录 invokeMethodAsync 调用；drag-updates 节流窗口 50ms，
// begin 后首推即时、后续推等待窗口跨越，测试以短等待排空。

import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test, afterEach } from 'node:test';

const modulePath = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/color-picker.js', import.meta.url);
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

function makeStyle() {
    return {
        props: new Map(),
        setProperty(name, value) { this.props.set(name, value); },
        removeProperty(name) { this.props.delete(name); },
    };
}

// 根容器桩：bindDrag 只经 closest 取得它，读 data-disabled、写预览变量。
function makeRoot({ disabled = false } = {}) {
    return {
        style: makeStyle(),
        attrs: { 'data-disabled': disabled ? 'true' : null },
        getAttribute(name) { return this.attrs[name] ?? null; },
    };
}

// 拖拽目标元素桩：100x100 矩形，pointer 捕获与监听器记录。
function makeElement({ root = makeRoot(), rect = { left: 0, top: 0, width: 100, height: 100 } } = {}) {
    const handlers = new Map();
    const el = {
        handlers,
        captured: false,
        style: makeStyle(),
        closest(selector) { return selector === '[data-atom-color-picker]' ? root : null; },
        getBoundingClientRect() { return rect; },
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
        hasPointerCapture(id) { return this.captured === true && id === 1; },
        // finish 分支读 e.type：dispatch 补默认 type，用例仍可覆写。
        dispatch(type, event = {}) {
            event.type ??= type;
            event.preventDefault ??= () => {};
            for (const fn of [...(handlers.get(type) ?? [])]) fn(event);
        },
        count(type) { return (handlers.get(type) ?? []).length; },
    };
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

// 浏览器全局最小桩：bindDrag 在 down/finish 里读写 userSelect，即时帧回调。
globalThis.document ??= { body: { style: {} } };
globalThis.requestAnimationFrame ??= callback => callback();

// drag-updates 节流窗口 50ms：begin 后首推即时，第二推要等窗口跨越。
const settle = () => new Promise(resolve => setTimeout(resolve, 120));

let nextId = 0;
const ids = [];

function freshId() {
    const id = `picker-${nextId++}`;
    ids.push(id);
    return id;
}

afterEach(async () => {
    const mod = await import(moduleUrl);
    while (ids.length) mod.dispose(ids.pop());
});

test('color-picker.js 可导入且导出面与 C# 点分标识符一一对应', async () => {
    const mod = await import(moduleUrl);

    // C# 侧调用点：colorPicker.initializeArea / initializeSlider / dispose。
    for (const name of ['initializeArea', 'initializeSlider', 'dispose']) {
        assert.equal(typeof mod[name], 'function', `color-picker.js 缺少导出 ${name}`);
    }
});

test('initializeArea 对缺失入参静默返回；无根容器时不挂监听', async () => {
    const mod = await import(moduleUrl);

    mod.initializeArea(null, makeDotNet(), freshId());
    mod.initializeArea(makeElement(), null, freshId());

    // closest 找不到 [data-atom-color-picker] 根：bindDrag 直接退化为空清理句柄。
    const orphan = makeElement({ root: null });
    const id = freshId();
    mod.initializeArea(orphan, makeDotNet(), id);
    assert.equal(orphan.count('pointerdown'), 0);
    mod.dispose(id); // 空状态幂等
});

test('initializeArea 挂载五类 pointer 监听并上报 UpdateAreaFromJs', async () => {
    const mod = await import(moduleUrl);
    const dotNet = makeDotNet();
    const element = makeElement();
    const id = freshId();
    mod.initializeArea(element, dotNet, id);

    for (const type of ['pointerdown', 'pointermove', 'pointerup', 'pointercancel', 'lostpointercapture']) {
        assert.equal(element.count(type), 1, `缺少 ${type} 监听器`);
    }

    // 按在 (25, 25)：x=0.25，y=1-0.25=0.75，begin 后首推即时跨越 interop。
    element.dispatch('pointerdown', { pointerId: 1, button: 0, clientX: 25, clientY: 25 });
    await settle();
    assert.deepEqual(dotNet.calls, [['UpdateAreaFromJs', 0.25, 0.75]]);

    // 预览变量写在根上：saturation=x、brightness=1-y。
    const root = element.closest('[data-atom-color-picker]');
    assert.equal(root.style.props.get('--atom-color-saturation'), '25%');
    assert.equal(root.style.props.get('--atom-color-brightness'), '25%');
});

test('区域拖拽经节流上报序列且 pointerup 交还预览变量', async () => {
    const mod = await import(moduleUrl);
    const dotNet = makeDotNet();
    const element = makeElement();
    const id = freshId();
    mod.initializeArea(element, dotNet, id);

    element.dispatch('pointerdown', { pointerId: 1, button: 0, clientX: 0, clientY: 100 });
    element.dispatch('pointermove', { pointerId: 1, clientX: 50, clientY: 50 });
    await settle();

    // 同窗口两推都到：0,0 与 0.5,0.5（move 的推在 50ms 后第二窗口发出）。
    assert.deepEqual(dotNet.calls, [
        ['UpdateAreaFromJs', 0, 0],
        ['UpdateAreaFromJs', 0.5, 0.5],
    ]);

    // pointerup 先补一次 update（0.6,0.4），再 flush + 双 rAF 移除预览属性。
    element.dispatch('pointerup', { pointerId: 1, clientX: 60, clientY: 60 });
    await settle();
    assert.deepEqual(dotNet.calls.at(-1), ['UpdateAreaFromJs', 0.6, 0.4]);

    const root = element.closest('[data-atom-color-picker]');
    assert.equal(root.style.props.size, 0); // releaseDragPreview 已交还
});

test('坐标越界被钳制到 [0,1]', async () => {
    const mod = await import(moduleUrl);
    const dotNet = makeDotNet();
    const element = makeElement();
    const id = freshId();
    mod.initializeArea(element, dotNet, id);

    element.dispatch('pointerdown', { pointerId: 1, button: 0, clientX: 150, clientY: -30 });
    await settle();
    assert.deepEqual(dotNet.calls, [['UpdateAreaFromJs', 1, 1]]);
});

test('禁用根拒绝手势：不落捕获也不上报', async () => {
    const mod = await import(moduleUrl);
    const dotNet = makeDotNet();
    const element = makeElement({ root: makeRoot({ disabled: true }) });
    const id = freshId();
    mod.initializeArea(element, dotNet, id);

    element.dispatch('pointerdown', { pointerId: 1, button: 0, clientX: 25, clientY: 25 });
    element.dispatch('pointermove', { pointerId: 1, clientX: 50, clientY: 50 });
    await settle();

    assert.deepEqual(dotNet.calls, []);
    assert.equal(element.captured, false);
});

test('非主键按下被忽略；异 pointerId 的 move/up 不放行', async () => {
    const mod = await import(moduleUrl);
    const dotNet = makeDotNet();
    const element = makeElement();
    const id = freshId();
    mod.initializeArea(element, dotNet, id);

    element.dispatch('pointerdown', { pointerId: 1, button: 2, clientX: 25, clientY: 25 });
    await settle();
    assert.deepEqual(dotNet.calls, []); // 右键按下不开拖

    element.dispatch('pointerdown', { pointerId: 1, button: 0, clientX: 25, clientY: 25 });
    await settle();
    const baseline = dotNet.calls.length;

    // pointerId 不匹配：move/up 直接早退。
    element.dispatch('pointermove', { pointerId: 9, clientX: 50, clientY: 50 });
    element.dispatch('pointerup', { pointerId: 9, clientX: 50, clientY: 50 });
    await settle();
    assert.equal(dotNet.calls.length, baseline);
});

test('initializeSlider 拒绝未知类型；hue 滑杆写位置与色相双变量', async () => {
    const mod = await import(moduleUrl);
    const dotNet = makeDotNet();

    const badType = makeElement();
    const badId = freshId();
    mod.initializeSlider(badType, dotNet, badId, 'saturation'); // 不在 ['hue','alpha'] 白名单
    assert.equal(badType.count('pointerdown'), 0);

    const element = makeElement();
    const id = freshId();
    mod.initializeSlider(element, dotNet, id, 'hue');

    element.dispatch('pointerdown', { pointerId: 1, button: 0, clientX: 25, clientY: 0 });
    await settle();
    assert.deepEqual(dotNet.calls, [['UpdateSliderFromJs', 'hue', 0.25]]);

    const root = element.closest('[data-atom-color-picker]');
    assert.equal(root.style.props.get('--atom-color-hue-position'), '25%');
    assert.equal(root.style.props.get('--atom-color-hue'), String(0.25 * 360));

    // pointerup 交还：hue 拖拽清理 [position, --atom-color-hue] 两枚属性。
    element.dispatch('pointerup', { pointerId: 1, clientX: 30, clientY: 0 });
    await settle();
    assert.equal(root.style.props.size, 0);
});

test('alpha 滑杆只写自身位置变量并上报 alpha 类型', async () => {
    const mod = await import(moduleUrl);
    const dotNet = makeDotNet();
    const element = makeElement();
    const id = freshId();
    mod.initializeSlider(element, dotNet, id, 'alpha');

    element.dispatch('pointerdown', { pointerId: 1, button: 0, clientX: 80, clientY: 0 });
    await settle();
    assert.deepEqual(dotNet.calls, [['UpdateSliderFromJs', 'alpha', 0.8]]);

    const root = element.closest('[data-atom-color-picker]');
    assert.equal(root.style.props.get('--atom-color-alpha-position'), '80%');
    assert.equal(root.style.props.has('--atom-color-hue'), false);
});

test('同 id 重复 initializeArea 替换旧绑定而非叠加', async () => {
    const mod = await import(moduleUrl);
    const dotNet = makeDotNet();
    const id = freshId();

    const first = makeElement();
    mod.initializeArea(first, dotNet, id);
    const second = makeElement();
    mod.initializeArea(second, dotNet, id);

    assert.equal(first.count('pointerdown'), 0); // 旧监听已摘
    assert.equal(second.count('pointerdown'), 1);

    second.dispatch('pointerdown', { pointerId: 1, button: 0, clientX: 25, clientY: 25 });
    await settle();
    assert.deepEqual(dotNet.calls, [['UpdateAreaFromJs', 0.25, 0.75]]);
});

test('dispose 摘除全部绑定且未知 id 无害；清理后可重挂', async () => {
    const mod = await import(moduleUrl);
    const dotNet = makeDotNet();
    const area = makeElement();
    const slider = makeElement();
    const id = freshId();
    mod.initializeArea(area, dotNet, id);
    mod.initializeSlider(slider, dotNet, id, 'hue');

    mod.dispose(id);
    assert.equal(area.count('pointerdown'), 0);
    assert.equal(slider.count('pointerdown'), 0);

    area.dispatch('pointerdown', { pointerId: 1, button: 0, clientX: 25, clientY: 25 });
    await settle();
    assert.deepEqual(dotNet.calls, []); // 摘除后事件不再上报

    mod.dispose('未知id'); // 不抛错即通过

    // 同 id 重挂（弹层 remount 沿）：C# 先 dispose 再 initialize。
    mod.initializeArea(area, dotNet, id);
    assert.equal(area.count('pointerdown'), 1);
});
