// numeric-input.js 的真实文件导入与行为测试。
// 模块顶层零 DOM 触碰（Map/常量/纯函数），document 只在 handleWheel 体内访问，
// 因此可在无 DOM 的 Node 环境以 file-URL 导入；组合输入法守卫的兄弟依赖
// composition-guard.js?assets=1 随静态依赖图一并解析（barrel 的镜像）。
// C# 侧调用点为 barrel 点分标识符 numericInput.initialize/updateConfig/
// setWheelStepEnabled/dispose（见 components-core.js 与 PrimitiveJsModuleTests 的
// 命名空间 camelCase 规则），本文件同时守护该导出面。
// DOM 交互沿使用最小元素桩（addEventListener/removeEventListener/value/
// selectionStart/setSelectionRange），以 file-URL 导入拿到模块单例后按实例 id 隔离。

import assert from 'node:assert/strict';
import { test } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/numeric-input.js', import.meta.url);

function makeElement() {
    const handlers = new Map();
    return {
        handlers,
        value: '',
        selectionStart: 0,
        addEventListener(type, fn) {
            if (!handlers.has(type)) handlers.set(type, []);
            handlers.get(type).push(fn);
        },
        removeEventListener(type, fn) {
            const list = handlers.get(type) ?? [];
            const index = list.indexOf(fn);
            if (index !== -1) list.splice(index, 1);
        },
        setSelectionRange(start) { this.selectionStart = start; },
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
        invokeMethodAsync(name, arg) {
            calls.push([name, arg]);
            return Promise.resolve();
        },
    };
}

const defaultConfig = {
    disableDebounce: true,
    debounceMs: 500,
    stepKeys: ['ArrowUp', 'ArrowDown', 'PageUp', 'PageDown', 'Home', 'End'],
    allowDecimal: true,
    allowNegative: true,
    decimalSeparator: '.',
    enableWheelStep: false,
};

let nextId = 0;

test('numeric-input.js 可导入且导出面与 numericInput. 点分标识符一一对应', async () => {
    const mod = await import(moduleUrl.href);

    // C# 侧调用点：numericInput.initialize / updateConfig / setWheelStepEnabled / dispose。
    for (const name of ['initialize', 'updateConfig', 'setWheelStepEnabled', 'dispose']) {
        assert.equal(typeof mod[name], 'function', `numeric-input.js 缺少导出 ${name}`);
    }
});

test('initialize 挂载四类监听器且默认不挂 wheel（零截获滚动契约）', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeElement();
    const dotNet = makeDotNet();

    mod.initialize(element, dotNet, `t-${nextId++}`, { ...defaultConfig });

    for (const type of ['input', 'blur', 'focus', 'keydown', 'compositionstart', 'compositionend']) {
        assert.ok(element.count(type) >= 1, `initialize 应挂载 ${type} 监听器`);
    }
    assert.equal(element.count('wheel'), 0);

    // enableWheelStep 显式为 true 时才挂 wheel。
    const eager = makeElement();
    mod.initialize(eager, makeDotNet(), `t-${nextId++}`, { ...defaultConfig, enableWheelStep: true });
    assert.equal(eager.count('wheel'), 1);
});

test('initialize 对缺失入参静默返回（预渲染/半程释放容错）', async () => {
    const mod = await import(moduleUrl.href);

    mod.initialize(null, null, 'x', defaultConfig);
    mod.initialize(makeElement(), null, 'y', defaultConfig);
});

test('input 沿清洗非法字符并即时回调 JsOnInput', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeElement();
    const dotNet = makeDotNet();
    mod.initialize(element, dotNet, `t-${nextId++}`, { ...defaultConfig });

    element.value = '12a3';
    element.dispatch('input');

    assert.equal(element.value, '123');
    assert.deepEqual(dotNet.calls.at(-1), ['JsOnInput', '123']);

    // 小数唯一 + 前导负号 + 多余的点与逗号被剥除。
    element.value = '-1.2.3,';
    element.dispatch('input');
    assert.equal(element.value, '-1.23');
});

test('全角数字与符号经折叠后按 ASCII 处理（日语输入法全角模式）', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeElement();
    const dotNet = makeDotNet();
    mod.initialize(element, dotNet, `t-${nextId++}`, { ...defaultConfig });

    element.value = '１２３．４';
    element.dispatch('input');
    assert.equal(element.value, '123.4');
    assert.deepEqual(dotNet.calls.at(-1), ['JsOnInput', '123.4']);
});

test('allowNegative=false 时负号被清洗剥除', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeElement();
    mod.initialize(element, makeDotNet(), `t-${nextId++}`, { ...defaultConfig, allowNegative: false });

    element.value = '-5';
    element.dispatch('input');
    assert.equal(element.value, '5');
});

test('updateConfig 热切换清洗规则', async () => {
    const mod = await import(moduleUrl.href);
    const id = `t-${nextId++}`;
    const element = makeElement();
    mod.initialize(element, makeDotNet(), id, { ...defaultConfig });

    element.value = '1.2';
    element.dispatch('input');
    assert.equal(element.value, '1.2');

    mod.updateConfig(id, { ...defaultConfig, allowDecimal: false });
    element.value = '3.4';
    element.dispatch('input');
    assert.equal(element.value, '34');
});

