/**
 * 树视图键盘导航
 * 处理方向键导航、展开/折叠、选中与复选框切换。
 */

const instances = new Map();

/**
 * 获取容器内按 DOM 顺序排列的全部可见树条目（role="treeitem"）。
 * 只返回实际可见的条目（不在折叠的分组合里面）。
 * @param {HTMLElement} container
 * @returns {HTMLElement[]}
 */
function getVisibleTreeItems(container) {
  if (!container) return [];
  const items = Array.from(container.querySelectorAll('[role="treeitem"]'));
  return items.filter(item => {
    // 检查全部祖先分组都可见（父级 treeitem 处于展开状态）
    let el = item;
    while (el && el !== container) {
      if (el.hidden) return false;
      if (el.getAttribute('role') === 'group') {
        const parentItem = el.parentElement;
        if (parentItem && parentItem.getAttribute('role') === 'treeitem') {
          if (parentItem.getAttribute('aria-expanded') !== 'true') {
            return false;
          }
        }
      }
      el = el.parentElement;
    }
    return true;
  });
}

/**
 * 从树条目元素上取 data-value 特性。
 * @param {HTMLElement} el
 * @returns {string|null}
 */
function getNodeValue(el) {
  return el ? el.getAttribute('data-value') : null;
}

/**
 * 检查树条目是否禁用。
 * @param {HTMLElement} el
 * @returns {boolean}
 */
function isDisabled(el) {
  return el && el.getAttribute('aria-disabled') === 'true';
}

/**
 * 检查事件目标是否是应自行处理事件的交互元素。
 * @param {Event} e
 * @param {HTMLElement} treeItem - 最近的 treeitem 祖先
 * @returns {boolean}
 */
function isFromInteractiveElement(e, treeItem) {
  if (!(e.target instanceof Element)) return false;
  const interactiveSelectors = 'button:not([data-tree-toggle]):not([data-tree-checkbox]), a, input, textarea, select, [contenteditable="true"]';
  const interactive = e.target.closest(interactiveSelectors);
  // 只有当它位于 treeitem 内部而又不是 treeitem 本身时才视为交互元素
  return interactive != null && interactive !== treeItem && treeItem.contains(interactive);
}

/**
 * 从事件目标向上找最近的 treeitem 元素。
 * @param {Event} e
 * @returns {HTMLElement|null}
 */
function getTreeItemFromEvent(e) {
  const target = e.target instanceof Element ? e.target : e.target.parentElement;
  return target ? target.closest('[role="treeitem"]') : null;
}

/**
 * 更新 roving tabindex：目标条目置 tabindex="0"，
 * 树内其余全部可见条目置 tabindex="-1"。
 * @param {HTMLElement} container
 * @param {HTMLElement} targetItem
 */
function updateRovingTabindex(container, targetItem) {
  const items = container.querySelectorAll('[role="treeitem"]');
  for (const item of items) {
    item.setAttribute('tabindex', item === targetItem ? '0' : '-1');
  }
}

/**
 * 为一个树视图实例初始化键盘导航。
 * @param {HTMLElement} containerElement
 * @param {object} dotNetRef - 供 C# 回调的 DotNetObjectReference
 * @param {string} instanceId - 唯一实例 ID
 */
