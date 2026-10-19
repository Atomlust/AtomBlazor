// file-upload.js 的真实文件导入与行为测试。
// 模块零 import 依赖、顶层零 DOM 触碰，document/DataTransfer 仅在处理器运行时触碰，
// 因此可在无 DOM 的 Node 环境以 file-URL 导入并以最小桩覆盖三导出。
// C# 侧调用点为 barrel 点分标识符 fileUpload.resetInput/initializeDropZone/initializePaste
// （见 components-core.js 与 PrimitiveJsModuleTests 的命名空间 camelCase 规则），
// 本文件同时守护该导出面；initializeDropZone/initializePaste 返回的清理句柄上
// dispose 为对象方法（非模块导出），C# 调用点保持原样不改。

import assert from 'node:assert/strict';
import { test, afterEach } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/file-upload.js', import.meta.url);

const originalDocument = globalThis.document;

function makeElement(extra = {}) {
    const handlers = new Map();
    const el = {
        handlers,
        value: 'stale',
        disabled: false,
        multiple: true,
        files: null,
        dispatched: [],
        addEventListener(type, fn, options) {
            const key = `${type}:${options?.capture ? 'capture' : 'bubble'}`;
            if (!handlers.has(key)) handlers.set(key, []);
            handlers.get(key).push(fn);
        },
        removeEventListener(type, fn, options) {
            const key = `${type}:${options?.capture ? 'capture' : 'bubble'}`;
            const list = handlers.get(key) ?? [];
            const index = list.indexOf(fn);
            if (index !== -1) list.splice(index, 1);
        },
        dispatchEvent(event) {
            el.dispatched.push(event);
        },
        dispatch(type, event = {}, options = {}) {
            const key = `${type}:${options.capture ? 'capture' : 'bubble'}`;
            for (const fn of [...(handlers.get(key) ?? [])]) fn(event);
            // 近似冒泡语义：dragover/drop/change 在桩上直接按类型派发即可
        },
        count(type) {
            let total = 0;
            for (const [key, list] of handlers) {
                if (key.startsWith(`${type}:`)) total += list.length;
            }
            return total;
        },
        contains: () => false,
        ...extra,
    };
    return el;
}

