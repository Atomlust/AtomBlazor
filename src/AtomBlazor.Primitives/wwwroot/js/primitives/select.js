// Select 基元工具：键盘导航、滚动与焦点管理
// 键盘处理全部放在 JavaScript 侧完成，避免与 Blazor 渲染产生竞态

/**
 * 按内容 id 记录当前激活的键盘处理器
 */
const activeHandlers = new Map();

/**
 * 按 DOM 顺序取得容器内的 select 选项元素。
 * @param {HTMLElement} container - select 内容容器元素
 * @returns {HTMLElement[]} 按 DOM 顺序排列的选项元素数组
 */
function getOptionsInDomOrder(container) {
    if (!container) return [];
    return Array.from(container.querySelectorAll('[role="option"]'));
}

/**
 * 按 DOM 顺序取得可用（未禁用）的选项。
 * @param {HTMLElement} container - select 内容容器元素
 * @returns {HTMLElement[]} 可用选项元素数组
 */
function getEnabledOptions(container) {
    return getOptionsInDomOrder(container).filter(item =>
        item.getAttribute('data-disabled') !== 'true' &&
        item.getAttribute('aria-disabled') !== 'true'
    );
}

/**
 * 取得当前高亮（focused）选项的下标。
 * @param {HTMLElement} container - select 内容容器
 * @returns {number} 高亮选项的下标，没有则返回 -1
 */
function getFocusedIndex(container) {
    const items = getEnabledOptions(container);
    return items.findIndex(item => item.getAttribute('data-focused') === 'true');
}

/**
 * 向上查找元素最近的可滚动祖先。
 * @param {HTMLElement} element - 要查找滚动父级的元素
 * @returns {HTMLElement|null} 最近的可滚动祖先，找不到返回 null
 */
function getScrollParent(element) {
    let parent = element.parentElement;
    while (parent) {
        const overflowY = getComputedStyle(parent).overflowY;
        if (overflowY === 'auto' || overflowY === 'scroll') {
            return parent;
        }
        parent = parent.parentElement;
    }
    return null;
}

/**
 * 把元素滚动到可滚动容器的可视区内，且绝不滚动整个页面。
 * @param {HTMLElement} element - 要滚入视野的元素
 * @param {HTMLElement} container - 兜底的滚动容器
 * @param {boolean} center - 是否把元素置于容器中央
 */
function scrollIntoContainerView(element, container, center = false) {
    if (!element || !container) return;

    // 容器本身不可滚动时，改用元素最近的滚动祖先
    const scrollContainer = getScrollParent(element) || container;

    // 计算元素相对滚动容器顶部的位置
    const elementTop = element.offsetTop;
    const elementHeight = element.offsetHeight;
    const containerScrollTop = scrollContainer.scrollTop;
    const containerHeight = scrollContainer.clientHeight;

    if (containerHeight <= 0 || elementHeight <= 0) return;

    if (center) {
        // 把元素居中到容器
        const targetScrollTop = elementTop - (containerHeight / 2) + (elementHeight / 2);
        scrollContainer.scrollTop = Math.max(0, targetScrollTop);
    } else {
        // 只有元素在可视区之外才滚动
        if (elementTop < containerScrollTop) {
            // 元素在可视区上方
            scrollContainer.scrollTop = elementTop;
        } else if (elementTop + elementHeight > containerScrollTop + containerHeight) {
            // 元素在可视区下方
            scrollContainer.scrollTop = elementTop + elementHeight - containerHeight;
        }
    }
}

/**
 * 给指定选项设置高亮标记。
 * @param {HTMLElement} container - select 内容容器
 * @param {number} index - 要高亮的选项下标
 * @param {boolean} center - 是否把选项居中到容器（用于初始选中项）
 */
