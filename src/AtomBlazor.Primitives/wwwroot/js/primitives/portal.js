// Blazor 组件的门户（Portal）渲染助手
// 协助在文档 body 层级渲染内容

/**
 * 若不存在则在文档 body 层级创建门户容器。
 * @param {string} id - 门户容器 ID
 * @returns {HTMLElement} 门户容器元素
 */
export function ensurePortalContainer(id = 'atom-portal-root') {
    let container = document.getElementById(id);

    if (!container) {
        container = document.createElement('div');
        container.id = id;
        container.style.position = 'fixed';
        container.style.zIndex = '9999';
        container.style.top = '0';
        container.style.left = '0';
        container.style.pointerEvents = 'none'; // 让点击穿过空白区域
        document.body.appendChild(container);
    }

    return container;
}

/**
 * 在门户容器为空时移除它。
 * @param {string} id - 门户容器 ID
 */
export function cleanupPortalContainer(id = 'atom-portal-root') {
    const container = document.getElementById(id);

    if (container && container.children.length === 0) {
        container.remove();
    }
}

/**
 * 为元素建立门户渲染。把元素移到 document.body 层级，
 * 脱离父级的堆叠上下文。
 * @param {HTMLElement} element - 要门户化的元素
 * @param {string} portalId - 门户容器 ID
 * @returns {Object} 带 dispose() 方法的可释放对象
 */
export function setupPortal(element, portalId = 'atom-portal-root') {
    if (!element) {
        console.warn('setupPortal: element is null');
        return { dispose: () => {} };
    }

    const originalParent = element.parentElement;
    const originalNextSibling = element.nextSibling;

    const portal = ensurePortalContainer(portalId);
    portal.appendChild(element);

    // 为该元素开启指针事件
    element.style.pointerEvents = 'auto';

    return {
        dispose: () => {
            // 把元素移回原位，保证 Blazor 正常释放
            if (originalParent && document.body.contains(element)) {
                if (originalNextSibling && originalNextSibling.parentElement === originalParent) {
                    originalParent.insertBefore(element, originalNextSibling);
                } else {
                    originalParent.appendChild(element);
                }
            }

            // 门户容器空了就清理掉
            if (portal && portal.children.length === 0 && portal.parentElement) {
                portal.remove();
            }
        }
    };
}

/**
 * 取得用于正确堆叠的计算后 z-index。
 * @param {HTMLElement} element - 要检查的元素
 * @returns {number} 计算后的 z-index
 */
export function getComputedZIndex(element) {
    const computed = window.getComputedStyle(element);
    const zIndex = parseInt(computed.zIndex, 10);
    return isNaN(zIndex) ? 0 : zIndex;
}

// ============================================================================
// body 滚动锁（引用计数）
// 堆叠/嵌套的浮层（如 Dialog 里再开 AlertDialog）各自获取一把锁。
// 必须只在最后一把锁释放后才恢复 body 的原始滚动状态——否则嵌套浮层的清理
// 拿到的是"已经锁定"的 hidden 状态，外层浮层的恢复会被它冲掉，页面无论
// 按什么顺序释放都会永久冻结。
// ============================================================================

let scrollLockCount = 0;
let savedScrollState = null;

/**
 * 锁定 body 滚动（模态框可用）。引用计数，嵌套浮层共享同一把底层锁。
 * @returns {Object} 带 apply() 方法（释放本把锁）的对象
 */
export function lockBodyScroll() {
    if (scrollLockCount === 0) {
        // 第一把锁：在改动之前捕获真正的原始状态。
        const scrollbarWidth = window.innerWidth - document.documentElement.clientWidth;
        savedScrollState = {
            overflow: document.body.style.overflow,
            paddingRight: document.body.style.paddingRight
        };

        document.body.style.overflow = 'hidden';

        // 用补 padding 的方式为滚动条占位，防止布局抖动
        if (scrollbarWidth > 0) {
            document.body.style.paddingRight = `${scrollbarWidth}px`;
        }
    }

    scrollLockCount++;

    // 防止同一个句柄被释放多次（如"关闭 + 释放"两条路径）。
    let released = false;

    // 清理函数包在对象里返回，供 C# interop 使用
    const cleanup = () => {
        if (released) {
            return;
        }
        released = true;
        scrollLockCount = Math.max(0, scrollLockCount - 1);

        // 只在所有锁都释放后恢复。
        if (scrollLockCount === 0 && savedScrollState) {
            document.body.style.overflow = savedScrollState.overflow;
            document.body.style.paddingRight = savedScrollState.paddingRight;
            savedScrollState = null;
        }
    };

    return {
        apply: cleanup
    };
}

/**
 * 检查元素当前是否在视口内可见。
 * @param {HTMLElement} element - 要检查的元素
 * @returns {boolean} 元素可见则为 true
 */
export function isElementInViewport(element) {
    const rect = element.getBoundingClientRect();

    return (
        rect.top >= 0 &&
        rect.left >= 0 &&
        rect.bottom <= (window.innerHeight || document.documentElement.clientHeight) &&
        rect.right <= (window.innerWidth || document.documentElement.clientWidth)
    );
}

// ============================================================================
// 自动聚焦监听器
// 带 data-autofocus 特性的元素变得可见时自动聚焦。
// 监听定位服务派发的 'atom:visible' 事件
// （已交付派发方：positioning.js applyPosition/hidePosition）。
// ============================================================================

let autofocusListenerInitialized = false;

function initAutofocusListener() {
    if (autofocusListenerInitialized) return;

    document.addEventListener('atom:visible', (event) => {
        const element = event.target;

        // 检查元素自身或其任一子元素是否带 data-autofocus
        if (element.hasAttribute('data-autofocus')) {
            element.focus();
        } else {
            // 再查子元素
            const autofocusChild = element.querySelector('[data-autofocus]');
            if (autofocusChild) {
                autofocusChild.focus();
            }
        }
    });

    autofocusListenerInitialized = true;
}

// 模块加载时初始化监听器
initAutofocusListener();

/**
 * 为刚变得可见的元素触发自动聚焦。
 * 供不使用定位服务的组件调用。
 * @param {HTMLElement} element - 现已可见的元素
 */
export function triggerAutofocus(element) {
    if (!element) return;

    // 派发与定位服务相同的事件
    element.dispatchEvent(new CustomEvent('atom:visible', { bubbles: true }));
}

/**
 * 在给定坐标处重新派发一个 contextmenu 事件。
 * 临时隐藏浮层与内容，以找到其下方的元素。
 * @param {HTMLElement} overlay - 检测期间要隐藏的浮层元素
 * @param {HTMLElement} content - 检测期间要隐藏的菜单内容元素
 * @param {number} clientX - client X 坐标
 * @param {number} clientY - client Y 坐标
 */
export function redispatchContextMenu(overlay, content, clientX, clientY) {
    overlay.style.display = 'none';
    if (content) {
        content.style.display = 'none';
    }

    const target = document.elementFromPoint(clientX, clientY);

    overlay.style.display = '';
    if (content) {
        content.style.display = '';
    }

    if (target) {
        target.dispatchEvent(new MouseEvent('contextmenu', {
            bubbles: true,
            cancelable: true,
            clientX: clientX,
            clientY: clientY,
            view: window
        }));
    }
}
