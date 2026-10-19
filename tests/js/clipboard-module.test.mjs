// clipboard.js 的真实文件导入与行为测试。
// 模块顶层零 DOM 触碰（仅常量定义），navigator/ClipboardItem/Blob/window/document
// 皆在函数体内访问，可在无 DOM 的 Node 环境以 file-URL 导入并逐一打桩；模块无相对依赖。
// C# 侧调用点为 barrel 点分标识符 clipboard.copyToClipboard / clipboard.copyFromAsyncSource /
// clipboard.initializeCopy（命名空间＝camelCase(文件名)，见 PrimitiveJsModuleTests 规则），
// 本文件同时守护该导出面。initializeCopy 返回的清理句柄上的 dispose 是对象方法、
// 非模块导出，C# 侧保持裸标识符调用，本文件亦按对象方法核验。

import assert from 'node:assert/strict';
import { test, beforeEach, afterEach } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/clipboard.js', import.meta.url);

// ---- 环境桩：navigator / window / document / ClipboardItem ----

let clipboardCalls;
let writeResults; // { writeText: [...], write: [...] } 每项可为 'ok' 或抛出的 Error
let clipboardItems;
let execCommands;

function setGlobal(name, value) {
    Object.defineProperty(globalThis, name, { value, configurable: true, writable: true });
}

function makeNavigator({ writeTextOutcome = 'ok', writeOutcome = 'ok' } = {}) {
    return {
        clipboard: {
            writeText(text) {
                clipboardCalls.push(['writeText', text]);
                return writeTextOutcome === 'ok'
                    ? Promise.resolve()
                    : Promise.reject(new Error('denied'));
            },
            write(items) {
                clipboardCalls.push(['write', items]);
                // 真实浏览器会等 ClipboardItem 内的 promise：空值在 blob 环节拒绝
                // 会让整个写入一起落空，桩照此传导 promise 的成败。
                const contents = items.flatMap((item) => Object.values(item.items ?? {}));
                return Promise.all(contents).then(
                    () => (writeOutcome === 'ok' ? undefined : Promise.reject(new Error('refused'))));
            },
        },
    };
}

function makeDocument({ execResult = true } = {}) {
    const created = [];
    return {
        created,
        createElement(tag) {
            const el = { tag, value: '', style: {}, select() { this.selected = true; } };
            created.push(el);
            return el;
        },
        body: { appendChild() {}, removeChild() {} },
        execCommand(command) {
            execCommands.push(command);
            if (!execResult) throw new Error('exec failed');
            return execResult;
        },
    };
}

function flushMicrotasks() {
    // 让 initializeCopy 里 .then 链全部走完（含嵌套 promise）。
    return new Promise((resolve) => setImmediate(resolve));
}

beforeEach(() => {
    clipboardCalls = [];
    clipboardItems = [];
    execCommands = [];

    class ClipboardItemStub {
        constructor(items) { this.items = items; clipboardItems.push(this); }
    }
    setGlobal('ClipboardItem', ClipboardItemStub);
    setGlobal('window', { isSecureContext: true });
    setGlobal('document', makeDocument());
    setGlobal('navigator', makeNavigator());
});

afterEach(() => {
    delete globalThis.ClipboardItem;
    delete globalThis.window;
    delete globalThis.document;
    delete globalThis.navigator;
});

// ------------------------------------------------------------------
// 导出面
// ------------------------------------------------------------------

test('clipboard.js 可导入且导出面与 clipboard. 点分标识符一一对应', async () => {
    const mod = await import(moduleUrl.href);

    // C# 侧调用点：clipboard.copyToClipboard / .copyFromAsyncSource / .initializeCopy。
    for (const name of ['copyToClipboard', 'copyFromAsyncSource', 'initializeCopy']) {
        assert.equal(typeof mod[name], 'function', `clipboard.js 缺少导出 ${name}`);
    }
});

// ------------------------------------------------------------------
// copyToClipboard
// ------------------------------------------------------------------

test('copyToClipboard 非字符串或空串回 noValue 且不触写入', async () => {
    const { copyToClipboard } = await import(moduleUrl.href);

    assert.equal(await copyToClipboard(''), 'noValue');
    assert.equal(await copyToClipboard(null), 'noValue');
    assert.equal(await copyToClipboard(42), 'noValue');
    assert.deepEqual(clipboardCalls, []);
});

test('copyToClipboard 写入成功回 ok 并原样递交文本', async () => {
    const { copyToClipboard } = await import(moduleUrl.href);

    assert.equal(await copyToClipboard('hello'), 'ok');
    assert.deepEqual(clipboardCalls, [['writeText', 'hello']]);
});

test('copyToClipboard 安全上下文被拒回 refused 且不落 execCommand 兜底', async () => {
    const { copyToClipboard } = await import(moduleUrl.href);
    setGlobal('navigator', makeNavigator({ writeTextOutcome: 'fail' }));

    assert.equal(await copyToClipboard('secret'), 'refused');
    assert.deepEqual(execCommands, []); // #466 的教训：execCommand 不是所有失败的兜底
});

