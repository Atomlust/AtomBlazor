// segmented-input.js 的真实文件导入与行为测试。
// 模块顶层只建 WeakMap（零 DOM 触碰），getComputedStyle/Event 都在调用时才触碰，
// 因此可在无 DOM 的 Node 环境以 file-URL 导入，调用前挂全局桩。
// C# 侧调用点为 barrel 点分标识符 segmentedInput.initialize/dispose（见
// components-core.js 与 PrimitiveJsModuleTests 的命名空间 camelCase 规则），
// 本文件同时守护该导出面。
// DOM 交互沿用 cascader-module.test.mjs 先例的最小元素桩（补 value/select/
// dispatchEvent 与 input[data-atom-segment] 复合选择器子集），分段属性名按本仓库
// 改名规则为 data-atom-segment。

import assert from 'node:assert/strict';
import { test, beforeEach } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/segmented-input.js', import.meta.url);

// ── 最小 DOM 桩 ────────────────────────────────────────────────────────────

class StubEl {
    constructor(attrs = {}, children = []) {
        this.attrs = attrs;
        this.children = [];
        this.parent = null;
        for (const child of children) this.appendChild(child);
        this.handlers = new Map();
        this.value = '';
        this.disabled = false;
        this.readOnly = false;
        this.selects = 0;
        this.focuses = 0;
        this.inputDispatches = 0;
    }

    appendChild(child) {
        child.parent = this;
        this.children.push(child);
        return child;
    }

    // data-* 属性 → camelCase dataset（浏览器语义的桩化子集）
    get dataset() {
        const out = {};
        for (const [name, value] of Object.entries(this.attrs)) {
            if (!name.startsWith('data-')) continue;
            const key = name.slice(5).replace(/-(\w)/g, (_, c) => c.toUpperCase());
            out[key] = value;
        }
        return out;
    }

    addEventListener(type, fn) {
        if (!this.handlers.has(type)) this.handlers.set(type, []);
        this.handlers.get(type).push(fn);
    }

    removeEventListener(type, fn) {
        const list = this.handlers.get(type) ?? [];
        const index = list.indexOf(fn);
        if (index !== -1) list.splice(index, 1);
    }

    dispatch(type, event = {}) {
        for (const fn of [...(this.handlers.get(type) ?? [])]) fn(event);
    }

    // 模块内部以 input.dispatchEvent(new Event('input')) 把自旋结果送交 Blazor 绑定。
    dispatchEvent(event) {
        if (event.type === 'input') this.inputDispatches++;
        this.dispatch(event.type, event);
    }

    count(type) { return (this.handlers.get(type) ?? []).length; }

    focus() { this.focuses++; }
    select() { this.selects++; }

    // 桩仅需支持模块实际用到的两个选择器形态。
    matches(sel) {
        if (sel === 'input[data-atom-segment]') {
            return this.attrs.tag === 'input' && 'data-atom-segment' in this.attrs;
        }
        throw new Error(`桩不支持选择器：${sel}`);
    }

    descendants() {
        const out = [];
        for (const child of this.children) {
            out.push(child, ...child.descendants());
        }
        return out;
    }

    querySelectorAll(sel) {
        if (sel === 'input[data-atom-segment]:not(:disabled)') {
            return this.descendants().filter(el => el.matches('input[data-atom-segment]') && !el.disabled);
        }
        throw new Error(`桩不支持选择器：${sel}`);
    }

    querySelector(sel) { return this.querySelectorAll(sel)[0] ?? null; }
}

function inputSegment(segment, attrs = {}) {
    return new StubEl({ tag: 'input', 'data-atom-segment': segment, ...attrs });
}

function keyEvent(key, target, overrides = {}) {
    return {
        key,
        target,
        altKey: false,
        ctrlKey: false,
        metaKey: false,
        shiftKey: false,
        isComposing: false,
        preventDefault() { this.defaultPrevented = true; },
        ...overrides,
    };
}

// 构造 root + 指定分段 input 的组。
function makeGroup(specs) {
    const root = new StubEl({});
    const inputs = specs.map(spec => {
        const el = inputSegment(spec.segment, spec.attrs);
        el.value = spec.value ?? '';
        el.disabled = spec.disabled ?? false;
        el.readOnly = spec.readOnly ?? false;
        root.appendChild(el);
        return el;
    });
    return { root, inputs };
}

beforeEach(() => {
    globalThis.getComputedStyle = () => ({ direction: 'ltr' });
});

