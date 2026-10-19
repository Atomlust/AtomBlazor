/**
 * 让内容宽度与触发器宽度保持一致
 * @param {HTMLElement} triggerElement - 触发器元素
 * @param {HTMLElement} contentElement - 待调整宽度的内容元素
 * @returns {Object} 清理函数
 */
export function matchTriggerWidth(triggerElement, contentElement) {
    if (!triggerElement || !contentElement) {
        console.warn('matchTriggerWidth: Missing required elements');
        return { dispose: () => {} };
    }

    function updateWidth() {
        const triggerRect = triggerElement.getBoundingClientRect();
        // 用 setProperty 以 !important 设置宽度，覆写任何基于类的宽度
        contentElement.style.setProperty('width', triggerRect.width + 'px', 'important');
    }

    // 立即应用一次宽度
    updateWidth();

    // 窗口尺寸变化时更新
    const resizeObserver = new ResizeObserver(() => {
        updateWidth();
    });

    resizeObserver.observe(triggerElement);

    return {
        dispose: () => {
            resizeObserver.disconnect();
        }
    };
}
