// resizable.js 的真实文件导入与行为测试。
// 模块顶层零 DOM 触碰（仅模块级 resizableStates 登记簿），document/body/
// requestAnimationFrame/getComputedStyle 均在函数体内引用，可在无 DOM 的 Node
// 环境以 file-URL 导入；模块无相对依赖。
// C# 侧调用点为 barrel 点分标识符 resizable.initializeResizable /
// resizable.startResize / resizable.disposeResizable（ResizablePanelGroup.razor，
// 经 ComponentModules.GetCoreAsync 装载 components-core.js；直连独立路径属
// 已登记的有意偏差）。
// 语义照源锁存：拖拽循环直写 flex-basis、rAF 每帧合并、只在松开时提交一次
// （dirty 才 Commit）；元素解析失败回退按帧 UpdatePanelSizes；可折叠面板低于
// 声明最小值一半归零、之间回弹最小值；rtl 水平组反向增量；清理句柄不点分。
// DOM 桩沿用 sidebar.test.mjs 先例最小口径：document 监听表 + body.style 哨兵，
// 组/面板元素桩带 getAttribute/querySelectorAll/getBoundingClientRect/指针捕获，
// rAF/CAF 手工队列，getComputedStyle 由开关返回 ltr/rtl。
// 共享状态 resizableStates 以 groupId 分键，各测试用独立组 id 保证互不串扰。

import assert from 'node:assert/strict';
import { test } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/resizable.js', import.meta.url);

// ------------------------------------------------------------------ 环境桩

const bodies = [];