function setFocusedOption(container, index, center = false, scroll = true) {
    const items = getEnabledOptions(container);

    // 先清掉所有项的高亮
    items.forEach(item => item.setAttribute('data-focused', 'false'));

    // 再给目标项设高亮
    if (index >= 0 && index < items.length) {
        items[index].setAttribute('data-focused', 'true');
        if (scroll) {
            // 用容器感知的滚动，避免把整个页面滚走
            scrollIntoContainerView(items[index], container, center);
        }
        setActiveDescendant(container, items[index].id);
    } else {
        setActiveDescendant(container, null);
    }
}

/**
 * 让 listbox 通过 aria-activedescendant 指向当前高亮项，供辅助技术读取。
 *
 * 这个属性过去由 C# 渲染：每按一次方向键都要走一个渲染批次——在 Blazor Server 上就是
 * 一次 circuit 往返——去移动一个浏览器本来就能自己移动的高亮。`data-focused` 写在这里、
 * `aria-activedescendant` 写在那里，两者会出现一个往返周期的不一致。现在两个属性归同一个写入方管。
 *
 * @param {HTMLElement} container - listbox 元素。
 * @param {string|null} id - 高亮选项的元素 id，无则传 null。
 */
function setActiveDescendant(container, id) {
    if (id) {
        container.setAttribute('aria-activedescendant', id);
    } else {
        container.removeAttribute('aria-activedescendant');
    }
}

/**
 * 移动到下一个选项。
 * @param {HTMLElement} container - select 内容容器
 * @param {boolean} loop - 末尾是否回绕到第一项
 */
function navigateNext(container, loop = true) {
    const items = getEnabledOptions(container);
    if (items.length === 0) return;

    const currentIndex = getFocusedIndex(container);
    let nextIndex;

    if (currentIndex === -1) {
        nextIndex = 0;
    } else if (currentIndex === items.length - 1) {
        nextIndex = loop ? 0 : currentIndex;
    } else {
        nextIndex = currentIndex + 1;
    }

    setFocusedOption(container, nextIndex);
}

/**
 * 移动到上一个选项。
 * @param {HTMLElement} container - select 内容容器
 * @param {boolean} loop - 首项是否回绕到末项
 */
function navigatePrevious(container, loop = true) {
    const items = getEnabledOptions(container);
    if (items.length === 0) return;

    const currentIndex = getFocusedIndex(container);
    let prevIndex;

    if (currentIndex === -1) {
        prevIndex = items.length - 1;
    } else if (currentIndex === 0) {
        prevIndex = loop ? items.length - 1 : 0;
    } else {
        prevIndex = currentIndex - 1;
    }

    setFocusedOption(container, prevIndex);
}

/**
 * 移动到第一个选项。
 * @param {HTMLElement} container - select 内容容器
 */
function navigateFirst(container) {
    setFocusedOption(container, 0);
}

/**
 * 移动到最后一个选项。
 * @param {HTMLElement} container - select 内容容器
 */
function navigateLast(container) {
    const items = getEnabledOptions(container);
    setFocusedOption(container, items.length - 1);
}

/**
 * 选中当前高亮项：直接触发它的 click 处理器。
 * @param {HTMLElement} container - select 内容容器
 */
function selectFocusedOption(container) {
    const items = getEnabledOptions(container);
    const focusedIndex = getFocusedIndex(container);

    if (focusedIndex >= 0 && focusedIndex < items.length) {
        items[focusedIndex].click();
    }
}

/**
 * 为 select 内容元素挂上键盘导航。
 * 由 Blazor 在 select 打开时调用。
 * @param {string} contentId - select 内容元素的 id
 * @param {object} dotNetRef - 回调用的 Blazor 组件引用
 * @returns {object} 带 dispose 方法的清理对象
 */
