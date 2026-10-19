// 为模态对话框与浮层提供的焦点捕获实现
// 基于焦点管理的无障碍最佳实践

const focusableSelectors = [
    'a[href]',
    'button:not([disabled])',
    'input:not([disabled])',
    'textarea:not([disabled])',
    'select:not([disabled])',
    '[tabindex]:not([tabindex="-1"])',
    'audio[controls]',
    'video[controls]'
].join(', ');

/**
 * 在指定容器元素内建立焦点捕获。
 *
 * 初始焦点目标按下面的顺序解析，让调用方总能压过默认值，而不必注入哑表序停靠点：
 *   1. 作为 `initialFocusElement` 传入的显式元素
 *   2. 容器内的 `[data-autofocus]` 元素——与 portal.js 已遵守的约定相同
 *   3. `mode`：'container' 聚焦容器自身，'none' 保持焦点原地不动
 *   4. 第一个可 Tab 到的后代——历史行为，也仍是默认值
 *
 * @param {HTMLElement} container - 要捕获焦点的容器元素
 * @param {'first'|'container'|'none'} [mode='first'] - 没有显式目标、也没有
 *   [data-autofocus] 元素时要聚焦的对象
 * @param {HTMLElement} [initialFocusElement=null] - 显式聚焦元素，优先级压过一切规则
 * @returns {Object} 携带 `apply` 清理函数的对象，供 IJSObjectReference 使用
 */
export function createFocusTrap(container, mode = 'first', initialFocusElement = null) {
    if (!container) {
        console.warn('Focus trap: container is null or undefined');
        return () => {};
    }

    const getFocusableElements = () => {
        return Array.from(container.querySelectorAll(focusableSelectors))
            .filter(el => {
                const style = window.getComputedStyle(el);
                return style.display !== 'none' && style.visibility !== 'hidden';
            });
    };

    const handleKeyDown = (e) => {
        if (e.key !== 'Tab') return;

        const focusableElements = getFocusableElements();
        if (focusableElements.length === 0) {
            e.preventDefault();
            container.focus();
            return;
        }

        const firstElement = focusableElements[0];
        const lastElement = focusableElements[focusableElements.length - 1];

        // autofocus 可能指向容器或 tabindex=-1 的 listbox。它们不在 Tab 序列里，
        // WebKit 否则可能直接跳到模态层背后的页面。按要求的方向显式进入被捕获的序列。
        if (!focusableElements.includes(document.activeElement)) {
            e.preventDefault();
            (e.shiftKey ? lastElement : firstElement).focus();
            return;
        }

        // 第一个元素上按 Shift + Tab：聚焦最后一个
        if (e.shiftKey && document.activeElement === firstElement) {
            e.preventDefault();
            lastElement.focus();
        }
        // 最后一个元素上按 Tab：聚焦第一个
        else if (!e.shiftKey && document.activeElement === lastElement) {
            e.preventDefault();
            firstElement.focus();
        }
    };

    container.addEventListener('keydown', handleKeyDown);

    // 解析初始焦点目标。`container.contains` 拦住不在捕获范围内的显式元素——
    // 那会把焦点放到被捕获对象之外。
    const resolveInitialFocus = () => {
        if (initialFocusElement && container.contains(initialFocusElement)) {
            return initialFocusElement;
        }

        const autofocusTarget = container.matches('[data-autofocus]')
            ? container
            : container.querySelector('[data-autofocus]');
        if (autofocusTarget) {
            return autofocusTarget;
        }

        if (mode === 'none') return null;
        if (mode === 'container') return container;

        return getFocusableElements()[0] ?? null;
    };

    const initialTarget = resolveInitialFocus();
    if (initialTarget) {
        initialTarget.focus();
    }

    // 清理函数包在对象里返回，供 C# 的 IJSObjectReference 使用
    const cleanup = () => {
        container.removeEventListener('keydown', handleKeyDown);
    };

    return {
        apply: cleanup
    };
}

/**
 * 聚焦容器内第一个可聚焦元素。
 * @param {HTMLElement} container - 要搜索的容器
 */
export function focusFirst(container) {
    if (!container) return;

    const focusableElements = Array.from(container.querySelectorAll(focusableSelectors))
        .filter(el => {
            const style = window.getComputedStyle(el);
            return style.display !== 'none' && style.visibility !== 'hidden';
        });

    if (focusableElements.length > 0) {
        focusableElements[0].focus();
    }
}

/**
 * 聚焦容器内最后一个可聚焦元素。
 * @param {HTMLElement} container - 要搜索的容器
 */
export function focusLast(container) {
    if (!container) return;

    const focusableElements = Array.from(container.querySelectorAll(focusableSelectors))
        .filter(el => {
            const style = window.getComputedStyle(el);
            return style.display !== 'none' && style.visibility !== 'hidden';
        });

    if (focusableElements.length > 0) {
        focusableElements[focusableElements.length - 1].focus();
    }
}
