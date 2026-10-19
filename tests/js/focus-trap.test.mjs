// -c：focus-trap.js 直接 file-URL 导入的行为测试（同名件 1:1，仅模块路径改名）。
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { createFocusTrap } from '../../src/AtomBlazor.Primitives/wwwroot/js/primitives/focus-trap.js';

function fixture(count = 2) {
    const listeners = new Map();
    globalThis.document = { activeElement: null };
    globalThis.window = { getComputedStyle: () => ({ display: 'block', visibility: 'visible' }) };
    const element = () => ({ focus() { document.activeElement = this; } });
    const buttons = Array.from({ length: count }, element);
    const listbox = element();
    const container = {
        ...element(),
        querySelectorAll: () => buttons,
        querySelector: () => listbox,
        matches: () => false,
        contains: target => target === listbox || buttons.includes(target),
        addEventListener: (name, handler) => listeners.set(name, handler),
        removeEventListener: name => listeners.delete(name),
    };
    const cleanup = createFocusTrap(container);
    const pressTab = (shiftKey = false) => {
        let prevented = false;
        listeners.get('keydown')({ key: 'Tab', shiftKey, preventDefault: () => prevented = true });
        return prevented;
    };
    return { buttons, listbox, container, cleanup, listeners, pressTab };
}

test('Tab from an autofocused listbox enters the modal tab sequence in either direction', () => {
    const { buttons, listbox, pressTab } = fixture();
    assert.equal(document.activeElement, listbox);
    assert.equal(pressTab(), true);
    assert.equal(document.activeElement, buttons[0]);
    listbox.focus();
    assert.equal(pressTab(true), true);
    assert.equal(document.activeElement, buttons[1]);
});

test('an empty modal retains focus on its container for both Tab directions', () => {
    const { container, pressTab } = fixture(0);
    for (const shift of [false, true]) {
        assert.equal(pressTab(shift), true);
        assert.equal(document.activeElement, container);
    }
});

test('boundary wrapping is preserved and disposing the trap removes its listener', () => {
    const { buttons, pressTab, cleanup, listeners } = fixture(3);
    buttons[0].focus();
    assert.equal(pressTab(true), true);
    assert.equal(document.activeElement, buttons[2]);
    assert.equal(pressTab(), true);
    assert.equal(document.activeElement, buttons[0]);
    buttons[1].focus();
    assert.equal(pressTab(), false);
    cleanup.apply();
    assert.equal(listeners.size, 0);
});
