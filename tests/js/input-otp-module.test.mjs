// input-otp.js 的真实文件导入与行为测试。
// 模块顶层零 DOM 触碰（Map/正则/纯函数），可在无 DOM 的 Node 环境以 file-URL 导入。
// C# 侧调用点为 barrel 点分标识符 inputOtp.initialize/updateOtpMode/dispose
//（见 components-core.js 与 PrimitiveJsModuleTests 的命名空间 camelCase 规则），
// 本文件同时守护该导出面。
// DOM 交互沿用 numeric-input-module.test.mjs 先例的最小元素桩
//（addEventListener/removeEventListener），dotNetRef 桩记录 invokeMethodAsync 调用。

import assert from 'node:assert/strict';
import { test, afterEach } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/input-otp.js', import.meta.url);

function makeElement() {
    const handlers = new Map();
    return {
        handlers,
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

let nextId = 0;
const ids = [];

function freshId() {
    const id = `t-${nextId++}`;
    ids.push(id);
    return id;
}

// 每个用例结束后清理实例登记表，避免模块级 Map 在用例间泄漏。
afterEach(async () => {
    const mod = await import(moduleUrl.href);
    while (ids.length) mod.dispose(ids.pop());
});

test('input-otp.js 可导入且导出面与 inputOtp. 点分标识符一一对应', async () => {
    const mod = await import(moduleUrl.href);

    // C# 侧调用点：inputOtp.initialize / updateOtpMode / dispose。
    for (const name of ['initialize', 'updateOtpMode', 'dispose']) {
        assert.equal(typeof mod[name], 'function', `input-otp.js 缺少导出 ${name}`);
    }
});

test('initialize 挂载 paste 与 keydown 监听器', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeElement();

    mod.initialize(element, makeDotNet(), freshId(), 'Numbers');

    assert.equal(element.count('paste'), 1);
    assert.equal(element.count('keydown'), 1);
});

test('initialize 对缺失入参静默返回（预渲染/半程释放容错）', async () => {
    const mod = await import(moduleUrl.href);

    mod.initialize(null, null, 'x', 'Numbers');
    mod.initialize(makeElement(), null, 'y', 'Numbers');
});

test('paste 阻止默认动作并把文本转发 OnPasteJs', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeElement();
    const dotNet = makeDotNet();
    mod.initialize(element, dotNet, freshId(), 'Numbers');

    let prevented = 0;
    element.dispatch('paste', {
        preventDefault() { prevented++; },
        clipboardData: { getData: () => '1-2345x6' },
    });
    assert.equal(prevented, 1);
    assert.deepEqual(dotNet.calls.at(-1), ['OnPasteJs', '1-2345x6']);
});

test('paste 事件缺少 clipboardData 时转发空串', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeElement();
    const dotNet = makeDotNet();
    mod.initialize(element, dotNet, freshId(), 'Numbers');

    element.dispatch('paste', { preventDefault() {} });
    assert.deepEqual(dotNet.calls.at(-1), ['OnPasteJs', '']);
});

test('keydown 按模式拦截非法字符、放行合法字符', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeElement();
    mod.initialize(element, makeDotNet(), freshId(), 'Numbers');

    let prevented = 0;
    element.dispatch('keydown', { key: 'a', preventDefault() { prevented++; } });
    assert.equal(prevented, 1); // Numbers 模式拒绝字母

    element.dispatch('keydown', { key: '7', preventDefault() { prevented++; } });
    assert.equal(prevented, 1); // 数字被放行，未被二次 preventDefault
});

test('keydown 对控制键、输入法组合与修饰键组合一律放行', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeElement();
    mod.initialize(element, makeDotNet(), freshId(), 'Numbers');

    let prevented = 0;
    const pass = (extra) => element.dispatch(
        'keydown', { key: 'a', isComposing: false, preventDefault() { prevented++; }, ...extra });

    pass({ key: 'Backspace' }); // 长度非 1：控制键
    pass({ isComposing: true }); // 输入法组合中
    pass({ ctrlKey: true });  Ctrl 快捷键（含粘贴）
    pass({ metaKey: true });  Cmd 快捷键
    assert.equal(prevented, 0);
});

test('updateOtpMode 热切换字符模式且未知实例静默', async () => {
    const mod = await import(moduleUrl.href);
    const id = freshId();
    const element = makeElement();
    mod.initialize(element, makeDotNet(), id, 'Numbers');

    let prevented = 0;
    element.dispatch('keydown', { key: 'a', preventDefault() { prevented++; } });
    assert.equal(prevented, 1);

    mod.updateOtpMode(id, 'Letters');
    element.dispatch('keydown', { key: 'a', preventDefault() { prevented++; } });
    assert.equal(prevented, 1); // 字母此时被放行

    element.dispatch('keydown', { key: '7', preventDefault() { prevented++; } });
    assert.equal(prevented, 2); // 数字反被拦截

    mod.updateOtpMode('nope', 'Letters'); // 未知实例不抛
});

test('LettersAndNumbers 模式放行字母数字、拦截符号', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeElement();
    mod.initialize(element, makeDotNet(), freshId(), 'LettersAndNumbers');

    let prevented = 0;
    const key = (k) => element.dispatch('keydown', { key: k, preventDefault() { prevented++; } });
    key('a');
    key('3');
    assert.equal(prevented, 0);
    key('-');
    assert.equal(prevented, 1);
});

test('dispose 摘除监听器且幂等、未知实例不抛', async () => {
    const mod = await import(moduleUrl.href);
    const id = freshId();
    const element = makeElement();
    const dotNet = makeDotNet();
    mod.initialize(element, dotNet, id, 'Numbers');

    mod.dispose(id);
    assert.equal(element.count('paste'), 0);
    assert.equal(element.count('keydown'), 0);

    // 摘除后事件不再转发。
    element.dispatch('paste', { preventDefault() {}, clipboardData: { getData: () => '9' } });
    assert.equal(dotNet.calls.length, 0);

    mod.dispose(id); // 幂等
    mod.dispose('nope'); // 未知实例
});