test('copyToClipboard 非安全上下文走 execCommand 兜底，成败各映射 ok 与 refused', async () => {
    const { copyToClipboard } = await import(moduleUrl.href);
    setGlobal('navigator', { clipboard: undefined }); // http 页面：Clipboard API 根本不存在
    setGlobal('window', { isSecureContext: false });

    setGlobal('document', makeDocument({ execResult: true }));
    assert.equal(await copyToClipboard('plain'), 'ok');
    assert.deepEqual(execCommands, ['copy']);

    setGlobal('document', makeDocument({ execResult: false }));
    assert.equal(await copyToClipboard('plain'), 'refused');
});

// ------------------------------------------------------------------
// copyFromAsyncSource
// ------------------------------------------------------------------

function makeDotNetRef(result, { fail = false } = {}) {
    return {
        calls: [],
        invokeMethodAsync(methodName, ...args) {
            this.calls.push([methodName, ...args]);
            return fail ? Promise.reject(new Error('circuit down')) : Promise.resolve(result);
        },
    };
}

test('copyFromAsyncSource 缺引用或方法名回 noValue', async () => {
    const { copyFromAsyncSource } = await import(moduleUrl.href);

    assert.equal(await copyFromAsyncSource(null, 'm'), 'noValue');
    assert.equal(await copyFromAsyncSource(makeDotNetRef('x'), ''), 'noValue');
});

test('copyFromAsyncSource promise 感知浏览器把 promise 直接放进 ClipboardItem 且只问 .NET 一次', async () => {
    const { copyFromAsyncSource } = await import(moduleUrl.href);
    const ref = makeDotNetRef('late-text');

    assert.equal(await copyFromAsyncSource(ref, 'ResolveAsyncValue'), 'ok');
    assert.deepEqual(ref.calls, [['ResolveAsyncValue']]); // 不预 await：只回调一次
    assert.equal(clipboardItems.length, 1);
    const items = clipboardItems[0].items['text/plain'];
    assert.equal(typeof items.then, 'function'); // ClipboardItem 收到的就是 promise 本身
    assert.ok((await items) instanceof Blob); // promise 落定为文本构成的 blob
    assert.equal(clipboardCalls[0][0], 'write');
});

test('copyFromAsyncSource 空串在 blob 环节拒绝写入而非放上空剪贴板', async () => {
    const { copyFromAsyncSource } = await import(moduleUrl.href);
    const ref = makeDotNetRef('');

    assert.equal(await copyFromAsyncSource(ref, 'ResolveAsyncValue'), 'noValue');
    assert.equal(clipboardCalls[0][0], 'write'); // write 抛出（blob 先拒）
    const texts = clipboardCalls.filter(([m]) => m === 'writeText');
    assert.deepEqual(texts, []); // 兜底也不会把空串写进 writeText
});

test('copyFromAsyncSource .NET 回调抛错回 noValue', async () => {
    const { copyFromAsyncSource } = await import(moduleUrl.href);
    const ref = makeDotNetRef(null, { fail: true });

    assert.equal(await copyFromAsyncSource(ref, 'ResolveAsyncValue'), 'noValue');
});

test('copyFromAsyncSource 无 promise 感知时先解析再写，成功回 ok', async () => {
    const { copyFromAsyncSource } = await import(moduleUrl.href);
    delete globalThis.ClipboardItem; // 浏览器不暴露 ClipboardItem
    const ref = makeDotNetRef('sync-text');

    assert.equal(await copyFromAsyncSource(ref, 'ResolveAsyncValue'), 'ok');
    assert.deepEqual(ref.calls, [['ResolveAsyncValue']]);
    assert.deepEqual(clipboardCalls, [['writeText', 'sync-text']]);
});

test('copyFromAsyncSource 暴露但拒 promise 的浏览器（Safari 实测面）回退后写成功', async () => {
    const { copyFromAsyncSource } = await import(moduleUrl.href);
    setGlobal('navigator', makeNavigator({ writeOutcome: 'fail', writeTextOutcome: 'ok' }));
    const ref = makeDotNetRef('safari-text');

    assert.equal(await copyFromAsyncSource(ref, 'ResolveAsyncValue'), 'ok');
    assert.deepEqual(clipboardCalls.map(([m]) => m), ['write', 'writeText']);
});

test('copyFromAsyncSource 回退路径二次写入仍被拒时回 refused', async () => {
    const { copyFromAsyncSource } = await import(moduleUrl.href);
    setGlobal('navigator', makeNavigator({ writeOutcome: 'fail', writeTextOutcome: 'fail' }));
    const ref = makeDotNetRef('doomed');

    assert.equal(await copyFromAsyncSource(ref, 'ResolveAsyncValue'), 'refused');
});

// ------------------------------------------------------------------
// initializeCopy（手势内写入 + 清理句柄 + HandleCopyOutcome/ResolveCopyValue 回call）
// ------------------------------------------------------------------

