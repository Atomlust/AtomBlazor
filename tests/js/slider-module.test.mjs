// slider.js 的真实文件导入与行为测试。
// 模块顶层零 DOM 触碰（Map/闭包定义），document/getComputedStyle 只在手柄函数体内
// 访问，因此可在无 DOM 的 Node 环境以 file-URL 导入；../drag-updates.js 相对依赖
// （createDragUpdates/releaseDragPreview/snapSliderValue）随静态依赖图一并解析。
// C# 侧调用点为 barrel 点分标识符 slider.initialize/dispose（见 primitives.js 与
// PrimitiveJsModuleTests 的命名空间 camelCase 规则），本文件同时守护该导出面。
// DOM 交互沿用 numeric-input-module.test.mjs 先例的最小元素桩
//（addEventListener/getAttribute/style/getBoundingClientRect/pointer capture），
// dotNetRef 桩以变参记录 invokeMethodAsync 调用。

import assert from 'node:assert/strict';
import { test, afterEach } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Primitives/wwwroot/js/primitives/slider.js', import.meta.url);

function makeElement(attrs = {}, rect = { left: 0, top: 0, right: 100, bottom: 100, width: 100, height: 100 }) {
    const handlers = new Map();
    return {
        handlers,
        attrs,
        style: { props: new Map(), setProperty(name, value) { this.props.set(name, value); }, removeProperty(name) { this.props.delete(name); } },
        getBoundingClientRect() { return rect; },
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
        setPointerCapture() { this.captured = true; },
        releasePointerCapture() { this.captured = false; },
        hasPointerCapture() { return this.captured === true; },
        dispatch(type, event = {}) {
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

// 浏览器全局的最小桩：slider.js 在手柄体内读取它们，模块求值期不触碰。
let direction = 'ltr';
globalThis.document = { body: { style: {} } };
globalThis.getComputedStyle = () => ({ direction });
globalThis.requestAnimationFrame = callback => callback();

let nextId = 0;
const ids = [];

afterEach(async () => {
    const mod = await import(moduleUrl.href);
    while (ids.length) mod.dispose(ids.pop());
    direction = 'ltr';
});

// drag-updates 的节流窗口为 50ms：两次不同的取整值要等第二个窗口跨越 interop。
const settle = () => new Promise(resolve => setTimeout(resolve, 120));

test('slider.js 可导入且导出面与 slider. 点分标识符一一对应', async () => {
    const mod = await import(moduleUrl.href);

    // C# 侧调用点：slider.initialize / slider.dispose。
    for (const name of ['initialize', 'dispose']) {
        assert.equal(typeof mod[name], 'function', `slider.js 缺少导出 ${name}`);
    }
});

test('initialize 挂载六类监听器', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeElement();

    mod.initialize(element, makeDotNet(), freshId(), { orientation: 'horizontal' });

    for (const type of ['keydown', 'pointerdown', 'pointermove', 'pointerup', 'pointercancel', 'lostpointercapture']) {
        assert.equal(element.count(type), 1, `缺少 ${type} 监听器`);
    }
});

test('initialize 对缺失入参静默返回（预渲染/半程释放容错）', async () => {
    const mod = await import(moduleUrl.href);

    mod.initialize(null, makeDotNet(), freshId(), {});
    mod.initialize(makeElement(), null, freshId(), {});

 // 没有任何可清理的状态登记（上一用例的 afterEach 已清空）。
    mod.dispose('未知id'); // 不抛错即通过
});

function freshId() {
    const id = `slider-${nextId++}`;
    ids.push(id);
    return id;
}

test('dispose 摘除全部监听器并清理预览样式属性', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeElement();
    const id = freshId();

    mod.initialize(element, makeDotNet(), id, { orientation: 'horizontal' });
    element.dispatch('pointerdown', { pointerId: 1, button: 0, clientX: 50, clientY: 0, preventDefault() {} });
    assert.equal(element.style.props.get('--atom-slider-position'), '50%');

    mod.dispose(id);

    for (const type of ['keydown', 'pointerdown', 'pointermove', 'pointerup']) {
        assert.equal(element.count(type), 0, `${type} 监听器未被摘除`);
    }
    assert.equal(element.style.props.has('--atom-slider-position'), false);
});

test('initialize 同一 id 重复挂载应先释放旧实例', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeElement();
    const id = freshId();

    mod.initialize(element, makeDotNet(), id, { orientation: 'horizontal' });
    mod.initialize(element, makeDotNet(), id, { orientation: 'horizontal' });

    // 旧监听器被移除：每类只剩最新一轮的一份。
    assert.equal(element.count('pointerdown'), 1);
    assert.equal(element.count('pointermove'), 1);
});

