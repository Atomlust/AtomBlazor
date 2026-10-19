/**
 * Masked Input JavaScript interop 模块。
 * 处理 Blazor 在键入过程中无法管理的光标定位。
 */

/**
 * 设置 input 元素的值并定位光标。
 * @param {HTMLInputElement} element - 输入元素。
 * @param {string} value - 要设置的值。
 * @param {number} cursorPosition - 光标定位的位置。
 */
export function setInputValue(element, value, cursorPosition) {
    if (!element) return;

    element.value = value;

    // 设置光标位置
    if (cursorPosition >= 0 && cursorPosition <= value.length) {
        element.setSelectionRange(cursorPosition, cursorPosition);
    }
}

/**
 * 设置 input 元素中的光标位置。
 * @param {HTMLInputElement} element - 输入元素。
 * @param {number} cursorPosition - 光标定位的位置。
 */
export function setCursorPosition(element, cursorPosition) {
    if (!element) return;

    // 立即设置光标位置
    if (cursorPosition >= 0 && cursorPosition <= element.value.length) {
        element.setSelectionRange(cursorPosition, cursorPosition);
    }
}

/**
 * 获取 input 元素当前的光标位置。
 * @param {HTMLInputElement} element - 输入元素。
 * @returns {number} 光标位置。
 */
export function getCursorPosition(element) {
    if (!element) return 0;
    return element.selectionStart || 0;
}
