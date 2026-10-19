/**
 * 基元的键盘导航助手
 * 阻止导航按键的默认滚动行为
 */

export function setupKeyboardNav(element, dotNetRef) {
    if (!element) return { dispose: () => {} };

    const handleKeyDown = (e) => {
        // 阻止导航按键的默认滚动行为
        if (['ArrowDown', 'ArrowUp', 'Home', 'End', 'PageUp', 'PageDown'].includes(e.key)) {
            e.preventDefault();
        }
    };

    element.addEventListener('keydown', handleKeyDown);

    // 返回清理函数
    return {
        dispose: () => {
            element.removeEventListener('keydown', handleKeyDown);
        }
    };
}

/**
 * 按 DOM 顺序取得容器内的菜单项。
 * 包含 menuitem、menuitemcheckbox 与 menuitemradio 三种角色。
 * @param {HTMLElement} container - 菜单容器元素
 * @returns {HTMLElement[]} 按 DOM 顺序排列的菜单项元素数组
 */
function getMenuItemsInDomOrder(container) {
    if (!container) return [];
    return Array.from(container.querySelectorAll('[role="menuitem"], [role="menuitemcheckbox"], [role="menuitemradio"]'));
}

/**
 * 按 DOM 顺序取得可用（未禁用）的菜单项。
 * @param {HTMLElement} container - 菜单容器元素
 * @returns {HTMLElement[]} 按 DOM 顺序排列的可用菜单项元素数组
 */
function getEnabledMenuItems(container) {
    return getMenuItemsInDomOrder(container).filter(item =>
        item.getAttribute('aria-disabled') !== 'true'
    );
}

/**
 * 导航到 DOM 顺序中的下一个菜单项。
 * @param {HTMLElement} container - 菜单容器元素
 * @param {boolean} loop - 是否从末项回绕到首项
 * @returns {HTMLElement|null} 获得焦点的元素，没有则为 null
 */
export function navigateNext(container, loop = true) {
    const items = getEnabledMenuItems(container);
    if (items.length === 0) return null;

    const currentIndex = items.findIndex(item => item === document.activeElement);
    let nextIndex;

    if (currentIndex === -1) {
        nextIndex = 0;
    } else if (currentIndex === items.length - 1) {
        nextIndex = loop ? 0 : currentIndex;
    } else {
        nextIndex = currentIndex + 1;
    }

    items[nextIndex]?.focus();
    return items[nextIndex] || null;
}

/**
 * 导航到 DOM 顺序中的上一个菜单项。
 * @param {HTMLElement} container - 菜单容器元素
 * @param {boolean} loop - 是否从首项回绕到末项
 * @returns {HTMLElement|null} 获得焦点的元素，没有则为 null
 */
export function navigatePrevious(container, loop = true) {
    const items = getEnabledMenuItems(container);
    if (items.length === 0) return null;

    const currentIndex = items.findIndex(item => item === document.activeElement);
    let prevIndex;

    if (currentIndex === -1) {
        prevIndex = items.length - 1;
    } else if (currentIndex === 0) {
        prevIndex = loop ? items.length - 1 : 0;
    } else {
        prevIndex = currentIndex - 1;
    }

    items[prevIndex]?.focus();
    return items[prevIndex] || null;
}

/**
 * 导航到第一个菜单项。
 * @param {HTMLElement} container - 菜单容器元素
 * @returns {HTMLElement|null} 获得焦点的元素，没有则为 null
 */
export function navigateFirst(container) {
    const items = getEnabledMenuItems(container);
    if (items.length === 0) return null;

    items[0]?.focus();
    return items[0] || null;
}

/**
 * 导航到最后一个菜单项。
 * @param {HTMLElement} container - 菜单容器元素
 * @returns {HTMLElement|null} 获得焦点的元素，没有则为 null
 */
export function navigateLast(container) {
    const items = getEnabledMenuItems(container);
    if (items.length === 0) return null;

    const lastIndex = items.length - 1;
    items[lastIndex]?.focus();
    return items[lastIndex] || null;
}


// 列表的 .NET 处理函数会改变选中项；对这个浏览器事件必须做取消处理。
export function setupListBox(element) {
    if (!element) return { dispose() {} };
    const keydown = event => {
        if (element.getAttribute('aria-disabled') === 'true') return;
        const selectAll = element.getAttribute('aria-multiselectable') === 'true'
            && (event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'a';
        if (selectAll || ['ArrowDown', 'ArrowUp', 'Home', 'End', 'PageUp', 'PageDown', ' ', 'Enter'].includes(event.key)) {
            event.preventDefault();
        }
    };
    element.addEventListener('keydown', keydown);
    return { dispose: () => element.removeEventListener('keydown', keydown) };
}
