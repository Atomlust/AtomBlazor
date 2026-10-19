// AtomBlazor 的全局键盘快捷键处理器
// 提供文档级键盘事件处理与 Blazor interop 的桥接

let dotNetRef = null;
let registeredShortcuts = new Set();
let keydownHandler = null;

/**
 * 初始化键盘快捷键监听器。
 * @param {Object} dotNetReference - 用于回调进 C# 的 DotNet 对象引用
 */
export function initialize(dotNetReference) {
    dotNetRef = dotNetReference;

    keydownHandler = handleKeyDown;
    document.addEventListener('keydown', keydownHandler, true);
}

/**
 * 登记要监听的组合键。
 * @param {string} normalizedKey - 规范化后的组合键（例如 "ctrl+shift+t"）
 */
export function registerShortcut(normalizedKey) {
    registeredShortcuts.add(normalizedKey.toLowerCase());
}

/**
 * 注销一个组合键。
 * @param {string} normalizedKey - 要移除的规范化组合键
 */
export function unregisterShortcut(normalizedKey) {
    registeredShortcuts.delete(normalizedKey.toLowerCase());
}

/**
 * 释放键盘快捷键监听器并清理全部资源。
 */
export function dispose() {
    if (keydownHandler) {
        document.removeEventListener('keydown', keydownHandler, true);
        keydownHandler = null;
    }
    dotNetRef = null;
    registeredShortcuts.clear();
}

/**
 * 处理 keydown 事件并触发已登记的快捷键。
 * @param {KeyboardEvent} event - 键盘事件
 */
function handleKeyDown(event) {
    // 目标若是输入类元素则跳过（用户正在打字）
    if (shouldSkipEvent(event)) {
        return;
    }

    const normalizedKey = getNormalizedKey(event);

    if (registeredShortcuts.has(normalizedKey)) {
        event.preventDefault();
        event.stopPropagation();

        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('HandleShortcutAsync', normalizedKey)
                .catch(err => console.error('Error invoking shortcut handler:', err));
        }
    }
}

/**
 * 判断该键盘事件是否应被跳过。
 * @param {KeyboardEvent} event - 键盘事件
 * @returns {boolean} 应跳过时为 true
 */
function shouldSkipEvent(event) {
    const target = event.target;
    if (!target) return false;

    // 在输入框里打字时跳过
    if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') {
        return true;
    }

    // contenteditable 中跳过
    if (target.isContentEditable) {
        return true;
    }

    // select 元素中跳过
    if (target.tagName === 'SELECT') {
        return true;
    }

    return false;
}

/**
 * 把键盘事件规范化为一致的键串。
 * @param {KeyboardEvent} event - 键盘事件
 * @returns {string} 规范化后的键串（例如 "ctrl+shift+t"）
 */
function getNormalizedKey(event) {
    const parts = [];

    // Ctrl 或 Meta（Mac 上的 Cmd）——同等对待
    if (event.ctrlKey || event.metaKey) {
        parts.push('ctrl');
    }

    if (event.shiftKey) {
        parts.push('shift');
    }

    if (event.altKey) {
        parts.push('alt');
    }

    // 规范化主键
    let key = normalizeKeyName(event.key);
    parts.push(key);

    return parts.join('+');
}

/**
 * 规范化键名以便一致比较。
 * @param {string} key - 事件里的键名
 * @returns {string} 规范化后的键名
 */
function normalizeKeyName(key) {
    const lowerKey = key.toLowerCase();

    // 处理特殊键名
    const keyMap = {
        ' ': 'space',
        'escape': 'escape',
        'enter': 'enter',
        'tab': 'tab',
        'backspace': 'backspace',
        'delete': 'delete',
        'insert': 'insert',
        'home': 'home',
        'end': 'end',
        'pageup': 'pageup',
        'pagedown': 'pagedown',
        'arrowup': 'arrowup',
        'arrowdown': 'arrowdown',
        'arrowleft': 'arrowleft',
        'arrowright': 'arrowright'
    };

    return keyMap[lowerKey] || lowerKey;
}