export function setupKeyboardNavigation(contentId, dotNetRef, autoFocus = true) {
    const container = document.getElementById(contentId);
    if (!container) {
        return { dispose: () => {} };
    }

    // 先清理该内容 id 上已有的处理器
    if (activeHandlers.has(contentId)) {
        activeHandlers.get(contentId).dispose();
    }

    const handleKeyDown = async (e) => {
        switch (e.key) {
            case 'ArrowDown':
                e.preventDefault();
                navigateNext(container, true);
                break;

            case 'ArrowUp':
                e.preventDefault();
                navigatePrevious(container, true);
                break;

            case 'Home':
                e.preventDefault();
                navigateFirst(container);
                break;

            case 'End':
                e.preventDefault();
                navigateLast(container);
                break;

            case 'Enter':
            case ' ':
                e.preventDefault();
                selectFocusedOption(container);
                break;

            case 'Escape':
                e.preventDefault();
                // 这个 Escape 归我们处理，绝不能继续冒泡到 document 级 escape 栈——
                // 否则对话框里打开的 select 会同时关掉两层。
                e.stopPropagation();
                if (dotNetRef) {
                    await dotNetRef.invokeMethodAsync('JsOnEscapeKey');
                }
                break;

            case 'Tab':
                if (dotNetRef) {
                    await dotNetRef.invokeMethodAsync('JsOnTabKey');
                }
                break;
        }
    };

    // 悬停移动高亮。之所以写在这里而不是给每个选项挂 @onmouseenter：那个处理器要回调 C#
    // 来设置高亮下标，导致每一项都重新渲染——指针每扫过一项就是一次 circuit 往返，
    // 而高亮本是浏览器自己能移动的东西。委托到容器上，开销不随选项数量增长。
    const handleMouseOver = (e) => {
        const option = e.target.closest?.('[role="option"]');
        if (!option || !container.contains(option)) {
            return;
        }

        if (option.getAttribute('data-disabled') === 'true' ||
            option.getAttribute('aria-disabled') === 'true') {
            return;
        }

        const index = getEnabledOptions(container).indexOf(option);
        if (index >= 0 && index !== getFocusedIndex(container)) {
            // 不滚动：指针已经停在这一项上，此时滚动等于把列表从光标底下抽走。
            setFocusedOption(container, index, false, false);
        }
    };

    // 把处理器挂到容器上
    container.addEventListener('keydown', handleKeyDown);
    container.addEventListener('mouseover', handleMouseOver);

    // 调用方选择退出时，焦点不归这里管。overlay.open 就会退出：它在展示 listbox 之前就
    // 挂好了处理器——而 `visibility: hidden` 的元素根本拿不到焦点，这里强行聚焦只会静默失败，
    // 之后每个按键都跑到 trigger 上去。
    if (autoFocus) {
        requestAnimationFrame(() => {
            requestAnimationFrame(() => focusListbox(contentId));
        });
    }

    const cleanup = {
        dispose: () => {
            container.removeEventListener('keydown', handleKeyDown);
            container.removeEventListener('mouseover', handleMouseOver);
            activeHandlers.delete(contentId);
        }
    };

    activeHandlers.set(contentId, cleanup);
    return cleanup;
}

/**
 * 把标记为当前值的元素滚进它自己滚动容器的可视区。
 *
 * 面向用图标而不是 `aria-selected` 呈现当前值的场景——combobox 会把 `aria-selected`
 * 读作「键盘焦点所在」，那和「当前值」是两回事——所以由使用方自己标记当前元素，
 * 在这里指明标记选择器。
 *
 * 它必须在浮层展示之前运行，也因此只能是浏览器侧调用：列表出现时就应当已经滚动到
 * 选中项，先显示再滚动会看到一次跳动。停放（parked）元素仍然参与布局——隐藏用的是
 * `visibility` 而非 `display`——所以这里需要的测量值全部有效。
 *
 * @param {string} containerId - 在其中查找的容器元素 id。
 * @param {string} selector - 当前项的 CSS 选择器，默认用 select 自己的。
 */
export function scrollMarkedIntoView(containerId, selector = '[role="option"][aria-selected="true"]') {
    const container = document.getElementById(containerId);
    if (!container) return;

    const target = container.querySelector(selector);
    if (!target) return;

    scrollIntoContainerView(target, container, true);
}

/**
 * 把键盘焦点交给 listbox，让方向键能被它接住。
 *
 * 必须在 listbox 已展示之后运行。隐藏元素拿不到焦点，而且失败是静默的：焦点留在
 * trigger 上，每个按键都变成一条 DOM 事件发往服务器，高亮纹丝不动。
 *
 * @param {string} contentId - select 内容元素的 id
 */