function makeElement() {
    return {
        handlers: {},
        addEventListener(type, fn) { (this.handlers[type] ??= []).push(fn); },
        removeEventListener(type, fn) {
            this.handlers[type] = (this.handlers[type] ?? []).filter((h) => h !== fn);
        },
        fire(type, event = {}) {
            const evt = { defaultPrevented: false, preventDefault() { this.defaultPrevented = true; }, ...event };
            for (const fn of this.handlers[type] ?? []) fn(evt);
            return evt;
        },
    };
}

test('initializeCopy 缺参回空清理句柄且不触 DOM', async () => {
    const { initializeCopy } = await import(moduleUrl.href);

    const ghost = initializeCopy(null, makeDotNetRef('x'));
    ghost.dispose(); // 不抛错即通过
    const orphan = initializeCopy(makeElement(), null);
    orphan.dispose();
    assert.deepEqual(clipboardCalls, []);
});

test('initializeCopy 挂 click/keydown 双监听，dispose 逐一摘除', async () => {
    const { initializeCopy } = await import(moduleUrl.href);
    const el = makeElement();

    const handle = initializeCopy(el, makeDotNetRef('x'));
    assert.deepEqual(Object.keys(el.handlers).sort(), ['click', 'keydown']);

    handle.dispose();
    assert.deepEqual(el.handlers.click, []);
    assert.deepEqual(el.handlers.keydown, []);
});

test('initializeCopy 点击在手势内写 ClipboardItem 并把 ok 回报 HandleCopyOutcome', async () => {
    const { initializeCopy } = await import(moduleUrl.href);
    const el = makeElement();
    const ref = makeDotNetRef('copied-text');

    initializeCopy(el, ref);
    el.fire('click');
    await flushMicrotasks();

    assert.deepEqual(ref.calls, [['ResolveCopyValue'], ['HandleCopyOutcome', 'ok']]);
    assert.equal(clipboardCalls[0][0], 'write');
});

test('initializeCopy 无 promise 感知路径经 ResolveCopyValue 解析后走 copyToClipboard 回报', async () => {
    const { initializeCopy } = await import(moduleUrl.href);
    delete globalThis.ClipboardItem;
    const el = makeElement();
    const ref = makeDotNetRef('legacy-text');

    initializeCopy(el, ref);
    el.fire('click');
    await flushMicrotasks();

    assert.deepEqual(ref.calls, [['ResolveCopyValue'], ['HandleCopyOutcome', 'ok']]);
    assert.deepEqual(clipboardCalls, [['writeText', 'legacy-text']]);
});

test('initializeCopy 空值回报 noValue 而非把空串写上剪贴板', async () => {
    const { initializeCopy } = await import(moduleUrl.href);
    const el = makeElement();
    const ref = makeDotNetRef('');

    initializeCopy(el, ref);
    el.fire('click');
    await flushMicrotasks();

    assert.deepEqual(ref.calls, [['ResolveCopyValue'], ['HandleCopyOutcome', 'noValue']]);
});

test('initializeCopy 写入被拒而值有效时回报 refused（与无值相区分、不问 .NET 第二次）', async () => {
    const { initializeCopy } = await import(moduleUrl.href);
    setGlobal('navigator', makeNavigator({ writeOutcome: 'fail' }));
    const el = makeElement();
    const ref = makeDotNetRef('real-value');

    initializeCopy(el, ref);
    el.fire('click');
    await flushMicrotasks();

    assert.deepEqual(ref.calls, [['ResolveCopyValue'], ['HandleCopyOutcome', 'refused']]);
});

test('initializeCopy ResolveCopyValue 回调抛错回报 noValue', async () => {
    const { initializeCopy } = await import(moduleUrl.href);
    const el = makeElement();
    const ref = makeDotNetRef(null, { fail: true });

    initializeCopy(el, ref);
    el.fire('click');
    await flushMicrotasks();

    assert.deepEqual(ref.calls.at(-1), ['HandleCopyOutcome', 'noValue']);
});

test('initializeCopy Enter 与空格触发复制并 preventDefault，其余按键忽略', async () => {
    const { initializeCopy } = await import(moduleUrl.href);
    const el = makeElement();
    const ref = makeDotNetRef('kbd-text');

    const handle = initializeCopy(el, ref);

    const tab = el.fire('keydown', { key: 'Tab' });
    await flushMicrotasks();
    assert.equal(tab.defaultPrevented, false);
    assert.equal(ref.calls.length, 0); // 非触发键不动 .NET

    const enter = el.fire('keydown', { key: 'Enter' });
    await flushMicrotasks();
    assert.equal(enter.defaultPrevented, true);
    assert.deepEqual(ref.calls.at(-1), ['HandleCopyOutcome', 'ok']);

    const space = el.fire('keydown', { key: ' ' });
    await flushMicrotasks();
    assert.equal(space.defaultPrevented, true);

    // dispose 后按键再无动作
    handle.dispose();
    const before = ref.calls.length;
    el.fire('keydown', { key: 'Enter' });
    await flushMicrotasks();
    assert.equal(ref.calls.length, before);
});