// ── 导出面 ────────────────────────────────────────────────────────────────

test('segmented-input.js 可导入且导出面与 segmentedInput. 点分标识符一一对应', async () => {
    const mod = await import(moduleUrl);
    // C# 侧调用点：segmentedInput.initialize / segmentedInput.dispose。
    for (const name of ['initialize', 'dispose']) {
        assert.equal(typeof mod[name], 'function', `segmented-input.js 缺少导出 ${name}`);
    }
});

// ── initialize：注册与幂等 ────────────────────────────────────────────────

test('initialize 挂 focusin/keydown 监听；重复与空 root 幂等静默', async () => {
    const mod = await import(moduleUrl);
    const { root, inputs } = makeGroup([{ segment: 'Day', attrs: { 'data-digits': '2' } }]);

    mod.initialize(root);
    assert.equal(root.count('focusin'), 1);
    assert.equal(root.count('keydown'), 1);

 // 同一 root 二次 initialize 直接返回：WeakMap 已登记，监听不翻倍。
    mod.initialize(root);
    assert.equal(root.count('focusin'), 1);
    assert.equal(root.count('keydown'), 1);

    mod.initialize(null); // 空 root 静默
    assert.equal(inputs[0].focuses, 0);
});

test('focusin 选中分段文本，非分段目标不动', async () => {
    const mod = await import(moduleUrl);
    const { root, inputs } = makeGroup([
        { segment: 'Day', attrs: { 'data-digits': '2' } },
    ]);
    root.appendChild(new StubEl({})); // 非 input 分段子节点
    mod.initialize(root);

    root.dispatch('focusin', { target: inputs[0] });
    assert.equal(inputs[0].selects, 1);

    root.dispatch('focusin', { target: root.children[1] });
    assert.equal(root.children[1].selects, 0); // 非分段目标不选中
});

// ── ArrowLeft/ArrowRight：段间移焦 ────────────────────────────────────────

test('ArrowRight/ArrowLeft 在启用分段间移焦并全选，越界与禁用段跳过', async () => {
    const mod = await import(moduleUrl);
    const { root, inputs } = makeGroup([
        { segment: 'Day' },
        { segment: 'Month', disabled: true },
        { segment: 'Year' },
    ]);
    mod.initialize(root);
    const [day, , year] = inputs;

    const right = keyEvent('ArrowRight', day);
    root.dispatch('keydown', right);
    assert.ok(right.defaultPrevented);
    assert.equal(year.focuses, 1); // 禁用段被 :not(:disabled) 选择器滤除，直接跳到 Year
    assert.equal(year.selects, 1);
    assert.equal(day.focuses, 0);

    const left = keyEvent('ArrowLeft', year);
    root.dispatch('keydown', left);
    assert.ok(left.defaultPrevented);
    assert.equal(day.focuses, 1);

    // 末段右移无后继：不 preventDefault、不移焦。
    const over = keyEvent('ArrowRight', year);
    root.dispatch('keydown', over);
    assert.ok(!over.defaultPrevented);
});

test('RTL 方向下左右键语义镜像', async () => {
    const mod = await import(moduleUrl);
    const { root, inputs } = makeGroup([{ segment: 'Day' }, { segment: 'Year' }]);
    mod.initialize(root);
    globalThis.getComputedStyle = () => ({ direction: 'rtl' });
    try {
        const right = keyEvent('ArrowRight', inputs[1]);
        root.dispatch('keydown', right);
        assert.ok(right.defaultPrevented);
        assert.equal(inputs[0].focuses, 1);
    } finally {
        globalThis.getComputedStyle = () => ({ direction: 'ltr' });
    }
});

// ── ArrowUp/ArrowDown/Home/End：自旋与夹取 ────────────────────────────────

test('ArrowUp/ArrowDown 按 min/max 夹取、padStart 补零并派发 input 事件', async () => {
    const mod = await import(moduleUrl);
    const { root, inputs } = makeGroup([
        { segment: 'Month', value: '04', attrs: { 'data-min': '1', 'data-max': '12', 'data-digits': '2' } },
    ]);
    mod.initialize(root);
    const month = inputs[0];

    const up = keyEvent('ArrowUp', month);
    root.dispatch('keydown', up);
    assert.ok(up.defaultPrevented);
    assert.equal(month.value, '05');
    assert.equal(month.inputDispatches, 1);
    assert.equal(month.selects, 1);

    month.value = '12';
    root.dispatch('keydown', keyEvent('ArrowUp', month));
    assert.equal(month.value, '12'); // 上界夹取不回绕

    month.value = '01';
    root.dispatch('keydown', keyEvent('ArrowDown', month));
    assert.equal(month.value, '01'); // 下界夹取不回绕
});

