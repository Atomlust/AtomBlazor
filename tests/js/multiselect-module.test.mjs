// multiselect.js 的真实文件导入与行为测试。
// 模块顶层只建 Map（零 DOM 触碰），HTMLElement/document 仅在 setup 与导航路径内使用，
// 因此可在无 DOM 的 Node 环境以 file-URL 导入；兄弟依赖 composition-guard.js?assets=1
// 随静态依赖图一并解析（barrel 的镜像）。
// C# 侧调用点为 barrel 点分标识符 multiselect.setupMultiSelectInput /
// multiselect.removeMultiSelectInput（见 components-core.js 与 PrimitiveJsModuleTests
// 的命名空间 camelCase 规则），disposeAll/focusElementWithPreventScroll 为模块自有
// 工具导出，本文件同时守护该导出面。
// DOM 交互沿用 tag-input-module.test.mjs 先例的最小元素桩（inputElement 必须是
// globalThis.HTMLElement 桩的实例——setup 里有 instanceof 检查），option 桩提供
// offsetParent/getAttribute/setAttribute/click/scrollIntoView 面，document 桩按
// contentId 返回含 role="option" 列表的内容容器，dotNetRef 桩以变参记录
// invokeMethodAsync 调用。inputHandler 与 focus 沿走真实 setTimeout，测试以短等待排空。

import assert from 'node:assert/strict';
import { test, afterEach } from 'node:test';

// 元素桩基类：setupMultiSelectInput 校验 inputElement instanceof HTMLElement
class StubHTMLElement {
}
globalThis.HTMLElement ??= StubHTMLElement;

const documents = new Map(); // contentId -> 内容桩（含 options 列表）

globalThis.document ??= {
    getElementById(id) {
        return documents.get(id) ?? null;
    },
};

function makeElement() {
    const handlers = new Map();
    const el = new StubHTMLElement();
    Object.assign(el, {
        handlers,
        value: '',
        focusCalls: [],
        focus(options) { el.focusCalls.push(options); },
        // composition-guard 在 compositionend 时重派发 input 事件
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
    });
    return el;
}

// option 桩：可见性走 offsetParent，禁用走 aria-disabled，data-focused 供断言。
function makeOption({ disabled = false, visible = true } = {}) {
    const attrs = { 'aria-disabled': disabled ? 'true' : 'false' };
    return {
        clicks: 0,
        offsetParent: visible ? {} : null,
        getAttribute(name) { return attrs[name] ?? null; },
        setAttribute(name, value) { attrs[name] = String(value); },
        click() { this.clicks++; },
        scrollIntoView() {},
        get focused() { return attrs['data-focused'] === 'true'; },
    };
}