function makeDocument() {
    const handlers = new Map();
    return {
        activeElement: null,
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
}

function makePointerEvent() {
    return {
        prevented: false,
        stopped: false,
        preventDefault() { this.prevented = true; },
        stopPropagation() { this.stopped = true; },
    };
}

// DataTransfer 桩：items.add 收集文件，files 读出为数组快照。
function installDataTransfer() {
    const original = globalThis.DataTransfer;
    globalThis.DataTransfer = class {
        constructor() {
            this.collected = [];
            this.items = { add: (file) => this.collected.push(file) };
        }
        get files() { return [...this.collected]; }
    };
    return () => {
        if (original === undefined) delete globalThis.DataTransfer;
        else globalThis.DataTransfer = original;
    };
}

function eventCount(el, type, capture) {
    const key = `${type}:${capture ? 'capture' : 'bubble'}`;
    return (el.handlers.get(key) ?? []).length;
}

test('导出面：resetInput/initializeDropZone/initializePaste 三函数', async () => {
    const module = await import(moduleUrl);
    assert.equal(typeof module.resetInput, 'function');
    assert.equal(typeof module.initializeDropZone, 'function');
    assert.equal(typeof module.initializePaste, 'function');
});

test('resetInput 清空 value，空入参安全无操作', async () => {
    const { resetInput } = await import(moduleUrl);
    const el = makeElement({ value: 'C:\\fakepath\\report.pdf' });
    resetInput(el);
    assert.equal(el.value, '');
    assert.doesNotThrow(() => resetInput(null));
    assert.doesNotThrow(() => resetInput(undefined));
});

test('initializeDropZone：挂载 dragover/drop 与 change 捕获锁，drop 转移文件并派发 change', async () => {
    const { initializeDropZone } = await import(moduleUrl);
    const dropZone = makeElement();
    const input = makeElement({ disabled: false });
    const cleanup = initializeDropZone(dropZone, input);
    assert.equal(eventCount(dropZone, 'dragover', false), 1);
    assert.equal(eventCount(dropZone, 'drop', false), 1);
    assert.equal(eventCount(input, 'change', true), 1);

    const fileA = { name: 'a.png' };
    const dropEvent = Object.assign(makePointerEvent(), {
        dataTransfer: { files: [fileA] },
    });
    dropZone.dispatch('drop', dropEvent);
    assert.equal(dropEvent.prevented, true);
    assert.equal(dropEvent.stopped, true);
    assert.deepEqual(input.files, [fileA]);
    assert.equal(input.dispatched.length, 1);
    assert.equal(input.dispatched[0].type, 'change');
    assert.equal(input.dispatched[0].bubbles, true);
    cleanup.dispose();
});

test('initializeDropZone：dragover 只 preventDefault/stopPropagation，不转移文件', async () => {
    const { initializeDropZone } = await import(moduleUrl);
    const dropZone = makeElement();
    const input = makeElement();
    const cleanup = initializeDropZone(dropZone, input);
    const over = makePointerEvent();
    dropZone.dispatch('dragover', over);
    assert.equal(over.prevented, true);
    assert.equal(over.stopped, true);
    assert.equal(input.files, null);
    assert.equal(input.dispatched.length, 0);
    cleanup.dispose();
});

test('initializeDropZone：禁用输入或空 dataTransfer 的 drop 放行不转移', async () => {
    const { initializeDropZone } = await import(moduleUrl);
    const dropZone = makeElement();
    const input = makeElement({ disabled: true });
    const cleanup = initializeDropZone(dropZone, input);
    const disabledDrop = Object.assign(makePointerEvent(), { dataTransfer: { files: [{ name: 'a' }] } });
    dropZone.dispatch('drop', disabledDrop);
    assert.equal(input.files, null);
    assert.equal(input.dispatched.length, 0);

    const emptyDrop = Object.assign(makePointerEvent(), { dataTransfer: { files: [] } });
    dropZone.dispatch('drop', emptyDrop);
    assert.equal(input.dispatched.length, 0);

    const nullTransfer = makePointerEvent();
    dropZone.dispatch('drop', nullTransfer);
    assert.equal(input.dispatched.length, 0);
    assert.equal(nullTransfer.prevented, true); // preventDefault 仍在转移判定之前
    cleanup.dispose();
});

test('initializeDropZone：change 捕获锁把输入置为禁用（防同 InputFile 二次选择）', async () => {
    const { initializeDropZone } = await import(moduleUrl);
    const dropZone = makeElement();
    const input = makeElement({ disabled: false });
    const cleanup = initializeDropZone(dropZone, input);
    assert.equal(input.disabled, false);
    input.dispatch('change', {}, { capture: true });
    assert.equal(input.disabled, true);
    cleanup.dispose();
});

test('initializeDropZone：dispose 摘除三监听且二次调用幂等', async () => {
    const { initializeDropZone } = await import(moduleUrl);
    const dropZone = makeElement();
    const input = makeElement();
    const cleanup = initializeDropZone(dropZone, input);
    cleanup.dispose();
    assert.equal(eventCount(dropZone, 'dragover', false), 0);
    assert.equal(eventCount(dropZone, 'drop', false), 0);
    assert.equal(eventCount(input, 'change', true), 0);
    assert.doesNotThrow(() => cleanup.dispose());
});

test('initializeDropZone：缺参记警并返回空操作句柄', async () => {
    const { initializeDropZone } = await import(moduleUrl);
    const warnings = [];
    const originalWarn = console.warn;
    console.warn = (message) => warnings.push(message);
    try {
        const cleanup = initializeDropZone(null, makeElement());
        assert.deepEqual(warnings, ['FileUpload: Missing dropZone or inputFile element']);
        assert.doesNotThrow(() => cleanup.dispose());
        const cleanup2 = initializeDropZone(makeElement(), null);
        assert.equal(warnings.length, 2);
        assert.equal(typeof cleanup2.dispose, 'function');
    } finally {
        console.warn = originalWarn;
    }
});

test('initializePaste：document 挂 paste，焦点在区内时转移文件并派发 change', async () => {
    const { initializePaste } = await import(moduleUrl);
    const restoreDataTransfer = installDataTransfer();
    const documentStub = makeDocument();
    globalThis.document = documentStub;
    try {
        const active = {};
        documentStub.activeElement = active;
        const dropZone = makeElement({ contains: (node) => node === active });
        const input = makeElement({ disabled: false, multiple: true });
        const cleanup = initializePaste(dropZone, input);
        assert.equal(documentStub.count('paste'), 1);

        const fileA = { name: 'a.png' };
        const fileB = { name: 'b.png' };
        const paste = Object.assign(makePointerEvent(), { clipboardData: { files: [fileA, fileB] } });
        documentStub.dispatch('paste', paste);
        assert.equal(paste.prevented, true);
        assert.deepEqual(input.files, [fileA, fileB]);
        assert.equal(input.dispatched.length, 1);
        assert.equal(input.dispatched[0].type, 'change');
        cleanup.dispose();
    } finally {
        restoreDataTransfer();
        if (originalDocument === undefined) delete globalThis.document;
        else globalThis.document = originalDocument;
    }
});

test('initializePaste：multiple=false 只交接首个文件', async () => {
    const { initializePaste } = await import(moduleUrl);
    const restoreDataTransfer = installDataTransfer();
    const documentStub = makeDocument();
    globalThis.document = documentStub;
    try {
        const active = {};
        documentStub.activeElement = active;
        const dropZone = makeElement({ contains: (node) => node === active });
        const input = makeElement({ disabled: false, multiple: false });
        const cleanup = initializePaste(dropZone, input);
        const fileA = { name: 'a.png' };
        const fileB = { name: 'b.png' };
        documentStub.dispatch('paste', Object.assign(makePointerEvent(), { clipboardData: { files: [fileA, fileB] } }));
        assert.deepEqual(input.files, [fileA]);
        cleanup.dispose();
    } finally {
        restoreDataTransfer();
        if (originalDocument === undefined) delete globalThis.document;
        else globalThis.document = originalDocument;
    }
});

test('initializePaste：禁用输入/焦点在区外/纯文本粘贴三态放行', async () => {
    const { initializePaste } = await import(moduleUrl);
    const restoreDataTransfer = installDataTransfer();
    const documentStub = makeDocument();
    globalThis.document = documentStub;
    try {
        const active = {};
        documentStub.activeElement = active;
        const dropZone = makeElement({ contains: (node) => node === active });

        const disabledInput = makeElement({ disabled: true });
        const disabledCleanup = initializePaste(dropZone, disabledInput);
        let paste = Object.assign(makePointerEvent(), { clipboardData: { files: [{ name: 'a.png' }] } });
        documentStub.dispatch('paste', paste);
        assert.equal(paste.prevented, false);
        assert.equal(disabledInput.dispatched.length, 0);
        disabledCleanup.dispose();

        const outsideZone = makeElement({ contains: () => false });
        const input = makeElement({ disabled: false });
        const outsideCleanup = initializePaste(outsideZone, input);
        paste = Object.assign(makePointerEvent(), { clipboardData: { files: [{ name: 'a.png' }] } });
        documentStub.dispatch('paste', paste);
        assert.equal(paste.prevented, false);
        assert.equal(input.dispatched.length, 0);
        outsideCleanup.dispose();

        const insideCleanup = initializePaste(dropZone, input);
        paste = Object.assign(makePointerEvent(), { clipboardData: { files: [] } });
        documentStub.dispatch('paste', paste);
        assert.equal(paste.prevented, false);
        paste = makePointerEvent(); // clipboardData 缺失
        documentStub.dispatch('paste', paste);
        assert.equal(paste.prevented, false);
        assert.equal(input.dispatched.length, 0);
        insideCleanup.dispose();
    } finally {
        restoreDataTransfer();
        if (originalDocument === undefined) delete globalThis.document;
        else globalThis.document = originalDocument;
    }
});

test('initializePaste：dispose 摘除 document 监听且二次调用幂等', async () => {
    const { initializePaste } = await import(moduleUrl);
    const documentStub = makeDocument();
    globalThis.document = documentStub;
    try {
        const dropZone = makeElement();
        const input = makeElement();
        const cleanup = initializePaste(dropZone, input);
        assert.equal(documentStub.count('paste'), 1);
        cleanup.dispose();
        assert.equal(documentStub.count('paste'), 0);
        assert.doesNotThrow(() => cleanup.dispose());
    } finally {
        if (originalDocument === undefined) delete globalThis.document;
        else globalThis.document = originalDocument;
    }
});

test('initializePaste：缺参记警并返回空操作句柄', async () => {
    const { initializePaste } = await import(moduleUrl);
    const warnings = [];
    const originalWarn = console.warn;
    console.warn = (message) => warnings.push(message);
    try {
        const cleanup = initializePaste(null, makeElement());
        assert.deepEqual(warnings, ['FileUpload: Missing dropZone or inputFile element for paste']);
        assert.doesNotThrow(() => cleanup.dispose());
    } finally {
        console.warn = originalWarn;
    }
});

afterEach(() => {
    if (originalDocument === undefined) delete globalThis.document;
    else globalThis.document = originalDocument;
});
