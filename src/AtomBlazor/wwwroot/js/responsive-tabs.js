// 响应式标签页的溢出检测
// 使用 ResizeObserver 检测标签按钮是否溢出其容器

const states = new Map();

/**
 * 为一个响应式标签容器初始化溢出检测。
 * @param {DotNetObject} dotNetRef - 指向 Blazor 组件的引用
 * @param {string} componentId - 本实例的唯一标识
 * @param {HTMLElement} containerElement - 包含 tablist 的外层元素
 */
export function initialize(dotNetRef, componentId, containerElement) {
  if (!dotNetRef || !containerElement) {
    return;
  }

  const tablist = containerElement.querySelector('[role="tablist"]');
  if (!tablist) {
    return;
  }

  // 在任何隐藏发生之前，一次性测出所有标签的自然宽度。
  // 这避免了隐藏标签会改变 scrollWidth 的反馈回路。
  let tabsNaturalWidth = tablist.scrollWidth;
  let lastOverflowing = null;

  const check = () => {
    const availableWidth = containerElement.clientWidth;
    const isOverflowing = tabsNaturalWidth > availableWidth;
    if (isOverflowing !== lastOverflowing) {
      lastOverflowing = isOverflowing;
      dotNetRef.invokeMethodAsync('OnOverflowChange', isOverflowing).catch(() => {});
    }
  };

  const observer = new ResizeObserver(() => check());
  observer.observe(containerElement);

  states.set(componentId, { observer, dotNetRef, tablist, remeasure });

  // 初始检查
  check();

  /**
   * 重新测量标签的自然宽度。在标签增删时调用。
   */
  function remeasure() {
    tabsNaturalWidth = tablist.scrollWidth;
    lastOverflowing = null;
    check();
  }
}

/**
 * 触发一次标签自然宽度的重测。
 * @param {string} componentId - 实例的唯一标识
 */
export function remeasure(componentId) {
  const state = states.get(componentId);
  if (state) {
    state.remeasure();
  }
}

/**
 * 释放某个响应式标签实例的溢出检测。
 * @param {string} componentId - 实例的唯一标识
 */
export function dispose(componentId) {
  const state = states.get(componentId);
  if (state) {
    state.observer.disconnect();
    states.delete(componentId);
  }
}