test('debounce 模式延迟合并 JsOnInput，blur 即刻冲刷取消挂起', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeElement();
    const dotNet = makeDotNet();
    mod.initialize(element, dotNet, `t-${nextId++}`, { ...defaultConfig, disableDebounce: false, debounceMs: 10 });

    element.value = '7';
    element.dispatch('input');
    assert.equal(dotNet.calls.length, 0); // 防抖窗口内未回调

    element.dispatch('blur'); // blur 取消挂起定时器并直接回调 JsOnBlur
    assert.deepEqual(dotNet.calls.at(-1), ['JsOnBlur', '7']);

    await new Promise((resolve) => setTimeout(resolve, 30));
    // 被取消的 input 定时器不再补发。
    assert.equal(dotNet.calls.filter(([name]) => name === 'JsOnInput').length, 0);

    // focus 回调无参。
    element.dispatch('focus');
    assert.deepEqual(dotNet.calls.at(-1), ['JsOnFocus', undefined]);
});

test('keydown 只对配置的步进键 preventDefault 并回调 JsOnKeyDown', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeElement();
    const dotNet = makeDotNet();
    mod.initialize(element, dotNet, `t-${nextId++}`, { ...defaultConfig, stepKeys: ['ArrowUp', 'ArrowDown'] });

    let prevented = 0;
    const stepEvent = { key: 'ArrowUp', preventDefault() { prevented++; } };
    element.dispatch('keydown', stepEvent);
    assert.equal(prevented, 1);
    assert.deepEqual(dotNet.calls.at(-1), ['JsOnKeyDown', 'ArrowUp']);

    const plainEvent = { key: 'a', preventDefault() { prevented++; } };
    element.dispatch('keydown', plainEvent);
    assert.equal(prevented, 1); // 未被二次 preventDefault
    assert.equal(dotNet.calls.length, 1);
});

test('wheel 沿：阈值折算、方向映射、步数封顶与增量累积', async () => {
    const mod = await import(moduleUrl.href);
    const id = `t-${nextId++}`;
    const element = makeElement();
    const dotNet = makeDotNet();
    mod.initialize(element, dotNet, id, { ...defaultConfig, enableWheelStep: true });

    // handleWheel 读取 document.activeElement——Node 无 DOM，给全局最小桩。
    const previousDocument = globalThis.document;
    globalThis.document = { activeElement: element };
    try {
        let prevented = 0;
        const wheel = (deltaY, deltaMode = 0) => {
            element.dispatch('wheel', {
                deltaY,
                deltaMode,
                preventDefault() { prevented++; },
            });
        };

        // 非零小增量：不足阈值，不回调但仍接管滚动。
        wheel(60);
        assert.equal(dotNet.calls.length, 0);
        assert.equal(prevented, 1);

        // 累积越过 +100：向下滚 -> ArrowDown 一次；余数结转。
        wheel(50);
        assert.deepEqual(dotNet.calls, [['JsOnKeyDown', 'ArrowDown']]);

        // 方向反转：累加器重置后计入 -150 -> ArrowUp 一次。
        wheel(-150);
        assert.equal(dotNet.calls.length, 2);
        assert.deepEqual(dotNet.calls.at(-1), ['JsOnKeyDown', 'ArrowUp']);

        // 行模式（Firefox）：3 行折算 100px，1 行 = 33.3px。
        dotNet.calls.length = 0;
        wheel(2, 1); // 2 行 * 33.3 ≈ 66.6，不足一步
        wheel(1, 1); // 累计 ≈ 100 -> 一步
        assert.deepEqual(dotNet.calls.at(-1), ['JsOnKeyDown', 'ArrowDown']);

        // 单事件步数封顶：+1500px 折 15 步，压到 10 步。
        dotNet.calls.length = 0;
        wheel(1500);
        assert.equal(dotNet.calls.length, 10);
        assert.ok(dotNet.calls.every(([name, key]) => name === 'JsOnKeyDown' && key === 'ArrowDown'));
    } finally {
        if (previousDocument === undefined) delete globalThis.document;
        else globalThis.document = previousDocument;
    }
});

test('setWheelStepEnabled 挂摘 wheel 监听器并重置累加器', async () => {
    const mod = await import(moduleUrl.href);
    const id = `t-${nextId++}`;
    const element = makeElement();
    mod.initialize(element, makeDotNet(), id, { ...defaultConfig });
    assert.equal(element.count('wheel'), 0);

    mod.setWheelStepEnabled(id, true);
    assert.equal(element.count('wheel'), 1);

    // 幂等：重复开启不加第二份监听器。
    mod.setWheelStepEnabled(id, true);
    assert.equal(element.count('wheel'), 1);

    mod.setWheelStepEnabled(id, false);
    assert.equal(element.count('wheel'), 0);

    // 未知实例静默。
    mod.setWheelStepEnabled('nope', true);
});

test('dispose 摘除全部监听器（含 wheel 与组合守卫），之后事件不再回调', async () => {
    const mod = await import(moduleUrl.href);
    const id = `t-${nextId++}`;
    const element = makeElement();
    const dotNet = makeDotNet();
    mod.initialize(element, dotNet, id, { ...defaultConfig, enableWheelStep: true });

    mod.dispose(id);
    for (const type of ['input', 'blur', 'focus', 'keydown', 'wheel', 'compositionstart', 'compositionend']) {
        assert.equal(element.count(type), 0, `dispose 后应摘除 ${type}`);
    }

    // 幂等：二次 dispose 与未知实例都不抛。
    mod.dispose(id);

    // 防抖定时器在 dispose 时被清掉：挂起的回调不再发出。
    const late = makeElement();
    const lateDotNet = makeDotNet();
    const lateId = `t-${nextId++}`;
    mod.initialize(late, lateDotNet, lateId, { ...defaultConfig, disableDebounce: false, debounceMs: 10 });
    late.value = '8';
    late.dispatch('input');
    mod.dispose(lateId);
    await new Promise((resolve) => setTimeout(resolve, 30));
    assert.equal(lateDotNet.calls.length, 0);
});