export function initialize(containerElement, dotNetRef, instanceId) {
  if (!containerElement || !dotNetRef) return;

  const state = { containerElement, dotNetRef, instanceId };

  const focusTreeItem = (item) => {
    if (!item) return;
    updateRovingTabindex(containerElement, item);
    item.focus({ preventScroll: false });
  };

  const handleKeyDown = (e) => {
    const currentItem = getTreeItemFromEvent(e);
    if (!currentItem) return;

    // 不拦截来自交互后代（按钮、链接、输入框等）的事件
    if (isFromInteractiveElement(e, currentItem)) return;

    const value = getNodeValue(currentItem);
    if (!value) return;

    const items = getVisibleTreeItems(containerElement);
    const currentIndex = items.indexOf(currentItem);

    // 展开方向跟随阅读方向：在从右到左的树里，ArrowLeft 负责展开节点、
    // ArrowRight 负责收起。
    const rtl = getComputedStyle(containerElement).direction === 'rtl';
    const key = rtl && (e.key === 'ArrowLeft' || e.key === 'ArrowRight')
      ? (e.key === 'ArrowLeft' ? 'ArrowRight' : 'ArrowLeft')
      : e.key;

    switch (key) {
      case 'ArrowDown': {
        e.preventDefault();
        // 焦点移到下一个可见节点
        for (let i = currentIndex + 1; i < items.length; i++) {
          if (!isDisabled(items[i])) {
            focusTreeItem(items[i]);
            break;
          }
        }
        break;
      }

      case 'ArrowUp': {
        e.preventDefault();
        // 焦点移到上一个可见节点
        for (let i = currentIndex - 1; i >= 0; i--) {
          if (!isDisabled(items[i])) {
            focusTreeItem(items[i]);
            break;
          }
        }
        break;
      }

      case 'ArrowRight': {
        e.preventDefault();
        const isExpanded = currentItem.getAttribute('aria-expanded') === 'true';
        const hasChildren = currentItem.getAttribute('data-has-children') === 'true';

        if (hasChildren && !isExpanded) {
          // 展开节点
          dotNetRef.invokeMethodAsync('JsOnNodeExpand', value).catch(() => {});
        } else if (hasChildren && isExpanded) {
          // 移到第一个子节点
          const group = currentItem.querySelector('[role="group"]');
          if (group) {
            const firstChild = group.querySelector('[role="treeitem"]');
            if (firstChild && !isDisabled(firstChild)) {
              focusTreeItem(firstChild);
            }
          }
        }
        break;
      }

      case 'ArrowLeft': {
        e.preventDefault();
        const isExpanded = currentItem.getAttribute('aria-expanded') === 'true';

        if (isExpanded) {
          // 折叠节点
          dotNetRef.invokeMethodAsync('JsOnNodeCollapse', value).catch(() => {});
        } else {
          // 移到父节点
          const group = currentItem.parentElement;
          if (group && group.getAttribute('role') === 'group') {
            const parentItem = group.parentElement;
            if (parentItem && parentItem.getAttribute('role') === 'treeitem' && !isDisabled(parentItem)) {
              focusTreeItem(parentItem);
            }
          }
        }
        break;
      }

      case 'Enter': {
        e.preventDefault();
        if (!isDisabled(currentItem)) {
          const isPicker = containerElement.getAttribute('data-tree-select') === 'true';
          const isCheckable = currentItem.getAttribute('aria-checked') !== null;
          if (isPicker && isCheckable) {
            dotNetRef.invokeMethodAsync('JsOnNodeCheck', value).catch(() => {});
          } else {
            const hasChildren = currentItem.getAttribute('data-has-children') === 'true';
            dotNetRef.invokeMethodAsync('JsOnNodeActivate', value, hasChildren).catch(() => {});
          }
        }
        break;
      }

      case ' ': {
        e.preventDefault();
        if (!isDisabled(currentItem)) {
          // 在 TreeSelect 中，空格只导航分支而不改变值。
          // 提交选中（或切换复选框）由 Enter 负责。
          if (containerElement.getAttribute('data-tree-select') === 'true') {
            if (currentItem.getAttribute('data-has-children') === 'true') {
              const action = currentItem.getAttribute('aria-expanded') === 'true'
                ? 'JsOnNodeCollapse' : 'JsOnNodeExpand';
              dotNetRef.invokeMethodAsync(action, value).catch(() => {});
            }
            break;
          }
          // 可勾选时切换复选框；否则激活/选中
          const isCheckable = currentItem.getAttribute('aria-checked') !== null;
          if (isCheckable) {
            dotNetRef.invokeMethodAsync('JsOnNodeCheck', value).catch(() => {});
          } else {
            const hasChildren = currentItem.getAttribute('data-has-children') === 'true';
            dotNetRef.invokeMethodAsync('JsOnNodeActivate', value, hasChildren).catch(() => {});
          }
        }
        break;
      }

      case 'Home': {
        e.preventDefault();
        // 焦点移到第一个可见节点
        for (let i = 0; i < items.length; i++) {
          if (!isDisabled(items[i])) {
            focusTreeItem(items[i]);
            break;
          }
        }
        break;
      }

      case 'End': {
        e.preventDefault();
        // 焦点移到最后一个可见节点
        for (let i = items.length - 1; i >= 0; i--) {
          if (!isDisabled(items[i])) {
            focusTreeItem(items[i]);
            break;
          }
        }
        break;
      }

      case '*': {
        e.preventDefault();
        // 展开焦点节点的全部兄弟节点
        dotNetRef.invokeMethodAsync('JsOnExpandSiblings', value).catch(() => {});
        break;
      }

      default:
        return; // 未处理的按键不阻止冒泡
    }
  };

  // 处理点击以选中/切换
  const handleClick = (e) => {
    const treeItem = getTreeItemFromEvent(e);
    if (!treeItem || isDisabled(treeItem)) return;

    const value = getNodeValue(treeItem);
    if (!value) return;

    // 为 closest() 调用把目标归一化为 Element
    const clickTarget = e.target instanceof Element ? e.target : (e.target.parentElement || null);

    // 检查点击是否落在展开开关区域
    const toggle = clickTarget ? clickTarget.closest('[data-tree-toggle]') : null;
    if (toggle) {
      const isExpanded = treeItem.getAttribute('aria-expanded') === 'true';
      if (isExpanded) {
        dotNetRef.invokeMethodAsync('JsOnNodeCollapse', value).catch(() => {});
      } else {
        dotNetRef.invokeMethodAsync('JsOnNodeExpand', value).catch(() => {});
      }
      return;
    }

    // 检查点击是否落在复选框上
    const checkbox = clickTarget ? clickTarget.closest('[data-tree-checkbox]') : null;
    if (checkbox) {
      dotNetRef.invokeMethodAsync('JsOnNodeCheck', value).catch(() => {});
      return;
    }

    // 不从交互元素（按钮、链接、输入框等）手里抢焦点
    if (isFromInteractiveElement(e, treeItem)) return;

    // 否则选中节点
    const hasChildren = treeItem.getAttribute('data-has-children') === 'true';
    dotNetRef.invokeMethodAsync('JsOnNodeActivate', value, hasChildren).catch(() => {});

    // 确保被点击的条目获得焦点并更新 roving tabindex
    focusTreeItem(treeItem);
  };

  containerElement.addEventListener('keydown', handleKeyDown);
  containerElement.addEventListener('click', handleClick);

  state.handleKeyDown = handleKeyDown;
  state.handleClick = handleClick;
  // 级联勾选的树会为选择关系保留被过滤的节点。
  // 当过滤把上一个可 Tab 的根节点藏起来时，保持一个可见的落点。
  const syncTabStop = () => {
    const visible = getVisibleTreeItems(containerElement).filter(item => !isDisabled(item));
    if (visible.length && !visible.some(item => item.getAttribute('tabindex') === '0')) {
      updateRovingTabindex(containerElement, visible[0]);
    }
  };
  state.observer = new MutationObserver(syncTabStop);
  state.observer.observe(containerElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['hidden', 'aria-expanded'] });
  syncTabStop();
  instances.set(instanceId, state);
}

/**
 * 释放一个树视图实例的键盘导航。
 * @param {string} instanceId
 */
export function dispose(instanceId) {
  const state = instances.get(instanceId);
  if (!state) return;

  state.observer?.disconnect();

  if (state.containerElement) {
    state.containerElement.removeEventListener('keydown', state.handleKeyDown);
    state.containerElement.removeEventListener('click', state.handleClick);
  }

  instances.delete(instanceId);
}
