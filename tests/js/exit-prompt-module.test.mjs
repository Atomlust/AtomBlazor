// exit-prompt.js 的真实文件导入与行为测试。
// 模块顶层只建一个 Map，window.addEventListener 在 arm/disarm 体内才触碰，
// 可在无 DOM 的 Node 环境以 file-URL 导入；模块无相对依赖。
// C# 侧调用点为 barrel 点分标识符 exitPrompt.arm / exitPrompt.disarm
// （命名空间＝camelCase(文件名)，见 PrimitiveJsModuleTests 规则），本文件守护该导出面。
// 模块级 armed Map 跨用例共享（同一 file-URL 的 import 缓存），各用例用专属 id。

import assert from 'node:assert/strict';
import { test, beforeEach, afterEach } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/exit-prompt.js', import.meta.url);

// ---- window 桩：记录 beforeunload 监听器的增删 ----

let listeners; // [{ type, fn }]

function installWindow() {
    listeners = [];
    Object.defineProperty(globalThis, 'window', {
        configurable: true,
        writable: true,
        value: {
            addEventListener(type, fn) { listeners.push({ type, fn }); },
            removeEventListener(type, fn) {
                const at = listeners.findIndex((l) => l.type === type && l.fn === fn);
                if (at >= 0) listeners.splice(at, 1);
            },
        },
    });
}

function fireBeforeUnload() {
    const event = {
        defaultPrevented: false,
        returnValue: undefined,
        preventDefault() { this.defaultPrevented = true; },
    };
    for (const { type, fn } of [...listeners]) {
        if (type === 'beforeunload') fn(event);
    }
    return event;
}

beforeEach(installWindow);
afterEach(() => { delete globalThis.window; });

// ------------------------------------------------------------------

test('exit-prompt.js 可导入且导出面与 exitPrompt. 点分标识符一一对应', async () => {
    const mod = await import(moduleUrl.href);

    // C# 侧调用点：exitPrompt.arm / exitPrompt.disarm。
    for (const name of ['arm', 'disarm']) {
        assert.equal(typeof mod[name], 'function', `exit-prompt.js 缺少导出 ${name}`);
    }
});

test('arm 挂 beforeunload 监听，事件被拦且 returnValue 置空串回空串', async () => {
    const { arm } = await import(moduleUrl.href);

    arm('t-arm-basic');
    assert.equal(listeners.length, 1);
    assert.equal(listeners[0].type, 'beforeunload');

    const event = fireBeforeUnload();
    assert.equal(event.defaultPrevented, true);
    assert.equal(event.returnValue, '');
});

test('arm 对同一 id 幂等，重复武装不再多挂监听', async () => {
    const { arm } = await import(moduleUrl.href);

    arm('t-arm-idempotent');
    arm('t-arm-idempotent');
    assert.equal(listeners.length, 1);

    const event = fireBeforeUnload();
    assert.equal(event.defaultPrevented, true); // 单个处理器照常拦下
});

test('disarm 摘除监听后离开不再被拦', async () => {
    const { arm, disarm } = await import(moduleUrl.href);

    arm('t-disarm-pass');
    disarm('t-disarm-pass');
    assert.equal(listeners.length, 0);

    const event = fireBeforeUnload();
    assert.equal(event.defaultPrevented, false);
});

test('disarm 未知 id 静默无操作', async () => {
    const { disarm } = await import(moduleUrl.href);

    disarm('t-disarm-ghost'); // 不抛错即通过
    assert.equal(listeners.length, 0);
});

test('不同 id 各自武装，disarm 只摘自己的监听器', async () => {
    const { arm, disarm } = await import(moduleUrl.href);

    arm('t-multi-a');
    arm('t-multi-b');
    assert.equal(listeners.length, 2);

    disarm('t-multi-a');
    assert.equal(listeners.length, 1);

    const stillArmed = fireBeforeUnload();
    assert.equal(stillArmed.defaultPrevented, true); // b 仍在岗

    disarm('t-multi-b');
    assert.equal(fireBeforeUnload().defaultPrevented, false);
});