export function focusListbox(contentId) {
    const container = document.getElementById(contentId);
    if (!container) return;

    container.focus({ preventScroll: true });

    // 焦点没落住的话，稍作延迟再试一次
    if (document.activeElement !== container) {
        setTimeout(() => container.focus({ preventScroll: true }), 10);
    }
}

/**
 * 清理某个 select 内容元素的键盘导航。
 * @param {string} contentId - select 内容元素的 id
 */
export function cleanupKeyboardNavigation(contentId) {
    if (activeHandlers.has(contentId)) {
        activeHandlers.get(contentId).dispose();
    }
}

/**
 * 聚焦 select 内容元素。
 * @param {string} contentId - select 内容元素的 id
 */
export function focusContent(contentId) {
    const contentElement = document.getElementById(contentId);
    if (contentElement) {
        contentElement.focus({ preventScroll: true });
    }
}

/**
 * 把某个项滚进其滚动容器（而非页面）的可视区。
 * @param {string} itemId - 项元素的 id
 * @param {boolean} instant - 是否瞬时滚动（不带动画）——已无用，为兼容 API 保留
 * @param {boolean} center - 是否把项居中到容器
 */
export function scrollItemIntoView(itemId, instant = false, center = true) {
    const itemElement = document.getElementById(itemId);
    if (!itemElement) return;

    // 找滚动容器：listbox 本身，或它的可滚动父级
    const container = itemElement.closest('[role="listbox"]') || itemElement.parentElement;
    if (container) {
        scrollIntoContainerView(itemElement, container, center);
    }
}

/**
 * 聚焦元素并阻止页面滚动。
 * @param {HTMLElement} element - 要聚焦的元素
 */
export function focusElementWithPreventScroll(element) {
    if (element) {
        setTimeout(() => {
            element.focus({ preventScroll: true });
        }, 10);
    }
}

/**
 * 一次调用完成打开 listbox 前的全部准备。
 *
 * 「把选中项滚进可视区」和「挂键盘处理器」过去是 C# 端两次分别 await 的调用，
 * 而在 Blazor Server 上每一次都是一个 circuit 往返——每次打开都要付两遍，
 * 用户还正等着列表可用。
 *
 * 滚动依然排在最前，而且是严格的「先」：它在这趟任务里最先执行，赶在展示浮层的
 * 那个 requestAnimationFrame 之前。列表一出现就已滚动到选中项，正是这个顺序的全部意义。
 *
 * @param {string} contentId - listbox 元素 id。
 * @param {string|null} selectedValue - 当前选中值，可为空。
 * @param {boolean} attachKeyboard - 是否还需要挂键盘处理器。
 * @param {Object|null} dotNetRef - 键盘处理器的回调目标。
 */
export function openListbox(contentId, selectedValue, attachKeyboard, dotNetRef) {
    focusInitialOption(contentId, selectedValue);

    if (attachKeyboard && dotNetRef) {
        // autoFocus 关掉：此刻 listbox 还停在停放点上、并不可见。
        // 由 overlay.open 在展示它之后再负责聚焦。
        setupKeyboardNavigation(contentId, dotNetRef, false);
    }
}

/**
 * 高亮初始选中项；没有选中项时高亮第一项。
 * @param {string} contentId - select 内容元素的 id
 * @param {string} selectedValue - 当前选中值（可选）
 */
export function focusInitialOption(contentId, selectedValue) {
    const container = document.getElementById(contentId);
    if (!container) return;

    const items = getEnabledOptions(container);
    if (items.length === 0) return;

    // 先按 aria-selected 属性找当前选中项
    let targetIndex = 0;
    const selectedIndex = items.findIndex(item =>
        item.getAttribute('aria-selected') === 'true'
    );
    if (selectedIndex >= 0) {
        targetIndex = selectedIndex;
    }

    // center=true：让选中项周围留出上下文
    setFocusedOption(container, targetIndex, true);
}