test('data-step 参与分钟自旋', async () => {
    const mod = await import(moduleUrl);
    const { root, inputs } = makeGroup([
        { segment: 'Minute', value: '10', attrs: { 'data-min': '0', 'data-max': '59', 'data-digits': '2', 'data-step': '15' } },
    ]);
    mod.initialize(root);
    root.dispatch('keydown', keyEvent('ArrowUp', inputs[0]));
    assert.equal(inputs[0].value, '25');
});

test('空段自旋： quirk——ArrowDown 空段也落到 min（不向下走）', async () => {
    const mod = await import(moduleUrl);
    const { root, inputs } = makeGroup([
        { segment: 'Day', value: '', attrs: { 'data-min': '1', 'data-max': '31', 'data-digits': '2' } },
    ]);
    mod.initialize(root);
    const day = inputs[0];

    root.dispatch('keydown', keyEvent('ArrowDown', day));
    assert.equal(day.value, '01'); // empty ? min：与 ArrowUp 同值，bug-for-bug 保留

    root.dispatch('keydown', keyEvent('Home', day));
    assert.equal(day.value, '01');

    root.dispatch('keydown', keyEvent('End', day));
    assert.equal(day.value, '31');
});

test('非数字残值按 NaN 兜底回 min 再步进', async () => {
    const mod = await import(moduleUrl);
    const { root, inputs } = makeGroup([
        { segment: 'Day', value: 'x', attrs: { 'data-min': '1', 'data-max': '31', 'data-digits': '2' } },
    ]);
    mod.initialize(root);
    root.dispatch('keydown', keyEvent('ArrowUp', inputs[0]));
    assert.equal(inputs[0].value, '02'); // (isFinite(NaN) ? NaN : 1) + 1
});

// ── 忽略面：修饰键/只读/无关按键 ──────────────────────────────────────────

test('修饰键、isComposing、readOnly、非分段目标与无关按键全部放行', async () => {
    const mod = await import(moduleUrl);
    const { root, inputs } = makeGroup([
        { segment: 'Day', value: '10', attrs: { 'data-min': '1', 'data-max': '31', 'data-digits': '2' } },
    ]);
    mod.initialize(root);
    const day = inputs[0];

    const cases = [
        keyEvent('ArrowUp', day, { ctrlKey: true }),
        keyEvent('ArrowUp', day, { metaKey: true }),
        keyEvent('ArrowUp', day, { altKey: true }),
        keyEvent('ArrowUp', day, { shiftKey: true }),
        keyEvent('ArrowUp', day, { isComposing: true }),
        keyEvent('a', day),
        keyEvent('ArrowUp', new StubEl({})), // 非分段目标
    ];
    for (const event of cases) root.dispatch('keydown', event);

    assert.equal(day.value, '10');
    assert.ok(cases.every(c => !c.defaultPrevented));

    day.readOnly = true;
    const ro = keyEvent('ArrowUp', day);
    root.dispatch('keydown', ro);
    assert.equal(day.value, '10');
    assert.ok(!ro.defaultPrevented);
});

// ── dispose ───────────────────────────────────────────────────────────────

test('dispose 摘除监听并可再 initialize 重挂', async () => {
    const mod = await import(moduleUrl);
    const { root, inputs } = makeGroup([
        { segment: 'Day', value: '10', attrs: { 'data-min': '1', 'data-max': '31', 'data-digits': '2' } },
    ]);
    mod.initialize(root);
    mod.dispose(root);
    assert.equal(root.count('focusin'), 0);
    assert.equal(root.count('keydown'), 0);

    // 已摘除后按键不再自旋。
    root.dispatch('keydown', keyEvent('ArrowUp', inputs[0]));
    assert.equal(inputs[0].value, '10');

    mod.dispose(root); // WeakMap 已清，二次 dispose 无害
    mod.initialize(root);
    assert.equal(root.count('keydown'), 1);
    root.dispatch('keydown', keyEvent('ArrowUp', inputs[0]));
    assert.equal(inputs[0].value, '11');

    mod.dispose(null); // 未登记的 root（含 null）：WeakMap.get 未命中，静默返回
});
