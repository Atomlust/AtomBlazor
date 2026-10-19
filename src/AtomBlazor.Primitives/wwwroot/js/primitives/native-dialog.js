// 供 Blazor 组件使用的原生 <dialog> 渲染助手。
// 驱动浏览器内建的 <dialog> 元素（顶层 top layer、焦点捕获、Escape、::backdrop），
// 取代 JS 侧焦点捕获/滚动锁定/门户的一整套握手。这消除了跨 Blazor 渲染模式边界
//（例如 InteractiveWebAssembly）时会破坏门户化浮层的"作用域服务 + 渲染握手"依赖。
//
// 注意：本模块刻意只使用函数声明与 globalThis 状态——不出现顶层 `let`/`const`/`class`。
// Blazor WebAssembly 的动态 `import()` 可能在共享作用域里重新求值一个模块，
// 顶层词法绑定届时会以 "Identifier has already been declared" 相撞。
// 函数声明与对 globalThis 的赋值则能安全地挺过这种重新求值。

/**
 * 检测浏览器是否支持带 showModal() 的原生 <dialog> 元素。
 * 基线：Chrome 37+、Firefox 98+、Safari 15.4+。首次调用后缓存。
 * @returns {boolean}
 */
export function supportsNativeDialog() {
    if (globalThis.__atomSupportsNativeDialog !== undefined) {
        return globalThis.__atomSupportsNativeDialog;
    }
    globalThis.__atomSupportsNativeDialog = typeof HTMLDialogElement !== 'undefined'
        && typeof HTMLDialogElement.prototype.showModal === 'function';
    return globalThis.__atomSupportsNativeDialog;
}

/**
 * 以模态（顶层）方式打开一个 <dialog> 元素。已打开时为空操作。
 * @param {HTMLDialogElement} element
 */
export function showModal(element) {
    if (!element) {
        return;
    }
    if (supportsNativeDialog() && !element.open) {
        element.showModal();
    }
}

/**
 * 关闭一个 <dialog> 元素。未打开时为空操作。
 * @param {HTMLDialogElement} element
 * @param {string|null} [returnValue]
 */
export function closeDialog(element, returnValue) {
    if (!element) {
        return;
    }
    if (supportsNativeDialog() && element.open) {
        element.close(returnValue ?? null);
    }
}

/**
 * 聚焦对话框自身（浏览器原生焦点捕获已把 Tab 约束在模态对话框内）。
 * 回退到第一个可聚焦元素。
 * @param {HTMLDialogElement} element
 */
export function focusDialog(element) {
    if (!element) {
        return;
    }
    if (document.activeElement === element) {
        return;
    }
    element.focus();
    if (document.activeElement !== element) {
        const focusable = element.querySelector(
            'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), '
            + 'textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
        );
        if (focusable) {
            focusable.focus();
        }
    }
}

/**
 * 聚焦打开对话框的那个元素，把焦点还给触发器。
 * @param {HTMLElement} element
 */
export function focusElement(element) {
    if (element && typeof element.focus === 'function') {
        element.focus();
    }
}

// ============================================================================
// 原生 <dialog> 事件接线
// Blazor 无法可靠区分遮罩点击与内容点击，而 Escape（cancel）必须可被阻止，
// 好让组件遵守自己的 CloseOnEscape。以下监听器把原生事件转发给 .NET，
// 由它掌管关闭逻辑。
// ============================================================================

/**
 * 把原生对话框的生命周期事件接到一个 .NET 实例上。
 * 要求该 .NET 对象暴露以下 JSInvokable 方法：
 *   JsOnNativeCancel()          - Escape 被按下（cancel 在此被阻止）
 *   JsOnNativeClose()           - 对话框已关闭（兜底）
 *   JsOnNativeBackdropClick()   - 点击落在了对话框自身（遮罩）
 * @param {HTMLDialogElement} dialog
 * @param {object} dotNetRef - 一个 DotNetObjectReference
 * @returns {{ dispose: () => void }}
 */
export function setupDialog(dialog, dotNetRef) {
    const onCancel = (e) => {
        e.preventDefault();
        dotNetRef.invokeMethodAsync('JsOnNativeCancel');
    };
    const onClose = () => {
        dotNetRef.invokeMethodAsync('JsOnNativeClose');
    };
    const onClick = (e) => {
        // 原生模态对话框占满顶层，所以点击 ::backdrop（对话框之外）与点击对话框
        // 自己的盒子（含内边距）都会解析为 e.target === dialog。只有前者是遮罩 dismissal；
        // 对话框内部——连内边距一起——的点击不得关闭它。用点击坐标与对话框边框盒比较
        // 来区分二者（JS 路径把内容与遮罩渲染为两个元素，从不混淆）。
        if (e.target !== dialog) {
            return;
        }
        const rect = dialog.getBoundingClientRect();
        const x = e.clientX;
        const y = e.clientY;
        const insideBox = x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom;
        if (!insideBox) {
            dotNetRef.invokeMethodAsync('JsOnNativeBackdropClick');
        }
    };

    dialog.addEventListener('cancel', onCancel);
    dialog.addEventListener('close', onClose);
    dialog.addEventListener('click', onClick);

    return {
        dispose: () => {
            dialog.removeEventListener('cancel', onCancel);
            dialog.removeEventListener('close', onClose);
            dialog.removeEventListener('click', onClick);
        }
    };
}