function makeContent(options) {
    return { querySelectorAll: () => options };
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

// 键盘事件桩：preventDefault/stopPropagation 可观察，keyCode 默认非 229。
function keyEvent(key, overrides = {}) {
    return {
        key,
        keyCode: 0,
        defaultPrevented: false,
        propagationStopped: false,
        preventDefault() { this.defaultPrevented = true; },
        stopPropagation() { this.propagationStopped = true; },
        ...overrides,
    };
}

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

let nextId = 0;
const ids = [];

afterEach(async () => {
    const mod = await import(moduleUrl);
    mod.disposeAll();
    ids.length = 0;
    documents.clear();
});

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/multiselect.js', import.meta.url);

// 挂载一套 输入框 + 列表内容 桩；contentId 与 C# 侧 $"{Id}-listbox" 同形。
async function mount({ input = makeElement(), dotNet = makeDotNet(), options = [] } = {}) {
    const mod = await import(moduleUrl);
    const inputId = `ms-search-${nextId}`;
    const contentId = `ms-listbox-${nextId++}`;
    documents.set(contentId, makeContent(options));
    ids.push(inputId);
    mod.setupMultiSelectInput(input, dotNet, inputId, contentId);
    return { mod, input, dotNet, inputId, contentId, options };
}

test('multiselect.js 可导入且导出面与 C# 点分标识符及清理工具一一对应', async () => {
    const mod = await import(moduleUrl);

    // C# 侧调用点：multiselect.setupMultiSelectInput / multiselect.removeMultiSelectInput；
    // 另有两个模块自有导出。
    for (const name of [
        'setupMultiSelectInput',
        'removeMultiSelectInput',
        'disposeAll',
        'focusElementWithPreventScroll',
    ]) {
        assert.equal(typeof mod[name], 'function', `multiselect.js 缺少导出 ${name}`);
    }
});

test('setupMultiSelectInput 对缺失入参记错并静默返回', async () => {
    const { mod } = await mount();

    const errors = [];
    const original = console.error;
    console.error = (...args) => errors.push(args.join(' '));
    try {
        mod.setupMultiSelectInput(null, makeDotNet(), 'bad-1', 'c-1');
        mod.setupMultiSelectInput(makeElement(), null, 'bad-2', 'c-2');
        // 非 HTMLElement 实例同样被拒
        mod.setupMultiSelectInput({ bad: true }, makeDotNet(), 'bad-3', 'c-3');
        mod.removeMultiSelectInput('bad-1'); // 未登记 id：无副作用
    } finally {
        console.error = original;
    }

    assert.equal(errors.length, 3);
    for (const message of errors) {
        assert.match(message, /setupMultiSelectInput: missing required parameters/);
    }
});

test('setup 在输入框挂 keydown（捕获）/input 与组合守卫监听', async () => {
    const { input } = await mount();

    assert.equal(input.count('keydown'), 1);
    // 自有 inputHandler + 守卫的抑制监听同在 input 类型上
    assert.equal(input.count('input'), 2);
    // composition-guard：compositionstart/compositionend/blur + 抑制 input（捕获）
    for (const type of ['compositionstart', 'compositionend', 'blur']) {
        assert.ok(input.count(type) >= 1, `缺少 ${type} 监听器`);
    }
});

test('方向键移动 data-focused 并在两端回绕，选中状态不被改写', async () => {
    const options = [makeOption(), makeOption(), makeOption()];
    const { input } = await mount({ options });

    input.dispatch('keydown', keyEvent('ArrowDown')); // 无聚焦 -> 首项
    assert.ok(options[0].focused);
    assert.equal(options[0].getAttribute('aria-selected'), null); // 不碰 aria-selected

    input.dispatch('keydown', keyEvent('ArrowDown')); // -> 次项，首项让位
    assert.ok(options[1].focused);
    assert.equal(options[0].getAttribute('data-focused'), 'false');

    input.dispatch('keydown', keyEvent('ArrowDown')); // -> 末项
    input.dispatch('keydown', keyEvent('ArrowDown')); // 回绕到首项
    assert.ok(options[0].focused);

    input.dispatch('keydown', keyEvent('ArrowUp')); // 从头再回绕到末项
    assert.ok(options[2].focused);
});

test('Home/End 直达首末项', async () => {
    const options = [makeOption(), makeOption(), makeOption()];
    const { input } = await mount({ options });

    input.dispatch('keydown', keyEvent('End'));
    assert.ok(options[2].focused);

    input.dispatch('keydown', keyEvent('Home'));
    assert.ok(options[0].focused);
});

test('不可见与禁用条目被导航跳过', async () => {
    const visible = [makeOption(), makeOption()];
    const skipped = [makeOption({ visible: false }), makeOption({ disabled: true })];
    const { input } = await mount({ options: [...skipped, ...visible] });

    input.dispatch('keydown', keyEvent('ArrowDown'));
    assert.ok(visible[0].focused);
    for (const hidden of skipped) {
        assert.equal(hidden.getAttribute('data-focused'), null);
    }
});

test('Space 在有条目聚焦时点击该项并回调 HandleSpace；无聚焦时放行', async () => {
    const options = [makeOption(), makeOption()];
    const { input, dotNet } = await mount({ options });

    // 未聚焦任何条目：空格字符放行给搜索框
    const free = keyEvent(' ');
    input.dispatch('keydown', free);
    assert.ok(!free.defaultPrevented);
    assert.deepEqual(dotNet.calls, []);

    input.dispatch('keydown', keyEvent('ArrowDown')); // 聚焦首项
    const space = keyEvent(' ');
    input.dispatch('keydown', space);
    assert.ok(space.defaultPrevented);
    assert.equal(options[0].clicks, 1);
    assert.deepEqual(dotNet.calls, [['HandleSpace']]);
});

test('Enter 点击聚焦条目并回调 HandleEnter；无聚焦也回调关闭', async () => {
    const options = [makeOption(), makeOption()];
    const { input, dotNet } = await mount({ options });

    input.dispatch('keydown', keyEvent('ArrowDown'));
    const enter = keyEvent('Enter');
    input.dispatch('keydown', enter);
    assert.ok(enter.defaultPrevented);
    assert.equal(options[0].clicks, 1);
    assert.deepEqual(dotNet.calls, [['HandleEnter']]);

    // 重新挂载：无聚焦条目时 Enter 仍触发关闭回调
    const second = await mount();
    const bare = keyEvent('Enter');
    second.input.dispatch('keydown', bare);
    assert.ok(bare.defaultPrevented);
    assert.deepEqual(second.dotNet.calls, [['HandleEnter']]);
});

test('Escape 阻断冒泡并回调 HandleEscape', async () => {
    const { input, dotNet } = await mount({ options: [makeOption()] });

    const escape = keyEvent('Escape');
    input.dispatch('keydown', escape);

    assert.ok(escape.defaultPrevented);
    assert.ok(escape.propagationStopped);
    assert.deepEqual(dotNet.calls, [['HandleEscape']]);
});

test('列表为空时导航键放行，Enter/Escape/Space 仍被处理', async () => {
    const { input, dotNet } = await mount({ options: [] });

    const down = keyEvent('ArrowDown');
    input.dispatch('keydown', down);
    assert.ok(!down.defaultPrevented);
    assert.deepEqual(dotNet.calls, []);

    input.dispatch('keydown', keyEvent('Escape'));
    assert.deepEqual(dotNet.calls, [['HandleEscape']]);
});

test('键入（input 事件）复位聚焦索引并清空聚焦态', async () => {
    const options = [makeOption(), makeOption()];
    const { input, dotNet } = await mount({ options });

    input.dispatch('keydown', keyEvent('ArrowDown'));
    assert.ok(options[0].focused);

    input.dispatch('input', {});
    await sleep(80); // inputHandler 的 50ms 延时排空（等 Blazor 重渲染）

    assert.equal(options[0].getAttribute('data-focused'), 'false');

    // 聚焦已复位：Space 不再拦截
    const space = keyEvent(' ');
    input.dispatch('keydown', space);
    assert.ok(!space.defaultPrevented);
    assert.deepEqual(dotNet.calls, []);
});

test('内容元素缺失时导航安全无操作', async () => {
    const { input, dotNet } = await mount({ options: [makeOption()] });
    documents.clear(); // getElementById 从此返回 null
    documents.clear(); // getElementById 从此返回 null

    const down = keyEvent('ArrowDown');
    input.dispatch('keydown', down);
    assert.ok(!down.defaultPrevented);

    input.dispatch('keydown', keyEvent('Enter'));
    assert.deepEqual(dotNet.calls, [['HandleEnter']]); // 无条目也要走关闭
});

test('输入法组合期间按键全部放行', async () => {
    const options = [makeOption()];
    const { input, dotNet } = await mount({ options });

    input.dispatch('compositionstart', {}); // 真守卫置 isComposing

    for (const key of ['Enter', 'Escape', ' ', 'ArrowDown']) {
        const e = keyEvent(key);
        input.dispatch('keydown', e);
        assert.ok(!e.defaultPrevented, `${key} 组合期间不应被拦截`);
    }
    assert.deepEqual(dotNet.calls, []);

    // keyCode 229 的旧信号同样被抑制（组合已结束，仅 229 在场）
    input.dispatch('compositionend', {});
    const legacy = keyEvent('Enter', { keyCode: 229 });
    input.dispatch('keydown', legacy);
    assert.ok(!legacy.defaultPrevented);
});

test('removeMultiSelectInput 摘除全部监听器且事件不再上报', async () => {
    const options = [makeOption()];
    const { mod, input, dotNet, inputId } = await mount({ options });

    mod.removeMultiSelectInput(inputId);

    const enter = keyEvent('Enter');
    input.dispatch('keydown', enter);
    input.dispatch('compositionstart', {});

    assert.ok(!enter.defaultPrevented);
    assert.deepEqual(dotNet.calls, []);
    assert.equal(input.count('keydown'), 0);
    assert.equal(input.count('input'), 0); // 自有 inputHandler 与守卫抑制监听一并摘除
    assert.equal(input.count('compositionstart'), 0);
    assert.equal(input.count('compositionend'), 0);
    assert.equal(input.count('blur'), 0);

    mod.removeMultiSelectInput(inputId); // 二次摘除无害
});

test('disposeAll 清理全部实例', async () => {
    const a = await mount();
    const b = await mount();

    a.mod.disposeAll();

    for (const { input, dotNet } of [a, b]) {
        const enter = keyEvent('Enter');
        input.dispatch('keydown', enter);
        assert.ok(!enter.defaultPrevented);
        assert.deepEqual(dotNet.calls, []);
    }

    a.mod.disposeAll(); // 空表再清无害
});

test('focusElementWithPreventScroll 延时聚焦且不带滚动', async () => {
    const mod = await import(moduleUrl);
    const el = makeElement();

    mod.focusElementWithPreventScroll(el);
    mod.focusElementWithPreventScroll(null); // 空引用无副作用
    assert.deepEqual(el.focusCalls, []); // 尚未到期

    await sleep(20); // setTimeout(…, 10) 到期
    assert.deepEqual(el.focusCalls, [{ preventScroll: true }]);
});