function makeDocument() {
    const handlers = new Map();
    const doc = {
        handlers,
        body: { style: {} },
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
    bodies.push(doc);
    return doc;
}

globalThis.document = makeDocument();

// rAF/CAF 手工队列：frameHandle 为自增票据，cancel 置墓碑。
let frameSeq = 0;
const frames = new Map();
globalThis.requestAnimationFrame = (cb) => {
    const handle = ++frameSeq;
    frames.set(handle, cb);
    return handle;
};
globalThis.cancelAnimationFrame = (handle) => { frames.delete(handle); };
function flushFrames() {
    const pending = [...frames.entries()];
    frames.clear();
    for (const [, cb] of pending) cb();
}

// getComputedStyle 桩：方向受全局开关支配。
let computedDirection = 'ltr';
globalThis.getComputedStyle = () => ({ direction: computedDirection });

// 面板元素桩：data 属性 + style 写入面。
function makePanel(groupId, index) {
    return {
        attrs: {
            'data-atom-resizable-group': groupId,
            'data-atom-resizable-panel': String(index),
        },
        style: {},
        getAttribute(name) { return this.attrs[name] ?? null; },
    };
}

// 组元素桩：面板清单、指针捕获记录、包围盒尺寸可调。
function makeGroup(groupId, panels, rect = { width: 1000, height: 1000 }) {
    const group = {
        panels,
        captured: [],
        released: [],
        releaseThrows: false,
        getBoundingClientRect: () => rect,
        querySelectorAll(selector) {
            return selector === '[data-atom-resizable-panel]' ? panels : [];
        },
        getAttribute(name) { return this.attrs?.[name] ?? null; },
        setPointerCapture(pointerId) { group.captured.push(pointerId); },
        releasePointerCapture(pointerId) {
            if (group.releaseThrows) throw new Error('already released');
            group.released.push(pointerId);
        },
    };
    return group;
}

// DotNet 引用桩：记录 invokeMethodAsync 的 (方法名, 载荷)。
function makeRef() {
    const calls = [];
    return {
        calls,
        invokeMethodAsync(name, ...args) {
            calls.push([name, ...args]);
            return Promise.resolve();
        },
    };
}

// 指针事件桩。
function pointer(over = {}) {
    const event = {
        pointerId: 7, clientX: 0, clientY: 0,
        preventDefaultCalls: 0,
        preventDefault() { this.preventDefaultCalls += 1; },
        ...over,
    };
    return event;
}

// console.error/warn 捕获。
function captureConsole() {
    const errors = [];
    const warns = [];
    const originalError = console.error;
    const originalWarn = console.warn;
    console.error = (...args) => errors.push(args.join(' '));
    console.warn = (...args) => warns.push(args.join(' '));
    return {
        errors,
        warns,
        restore() { console.error = originalError; console.warn = originalWarn; },
    };
}

const rz = await import(moduleUrl.href);

let groupCounter = 0;
function nextGroupId() { return `g${++groupCounter}`; }

// 挂载一组双面板并起手拖拽的公共铺排。
function mountAndStart(options = {}) {
    const groupId = nextGroupId();
    const panel0 = makePanel(groupId, 0);
    const panel1 = makePanel(groupId, 1);
    const group = makeGroup(groupId, options.panels ?? [panel0, panel1], options.rect);
    const ref = makeRef();
    rz.initializeResizable(group, ref, groupId, options.isHorizontal ?? true);
    rz.startResize(
        groupId, options.handleIndex ?? 0,
        options.clientX ?? 100, options.clientY ?? 0,
        options.currentSizes ?? [40, 60], options.pointerId ?? 7,
        options.minSizes ?? [20, 20], options.maxSizes ?? [100, 100],
        options.isHorizontal ?? true, options.collapsible ?? [false, false]);
    return { groupId, group, panel0, panel1, ref };
}

// ------------------------------------------------------------------ 导出面

test('导出面恰为 initializeResizable/startResize/disposeResizable 三枚', () => {
    assert.deepEqual(Object.keys(rz).sort(),
        ['disposeResizable', 'initializeResizable', 'startResize']);
    for (const name of Object.keys(rz)) assert.equal(typeof rz[name], 'function');
});

// ------------------------------------------------------------------ 装载与守卫

test('initializeResizable 缺参早退：console.error 且不建状态，随后 startResize 静默', () => {
    const cap = captureConsole();
    try {
        const groupId = nextGroupId();
        rz.initializeResizable(null, makeRef(), groupId, true);
        rz.initializeResizable(makeGroup(groupId, []), null, groupId, true);
        assert.equal(cap.errors.filter((t) => t.includes('missing required parameters')).length, 2);

        rz.startResize(groupId, 0, 10, 10, [50, 50], 7);
        assert.equal(document.count('pointermove'), 0);
        assert.equal(document.body.style.userSelect, undefined);

 // 未登记的组直接 startResize 同样早退。
        rz.startResize(nextGroupId(), 0, 10, 10, [50, 50], 7);
        assert.equal(document.count('pointermove'), 0);
    } finally { cap.restore(); }
});

// ------------------------------------------------------------------ 正常拖拽链

test('拖拽主链：rAF 每帧合并、flex-basis 直写、松开单次 Commit 并全量清理', () => {
    const { groupId, group, panel0, panel1, ref } = mountAndStart();
    assert.deepEqual(group.captured, [7]);
    assert.equal(document.body.style.userSelect, 'none');
    assert.equal(document.body.style.cursor, 'col-resize');

    // 同帧两次 move 只排一个 rAF（合并语义）。
    document.dispatch('pointermove', pointer({ clientX: 120 }));
    document.dispatch('pointermove', pointer({ clientX: 150 }));
    assert.equal(frames.size, 1);
    flushFrames();
    assert.equal(panel0.style.flexBasis, '45%');
    assert.equal(panel1.style.flexBasis, '55%');
    assert.equal(ref.calls.length, 0); // 拖拽期间零 interop

    document.dispatch('pointerup', pointer());
    assert.deepEqual(ref.calls.at(-1), ['CommitPanelSizes', [45, 55]]);
    assert.equal(ref.calls.filter((c) => c[0] === 'CommitPanelSizes').length, 1);
    assert.deepEqual(group.released, [7]);
    assert.equal(document.count('pointermove'), 0);
    assert.equal(document.count('pointerup'), 0);
    assert.equal(document.count('pointercancel'), 0);
    assert.equal(document.body.style.userSelect, '');
    assert.equal(document.body.style.cursor, '');

    rz.disposeResizable(groupId); // 复位登记簿
});

test('越界拖拽钳到限位钉住而非冻结：上界 80/20 下界 20/80', () => {
    const { groupId, panel0, panel1, ref } = mountAndStart();

    document.dispatch('pointermove', pointer({ clientX: 600 })); // +50% 越上界
    flushFrames();
    assert.equal(panel0.style.flexBasis, '80%'); // 上界 = min(100-40, 60-20) = +40
    assert.equal(panel1.style.flexBasis, '20%');

    document.dispatch('pointermove', pointer({ clientX: -400 })); // -50% 越下界
    flushFrames();
    assert.equal(panel0.style.flexBasis, '20%'); // 下界 = max(20-40, 60-100) = -20（startSizes 全程不变）
    assert.equal(panel1.style.flexBasis, '80%');

    document.dispatch('pointerup', pointer());
    assert.deepEqual(ref.calls.at(-1), ['CommitPanelSizes', [20, 80]]);
    rz.disposeResizable(groupId);
});

test('次级触摸忽略：拖拽中再 startResize 不重复挂监听，异指针 move 不动作', () => {
    const { groupId, panel0, ref } = mountAndStart();
    rz.startResize(groupId, 1, 500, 0, [40, 60], 8); // isDragging 早退

    assert.equal(document.count('pointermove'), 1);

    document.dispatch('pointermove', pointer({ pointerId: 99, clientX: 300 }));
    assert.equal(frames.size, 0); // 指针不符：不入队也不 preventDefault
    flushFrames();
    assert.equal(panel0.style.flexBasis, undefined);

    // 异指针 pointerup 不结算。
    document.dispatch('pointerup', pointer({ pointerId: 99 }));
    assert.equal(ref.calls.length, 0);
    assert.equal(document.count('pointermove'), 1);

    document.dispatch('pointermove', pointer({ clientX: 110 }));
    flushFrames();
    assert.equal(panel0.style.flexBasis, '41%');

    document.dispatch('pointerup', pointer());
    assert.equal(ref.calls.at(-1)[0], 'CommitPanelSizes');
    rz.disposeResizable(groupId);
});

test('按住不动再松开不算调整尺寸：无提交、样式照常清理', () => {
    const { groupId, group, ref } = mountAndStart();
    document.dispatch('pointerup', pointer());
    assert.equal(ref.calls.length, 0);
    assert.deepEqual(group.released, [7]);
    assert.equal(document.body.style.userSelect, '');
    assert.equal(document.count('pointerup'), 0);
    rz.disposeResizable(groupId);
});

test('pointercancel 与 pointerup 同结算面：脏则提交一次', () => {
    const { groupId, ref } = mountAndStart();
    document.dispatch('pointermove', pointer({ clientX: 120 }));
    flushFrames();
    document.dispatch('pointercancel', pointer());
    assert.deepEqual(ref.calls.at(-1), ['CommitPanelSizes', [42, 58]]);
    assert.equal(document.count('pointermove'), 0);
    rz.disposeResizable(groupId);
});

// ------------------------------------------------------------------ 折叠吸附

test('可折叠面板吸附：低于声明最小值一半归零，之间回弹最小值', () => {
    const { groupId, panel0, panel1, ref } = mountAndStart({
        currentSizes: [40, 60], collapsible: [true, false], minSizes: [20, 20],
    });

    // -14% → 26：仍在 min 之上，普通收缩。
    document.dispatch('pointermove', pointer({ clientX: -40 }));
    flushFrames();
    assert.equal(panel0.style.flexBasis, '26%');

    // -26% → 14：落在 [10,20) 区间，回弹到声明最小值 20。
    document.dispatch('pointermove', pointer({ clientX: -160 }));
    flushFrames();
    assert.equal(panel0.style.flexBasis, '20%');

    // -36% → 4：低于 min/2=10，折叠归零，邻板吃满 100。
    document.dispatch('pointermove', pointer({ clientX: -260 }));
    flushFrames();
    assert.equal(panel0.style.flexBasis, '0%');
    assert.equal(panel1.style.flexBasis, '100%');

    document.dispatch('pointerup', pointer());
    assert.deepEqual(ref.calls.at(-1), ['CommitPanelSizes', [0, 100]]);
    rz.disposeResizable(groupId);
});

// ------------------------------------------------------------------ RTL

test('rtl 水平组反向增量：向左拖反而增大首板', () => {
    computedDirection = 'rtl';
    try {
        const { groupId, panel0, panel1 } = mountAndStart();
        document.dispatch('pointermove', pointer({ clientX: 50 })); // -50px → rtl 取反 = +5%
        flushFrames();
        assert.equal(panel0.style.flexBasis, '45%');
        assert.equal(panel1.style.flexBasis, '55%');
        rz.disposeResizable(groupId);
    } finally { computedDirection = 'ltr'; }
});

// ------------------------------------------------------------------ 回退路径

test('面板元素解析失败：console.warn 后逐帧 UpdatePanelSizes，松开仍补 Commit 一次', () => {
    const cap = captureConsole();
    try {
        const groupId = nextGroupId();
        const group = makeGroup(groupId, []); // 无可解析面板
        const ref = makeRef();
        rz.initializeResizable(group, ref, groupId, true);
        rz.startResize(groupId, 0, 100, 0, [40, 60], 7, [20, 20], [100, 100], true, [false, false]);
        assert.equal(cap.warns.filter((t) => t.includes('Falling back to per-frame interop')).length, 1);

        document.dispatch('pointermove', pointer({ clientX: 150 }));
        flushFrames();
        assert.deepEqual(ref.calls.at(-1), ['UpdatePanelSizes', [45, 55]]);

        document.dispatch('pointerup', pointer());
        assert.equal(ref.calls.filter((c) => c[0] === 'UpdatePanelSizes').length, 1);
        assert.equal(ref.calls.filter((c) => c[0] === 'CommitPanelSizes').length, 1);
        rz.disposeResizable(groupId);
    } finally { cap.restore(); }
});

// ------------------------------------------------------------------ 嵌套隔离

test('嵌套组面板不拾取：仅同 data-atom-resizable-group 的面板可写', () => {
    const groupId = nextGroupId();
    const nestedGroupId = nextGroupId();
    const own0 = makePanel(groupId, 0);
    const own1 = makePanel(groupId, 1);
    const foreign = makePanel(nestedGroupId, 2); // 混入后代：组 id 不符必须跳过
    const panel2 = makePanel(groupId, 2);

    const group = makeGroup(groupId, [own0, own1, foreign, panel2]);
    const ref = makeRef();
    rz.initializeResizable(group, ref, groupId, true);
    rz.startResize(groupId, 0, 100, 0, [40, 60], 7, [20, 20], [100, 100], true, [false, false]);

    document.dispatch('pointermove', pointer({ clientX: 150 }));
    flushFrames();
    assert.equal(own0.style.flexBasis, '45%');
    assert.equal(own1.style.flexBasis, '55%');
    assert.equal(foreign.style.flexBasis, undefined);
    assert.equal(panel2.style.flexBasis, undefined);

    document.dispatch('pointerup', pointer());
    rz.disposeResizable(groupId);
});

// ------------------------------------------------------------------ dispose

test('disposeResizable 拖拽中途释放：清 body 残留、摘监听、撤队帧；未知 id 静默', () => {
    const { groupId, group, ref } = mountAndStart();
    document.dispatch('pointermove', pointer({ clientX: 150 })); // 帧已入队未执行

    rz.disposeResizable(groupId);
    assert.equal(frames.size, 0); // 撤队：拖拽残帧不再落笔
    assert.equal(document.body.style.userSelect, '');
    assert.equal(document.body.style.cursor, '');
    assert.equal(document.count('pointermove'), 0);

    // 释放后指针再松开：状态已删，不得再有任何 interop。
    document.dispatch('pointerup', pointer());
    assert.equal(ref.calls.length, 0);

    // 未知 id：no-op 不抛。
    rz.disposeResizable('no-such-group');
    rz.disposeResizable(groupId); // 双释同 id 也静默

    // releasePointerCapture 抛错被吞：坏捕获不拉崩结算。
    const second = mountAndStart();
    second.group.releaseThrows = true;
    document.dispatch('pointermove', pointer({ clientX: 110 }));
    flushFrames();
    document.dispatch('pointerup', pointer());
    assert.deepEqual(second.ref.calls.at(-1), ['CommitPanelSizes', [41, 59]]);
    rz.disposeResizable(second.groupId);
});

// ------------------------------------------------------------------ 垂直朝向

test('垂直组：起始位置取 clientY，光标为 row-resize，包围盒取高', () => {
    const { groupId, panel0, panel1, ref } = mountAndStart({
        isHorizontal: false, clientY: 100, rect: { width: 1000, height: 500 },
    });
    assert.equal(document.body.style.cursor, 'row-resize');

    document.dispatch('pointermove', pointer({ pointerId: 7, clientX: 0, clientY: 150 }));
    flushFrames();
    assert.equal(panel0.style.flexBasis, '50%'); // +50px/500px = +10%
    assert.equal(panel1.style.flexBasis, '50%');

    document.dispatch('pointerup', pointer());
    assert.deepEqual(ref.calls.at(-1), ['CommitPanelSizes', [50, 50]]);
    rz.disposeResizable(groupId);
});

// ------------------------------------------------------------------ 桶接线

test('components-core.js barrel 已收口 resizable 命名空间', async () => {
    const { readFile } = await import('node:fs/promises');
    const core = new URL('../../src/AtomBlazor.Components/wwwroot/js/components-core.js', import.meta.url);
    const text = await readFile(core, 'utf8');
    assert.match(text, /^import \* as resizable from '\.\/resizable\.js\?assets=1';$/m);
    assert.match(text, /^assertFresh\('resizable\.js', resizable, 'initializeResizable'\);$/m);
    assert.match(text, /^export \{[\s\S]*\bresizable,[\s\S]*\};$/m);
    assert.match(text, /（resizable）与 （dock）已交付的模块。/);
});