test('禁用或非主键按下应被拒绝', async () => {
    const mod = await import(moduleUrl.href);
    const dotNet = makeDotNet();

    const disabled = makeElement({ 'data-disabled': 'true' });
    mod.initialize(disabled, dotNet, freshId(), { orientation: 'horizontal' });
    let prevented = false;
    disabled.dispatch('pointerdown', { pointerId: 1, button: 0, clientX: 50, clientY: 0, preventDefault() { prevented = true; } });
    assert.equal(prevented, false);
    assert.equal(disabled.style.props.has('--atom-slider-position'), false);

    // 右键（button > 0）不启动拖拽。
    const element = makeElement();
    mod.initialize(element, dotNet, freshId(), { orientation: 'horizontal' });
    element.dispatch('pointerdown', { pointerId: 2, button: 2, clientX: 50, clientY: 0, preventDefault() {} });
    assert.equal(element.style.props.has('--atom-slider-position'), false);
});

test('指针拖拽应换算为取整后的比例并跨 interop 上报', async () => {
    const mod = await import(moduleUrl.href);
    const dotNet = makeDotNet();
    const element = makeElement({ 'data-min': '0', 'data-max': '100', 'data-step': '20' });
    const id = freshId();
    mod.initialize(element, dotNet, id, { orientation: 'horizontal' });

    element.dispatch('pointerdown', { pointerId: 1, button: 0, clientX: 0, clientY: 0, preventDefault() {} });
    // 44% → 步长 20 取整为 40 → 比例 0.4。
    element.dispatch('pointermove', { pointerId: 1, clientX: 44, clientY: 0, preventDefault() {} });
    assert.equal(element.style.props.get('--atom-slider-position'), '40%');
    element.dispatch('pointerup', { pointerId: 1, type: 'pointerup', clientX: 44, clientY: 0, preventDefault() {} });
    await settle();

    // pointerdown 先上报 0，移动换档后上报 0.4；pointerup 的重算落在同档被滤除。
    assert.deepEqual(dotNet.calls, [['JsUpdateValueFromPercentage', 0], ['JsUpdateValueFromPercentage', 0.4]]);
    // 手势结束：预览交还与 C# 渲染，样式属性被移除。
    assert.equal(element.style.props.has('--atom-slider-position'), false);
});

test('相同步进的重复移动只跨一次电路', async () => {
    const mod = await import(moduleUrl.href);
    const dotNet = makeDotNet();
    const element = makeElement({ 'data-min': '0', 'data-max': '100', 'data-step': '50' });
    const id = freshId();
    mod.initialize(element, dotNet, id, { orientation: 'horizontal' });

    element.dispatch('pointerdown', { pointerId: 1, button: 0, clientX: 60, clientY: 0, preventDefault() {} });
    // 两次移动都落在步长 50 的同一档（50）。
    element.dispatch('pointermove', { pointerId: 1, clientX: 60, clientY: 0, preventDefault() {} });
    element.dispatch('pointermove', { pointerId: 1, clientX: 70, clientY: 0, preventDefault() {} });
    await settle();

    assert.deepEqual(dotNet.calls, [['JsUpdateValueFromPercentage', 0.5]]);
});

test('垂直滑块自底向上换算', async () => {
    const mod = await import(moduleUrl.href);
    const dotNet = makeDotNet();
    const element = makeElement({ 'data-orientation': 'vertical', 'data-min': '0', 'data-max': '100', 'data-step': '1' });
    const id = freshId();
    mod.initialize(element, dotNet, id, { orientation: 'vertical' });

    // 从顶部往下 25% 处按下（自底向上换算）：值应为 25。
    element.dispatch('pointerdown', { pointerId: 1, button: 0, clientX: 0, clientY: 75, preventDefault() {} });
    await settle();

    assert.deepEqual(dotNet.calls, [['JsUpdateValueFromPercentage', 0.25]]);
    assert.equal(element.style.props.get('--atom-slider-position'), '25%');
});

test('RTL 下水平轨道从右缘起算：点击左端意为最大值', async () => {
    const mod = await import(moduleUrl.href);
    const dotNet = makeDotNet();
    const element = makeElement({ 'data-min': '0', 'data-max': '100', 'data-step': '1' });
    const id = freshId();
    mod.initialize(element, dotNet, id, { orientation: 'horizontal' });

    direction = 'rtl';
    element.dispatch('pointerdown', { pointerId: 1, button: 0, clientX: 0, clientY: 0, preventDefault() {} });
    await settle();

    assert.deepEqual(dotNet.calls, [['JsUpdateValueFromPercentage', 1]]);
});

test('方向键步进按键应 preventDefault 而其余按键放行', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeElement();
    const id = freshId();
    mod.initialize(element, makeDotNet(), id, { orientation: 'horizontal' });

    const prevented = [];
    const key = k => {
        const e = { key: k, preventDefault() { prevented.push(k); } };
        element.dispatch('keydown', e);
    };

    for (const k of ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End', 'PageUp', 'PageDown']) {
        key(k);
    }
    key('a');
    key('Tab');

    assert.deepEqual(prevented, ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End', 'PageUp', 'PageDown']);
});

test('禁用态下步进按键不再 preventDefault', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeElement({ 'data-disabled': 'true' });
    const id = freshId();
    mod.initialize(element, makeDotNet(), id, { orientation: 'horizontal' });

    let prevented = false;
    element.dispatch('keydown', { key: 'ArrowUp', preventDefault() { prevented = true; } });
    assert.equal(prevented, false);
});
