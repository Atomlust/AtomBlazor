// tag-input.js 的真实文件导入与行为测试。
// 模块顶层只建 Map（零 DOM 触碰），document 仅在 composition-guard 内部使用，
// 因此可在无 DOM 的 Node 环境以 file-URL 导入；兄弟依赖 composition-guard.js?assets=1
// 随静态依赖图一并解析（barrel 的镜像）。
// C# 侧调用点为 barrel 点分标识符 tagInput.initialize/focusInput/dispose（见
// components-core.js 与 PrimitiveJsModuleTests 的命名空间 camelCase 规则），
// 本文件同时守护该导出面。
// DOM 交互沿用 numeric-input-module.test.mjs 先例的最小元素桩（增加 contains/closest
// 桩给容器点击沿），dotNetRef 桩以变参记录 invokeMethodAsync 调用。

import assert from 'node:assert/strict';
import { test, afterEach } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/tag-input.js', import.meta.url);

function makeElement({ focusCalls = null } = {}) {
    const handlers = new Map();
    const el = {
        handlers,
        value: '',
        focus() { if (focusCalls) focusCalls.push(this); },
        // composition-guard 在 suppress input 时于 compositionend 重派发 input 事件
        dispatchEvent() {},
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

// 键盘事件桩：preventDefault 可观察，keyCode 默认非 229。
function keyEvent(key, overrides = {}) {
    return { key, keyCode: 0, preventDefault() { this.defaultPrevented = true; }, ...overrides };
}

// 全触发位掩码：Enter|Comma|Space|Tab|Semicolon = 31。
const ALL_TRIGGERS = 31;

let nextId = 0;
const ids = [];

afterEach(async () => {
    const mod = await import(moduleUrl);
    while (ids.length) mod.dispose(ids.pop());
});

const mount = async ({ triggers = ALL_TRIGGERS, input, container, dotNet } = {}) => {
    const mod = await import(moduleUrl);
    const id = `t-${nextId++}`;
    ids.push(id);
    mod.initialize(
        container ?? makeElement(),
        input ?? makeElement(),
        dotNet ?? makeDotNet(),
        id,
        { triggers });
    return { mod, id };
};

test('tag-input.js 可导入且导出面与 tagInput. 点分标识符一一对应', async () => {
    const mod = await import(moduleUrl);

    // C# 侧调用点：tagInput.initialize / focusInput / dispose。
    for (const name of ['initialize', 'focusInput', 'dispose']) {
        assert.equal(typeof mod[name], 'function', `tag-input.js 缺少导出 ${name}`);
    }
});

test('initialize 对缺失入参静默返回', async () => {
    const mod = await import(moduleUrl);

    mod.initialize(null, makeElement(), makeDotNet(), 't-x', { triggers: 1 });
    mod.initialize(makeElement(), null, makeDotNet(), 't-x', { triggers: 1 });
    mod.initialize(makeElement(), makeElement(), null, 't-x', { triggers: 1 });
});

test('initialize 在输入框挂 keydown/paste/组合守卫并在容器挂 click', async () => {
    const input = makeElement();
    const container = makeElement();
    await mount({ input, container });

    assert.equal(input.count('keydown'), 1);
    assert.equal(input.count('paste'), 1);
    // composition-guard：compositionstart/compositionend/blur + suppress input（捕获）
    for (const type of ['compositionstart', 'compositionend', 'blur', 'input']) {
        assert.ok(input.count(type) >= 1, `缺少 ${type} 监听器`);
    }
    assert.equal(container.count('click'), 1);
});

test('已配置触发键应 preventDefault 并回调 JsTriggerAdd', async () => {
    const input = makeElement();
    const dotNet = makeDotNet();
    await mount({ input, dotNet });

    for (const key of ['Enter', ',', ' ', 'Tab', ';']) {
        const e = keyEvent(key);
        input.dispatch('keydown', e);
        assert.ok(e.defaultPrevented, `${key} 应被 preventDefault`);
    }

    assert.equal(dotNet.calls.filter(([name]) => name === 'JsTriggerAdd').length, 5);
});

test('未配置触发位时按键不拦截不上报', async () => {
    const input = makeElement();
    const dotNet = makeDotNet();
    const { id } = await mount({ triggers: 0, input, dotNet });

    const e = keyEvent('Enter');
    input.dispatch('keydown', e);

    assert.ok(!e.defaultPrevented);
    assert.deepEqual(dotNet.calls, []);
});

test('空输入框 Backspace 回调 JsBackspace；非空不回调', async () => {
    const input = makeElement();
    const dotNet = makeDotNet();
    const { id } = await mount({ input, dotNet });

    input.dispatch('keydown', keyEvent('Backspace'));
    assert.deepEqual(dotNet.calls, [['JsBackspace']]);

    dotNet.calls.length = 0;
    input.value = 'a';
    input.dispatch('keydown', keyEvent('Backspace'));
    assert.deepEqual(dotNet.calls, []);
});

test('方向键与 Escape 驱动建议导航回调', async () => {
    const input = makeElement();
    const dotNet = makeDotNet();
    const { id } = await mount({ input, dotNet });

    const down = keyEvent('ArrowDown');
    input.dispatch('keydown', down);
    const up = keyEvent('ArrowUp');
    input.dispatch('keydown', up);
    input.dispatch('keydown', keyEvent('Escape'));

    assert.deepEqual(dotNet.calls, [
        ['JsSuggestionNext'],
        ['JsSuggestionPrev'],
        ['JsSuggestionClose'],
    ]);
    assert.ok(down.defaultPrevented && up.defaultPrevented);
});

test('输入法组合期间按键全部放行不上报', async () => {
    const input = makeElement();
    const dotNet = makeDotNet();
    const { id } = await mount({ input, dotNet });

    // 模拟组合开始：compositionguard 置位 isComposing
    input.dispatch('compositionstart', {});

    input.dispatch('keydown', keyEvent('Enter'));
    input.dispatch('keydown', keyEvent('ArrowDown'));
    input.dispatch('keydown', keyEvent('Escape'));
    // keyCode 229 的旧信号同样被抑制
    input.dispatch('compositionend', {});
    input.dispatch('keydown', keyEvent('Enter', { keyCode: 229 }));

    assert.deepEqual(dotNet.calls, []);
});

test('含已配置分隔符的粘贴应 preventDefault 并回调 JsPasteText', async () => {
    const input = makeElement();
    const dotNet = makeDotNet();
    const { id } = await mount({ input, dotNet });

    const paste = (text) => {
        const e = { clipboardData: { getData: () => text }, preventDefault() { this.defaultPrevented = true; } };
        input.dispatch('paste', e);
        return e;
    };

    const comma = paste('a,b');
    assert.ok(comma.defaultPrevented);
    const semi = paste('a;b');
    assert.ok(semi.defaultPrevented);
    const space = paste('a b');
    assert.ok(space.defaultPrevented);

    // 无分隔符或空文本：不拦截不上报
    const plain = paste('ab');
    assert.ok(!plain.defaultPrevented);
    paste('');

    const reported = dotNet.calls.filter(([name]) => name === 'JsPasteText');
    assert.deepEqual(reported, [['JsPasteText', 'a,b'], ['JsPasteText', 'a;b'], ['JsPasteText', 'a b']]);
});

test('分隔符检测跟随触发位配置', async () => {
    const input = makeElement();
    const dotNet = makeDotNet();
    // 仅逗号位（Enter|Comma = 3）
    const { id } = await mount({ triggers: 3, input, dotNet });

    const semi = { clipboardData: { getData: () => 'a;b' }, preventDefault() { this.defaultPrevented = true; } };
    input.dispatch('paste', semi);
    assert.ok(!semi.defaultPrevented);

    const comma = { clipboardData: { getData: () => 'a,b' }, preventDefault() { this.defaultPrevented = true; } };
    input.dispatch('paste', comma);
    assert.ok(comma.defaultPrevented);
    assert.deepEqual(dotNet.calls, [['JsPasteText', 'a,b']]);
});

test('容器点击应聚焦输入框但不抢按钮焦点', async () => {
    const focusCalls = [];
    const input = makeElement({ focusCalls });
    const container = makeElement();
    const { id } = await mount({ input, container });

    // 点在标签移除按钮上：不聚焦
    container.dispatch('click', { target: { closest: () => ({}) } });
    assert.equal(focusCalls.length, 0);

    // 点在容器空白处：聚焦
    container.dispatch('click', { target: { closest: () => null } });
    assert.equal(focusCalls.length, 1);
});

test('focusInput 聚焦已存实例；未知 id 无害', async () => {
    const focusCalls = [];
    const input = makeElement({ focusCalls });
    const mod = await import(moduleUrl);
    const id = `t-${nextId++}`;
    ids.push(id);
    mod.initialize(makeElement(), input, makeDotNet(), id, { triggers: 1 });

    mod.focusInput(id);
    assert.equal(focusCalls.length, 1);

    mod.focusInput('未知id'); // 不抛错即通过
});

test('dispose 摘除全部监听器且事件不再上报', async () => {
    const input = makeElement();
    const container = makeElement();
    const dotNet = makeDotNet();
    const mod = await import(moduleUrl);
    const id = `t-${nextId++}`;
    mod.initialize(container, input, dotNet, id, { triggers: ALL_TRIGGERS });

    mod.dispose(id);

    const e = keyEvent('Enter');
    input.dispatch('keydown', e);
    container.dispatch('click', { target: { closest: () => null } });

    assert.deepEqual(dotNet.calls, []);
    assert.ok(!e.defaultPrevented);
    assert.ok(!input.handlers.get('keydown')?.length);
    assert.ok(!input.handlers.get('paste')?.length);
    assert.ok(!container.handlers.get('click')?.length);
});
