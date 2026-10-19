/**
 * InputOTP JavaScript interop 模块。
 * 为兼容 .NET 8 从粘贴事件读取剪贴板数据，
 * 因为 ClipboardEventArgs 在 C# 侧不暴露粘贴文本。
 */

const instances = new Map();

/**
 * 为 OTP 容器元素挂载粘贴事件处理器。
 * @param {HTMLElement} element - OTP 容器元素。
 * @param {DotNetObject} dotNetRef - Blazor 组件的引用。
 * @param {string} instanceId - 本实例的唯一 ID。
 * @param {string} otpMode - C# 枚举 InputOTPInputMode（Numbers、Letters、LettersAndNumbers）。
 */
export function initialize(element, dotNetRef, instanceId, otpMode) {
    if (!element || !dotNetRef) return;

    const handlePaste = (event) => {
        const text = event.clipboardData?.getData('text') ?? '';
        event.preventDefault();
        dotNetRef.invokeMethodAsync('OnPasteJs', text).catch(err => {
            console.error('Error invoking OnPasteJs:', err);
        });
    };

    element.addEventListener('paste', handlePaste);

    const handleKeyDown = (event) => {
        const currentMode = instances.get(instanceId)?.otpMode;

        if (_isControlKey(event.key)) {
            return;
        } else if (event.isComposing) {
            return;
        } else if (event.ctrlKey || event.metaKey) {
            return;
        } else if (currentMode && !_isCharAllowed(event.key, currentMode)) {
            event.preventDefault();
        }
    };

    element.addEventListener('keydown', handleKeyDown);

    instances.set(instanceId, { element, handlePaste, handleKeyDown, otpMode });
}

/**
 * 更新既有 OTP 实例允许的字符模式。
 * @param {string} instanceId - 要更新的实例。
 * @param {string} otpMode - 新的 InputOTPInputMode 值。
 */
export function updateOtpMode(instanceId, otpMode) {
    const instance = instances.get(instanceId);
    if (!instance) return;
    instance.otpMode = otpMode;
}

function _isControlKey(key) {
    return key.length !== 1;
}

function _isCharAllowed(char, otpMode) {
    if (otpMode === 'Numbers') {
        return /\d/.test(char);
    } else if (otpMode === 'Letters') {
        return /[a-zA-Z]/.test(char);
    } else if (otpMode === 'LettersAndNumbers') {
        return /[a-zA-Z0-9]/.test(char);
    }
    return true;
}

/**
 * 移除粘贴事件处理器并清理。
 * @param {string} instanceId - 要释放的实例。
 */
export function dispose(instanceId) {
    const instance = instances.get(instanceId);
    if (!instance) return;
    instance.element.removeEventListener('paste', instance.handlePaste);
    instance.element.removeEventListener('keydown', instance.handleKeyDown);
    instances.delete(instanceId);
}
