/**
 * 导航菜单键盘导航辅助模块
 * 处理导航菜单下拉内容内部的箭头键导航。
 * 与 primitives 的 keyboard-nav.js 不同，这里瞄准的是可聚焦链接（a[href]）
 * 而不是 role="menuitem" 元素，因为导航菜单里常见的
 * 是含普通锚标签的自定义内容。
 */

/**
 * 按 DOM 顺序获取容器内可聚焦的元素。
 * 瞄准带 href 属性且可见的锚标签，以及 role="menuitem" 元素。
 * @param {HTMLElement} container - 菜单容器元素
 * @returns {HTMLElement[]} 按 DOM 顺序排列的可聚焦元素数组
 */
function getFocusableElements(container) {
    if (!container) return [];

    // 找出所有带 href 的锚标签与 role="menuitem" 元素
    const elements = Array.from(container.querySelectorAll('a[href], [role="menuitem"]'));

    // 过滤掉隐藏或被禁用的元素
    return elements.filter(el => {
        if (el.getAttribute('aria-disabled') === 'true') return false;
        if (el.hidden) return false;

        // 检查元素是否可见
        const style = window.getComputedStyle(el);
        if (style.display === 'none' || style.visibility === 'hidden') return false;

        return true;
    });
}

/**
 * 导航到 DOM 顺序中的下一个可聚焦元素。
 * @param {HTMLElement} container - 菜单容器元素
 * @returns {HTMLElement|null} 被聚焦的元素，无则返回 null
 */
export function navigateNext(container) {
    const items = getFocusableElements(container);
    if (items.length === 0) return null;

    const currentIndex = items.findIndex(item => item === document.activeElement);
    let nextIndex;

    if (currentIndex === -1) {
        nextIndex = 0;
    } else if (currentIndex === items.length - 1) {
        nextIndex = 0; // 回环到第一个
    } else {
        nextIndex = currentIndex + 1;
    }

    items[nextIndex]?.focus();
    return items[nextIndex] || null;
}

/**
 * 导航到 DOM 顺序中的上一个可聚焦元素。
 * @param {HTMLElement} container - 菜单容器元素
 * @returns {HTMLElement|null} 被聚焦的元素，无则返回 null
 */
export function navigatePrevious(container) {
    const items = getFocusableElements(container);
    if (items.length === 0) return null;

    const currentIndex = items.findIndex(item => item === document.activeElement);
    let prevIndex;

    if (currentIndex === -1) {
        prevIndex = items.length - 1;
    } else if (currentIndex === 0) {
        prevIndex = items.length - 1; // 回环到最后一个
    } else {
        prevIndex = currentIndex - 1;
    }

    items[prevIndex]?.focus();
    return items[prevIndex] || null;
}

/**
 * 导航到第一个可聚焦元素。
 * @param {HTMLElement} container - 菜单容器元素
 * @returns {HTMLElement|null} 被聚焦的元素，无则返回 null
 */
export function navigateFirst(container) {
    const items = getFocusableElements(container);
    if (items.length === 0) return null;

    items[0]?.focus();
    return items[0] || null;
}

/**
 * 导航到最后一个可聚焦元素。
 * @param {HTMLElement} container - 菜单容器元素
 * @returns {HTMLElement|null} 被聚焦的元素，无则返回 null
 */
export function navigateLast(container) {
    const items = getFocusableElements(container);
    if (items.length === 0) return null;

    const lastIndex = items.length - 1;
    items[lastIndex]?.focus();
    return items[lastIndex] || null;
}

/**
 * 聚焦给定的元素。
 * @param {HTMLElement} element - 要聚焦的元素
 */
export function focusElement(element) {
    if (element) {
        element.focus();
    }
}

/**
 * 在容器上装配键盘导航。
 * 使用元素级监听器，避免干扰其他组件。
 * 返回一个清理函数。
 * @param {HTMLElement} container - 菜单容器元素
 * @param {DotNetObjectReference} dotNetRef - 回调进 Blazor 的引用
 */
export function setupKeyboardNavigation(container, dotNetRef) {
    const handleKeyDown = (e) => {
        const items = getFocusableElements(container);
        const focusedItemIndex = items.indexOf(document.activeElement);
        const hasFocusedItem = focusedItemIndex !== -1;

        switch (e.key) {
            case 'ArrowDown':
                e.preventDefault();
                if (!hasFocusedItem) {
                    navigateFirst(container);
                } else {
                    navigateNext(container);
                }
                break;
            case 'ArrowUp':
                e.preventDefault();
                if (!hasFocusedItem) {
                    navigateLast(container);
                } else {
                    navigatePrevious(container);
                }
                break;
            case 'Home':
                e.preventDefault();
                navigateFirst(container);
                break;
            case 'End':
                e.preventDefault();
                navigateLast(container);
                break;
            case 'Escape':
                e.preventDefault();
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync('HandleEscapeKey');
                }
                break;
        }
    };

    // 元素级监听器——只在容器或其子元素持有焦点时触发
    container.addEventListener('keydown', handleKeyDown);

    // 返回清理对象
    return {
        dispose: () => {
            container.removeEventListener('keydown', handleKeyDown);
        }
    };
}
