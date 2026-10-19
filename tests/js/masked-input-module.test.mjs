// masked-input.js 的真实文件导入与行为测试。
// 模块顶层零 DOM 触碰（纯函数导出），可在无 DOM 的 Node 环境以 file-URL 导入。
// C# 侧调用点为 barrel 点分标识符 maskedInput.setInputValue（见 components-core.js
// 与 PrimitiveJsModuleTests 的命名空间 camelCase 规则），本文件同时守护该导出面。
// DOM 交互沿用 numeric-input-module.test.mjs 先例的最小元素桩
// （value/selectionStart/setSelectionRange）。

import assert from 'node:assert/strict';
import { test } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/masked-input.js', import.meta.url);

function makeElement(value = '') {
    return {
        value,
        selectionStart: 0,
        selectionEnd: 0,
        setSelectionRange(start, end) {
            this.selectionStart = start;
            this.selectionEnd = end;
        },
    };
}

test('masked-input.js 可导入且导出面与 maskedInput. 点分标识符一一对应', async () => {
    const mod = await import(moduleUrl.href);

    // C# 侧调用点：maskedInput.setInputValue；其余导出守护 1:1 面。
    for (const name of ['setInputValue', 'setCursorPosition', 'getCursorPosition']) {
        assert.equal(typeof mod[name], 'function', `masked-input.js 缺少导出 ${name}`);
    }
});

test('setInputValue 写入值并把光标夹在界内', async () => {
    const mod = await import(moduleUrl.href);
    const element = makeElement();

    mod.setInputValue(element, '123-___', 4);

    assert.equal(element.value, '123-___');
    assert.equal(element.selectionStart, 4);
    assert.equal(element.selectionEnd, 4);
});

test('setInputValue 越界或负光标不移动选区', async () => {
    const mod = await import(moduleUrl.href);

    const far = makeElement();
    mod.setInputValue(far, 'abc', 99);
    assert.equal(far.selectionStart, 0);

    const negative = makeElement();
    mod.setInputValue(negative, 'abc', -1);
    assert.equal(negative.selectionStart, 0);
});

test('setInputValue 对缺失元素静默返回（预渲染/半程释放容错）', async () => {
    const mod = await import(moduleUrl.href);
    mod.setInputValue(null, 'x', 0);
});

test('setCursorPosition 依据元素当前值夹光标', async () => {
    const mod = await import(moduleUrl.href);

    const inside = makeElement('(800) 555-1212');
    mod.setCursorPosition(inside, 6);
    assert.equal(inside.selectionStart, 6);

    const outside = makeElement('ab');
    mod.setCursorPosition(outside, 5);
    assert.equal(outside.selectionStart, 0);

    mod.setCursorPosition(null, 1);
});

test('getCursorPosition 读取 selectionStart 并回退零', async () => {
    const mod = await import(moduleUrl.href);

    assert.equal(mod.getCursorPosition(makeElement('abc')), 0);

    const element = makeElement('abc');
    element.selectionStart = 2;
    assert.equal(mod.getCursorPosition(element), 2);

    assert.equal(mod.getCursorPosition(null), 0);
});
