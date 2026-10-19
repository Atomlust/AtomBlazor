/**
 * Command 组件的虚拟化滚动工具。
 * 为虚拟化列表提供滚动相关函数。
 */

/**
 * 滚动到虚拟化分组中的指定索引。
 * 计算滚动位置时会考虑容器偏移、标题高度与项索引。
 * @param {string} listId - 可滚动列表容器的 ID
 * @param {string} containerId - 分组容器的 ID
 * @param {number} index - 要滚动到的项索引
 * @param {number} headingHeight - 分组标题高度（像素，默认 32）
 * @param {number} itemHeight - 每个项的高度（像素，默认 32）
 */
export function scrollToVirtualizedIndex(listId, containerId, index, headingHeight = 32, itemHeight = 32) {
    const list = document.getElementById(listId);
    const container = document.getElementById(containerId);
    if (!list || !container) return;

    // 取得容器相对列表的位置
    const containerTop = container.offsetTop - list.offsetTop;
    let targetScroll;

    if (index === 0) {
        // 首项：滚动到可显示分组标题的位置
        targetScroll = containerTop;
    } else {
        // 其余项：滚动到可显示该项的位置
        targetScroll = containerTop + headingHeight + (index * itemHeight);
    }

    // 钳到有效滚动范围
    const maxScroll = list.scrollHeight - list.clientHeight;
    list.scrollTop = Math.max(0, Math.min(targetScroll, maxScroll));
}

/**
 * 以可配置的块对齐方式把元素滚入视图。
 * @param {string} elementId - 要滚入视图的元素 ID
 * @param {string} scrollBlock - 块对齐方式（'start'、'nearest'、'center'、'end'）
 */
export function scrollElementIntoView(elementId, scrollBlock = 'nearest') {
    const element = document.getElementById(elementId);
    if (element) {
        element.scrollIntoView({
            block: scrollBlock,
            behavior: 'instant'
        });
    }
}
