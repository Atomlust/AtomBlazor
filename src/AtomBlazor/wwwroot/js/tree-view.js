/**
 * 树视图拖放处理器
 * 使用指针事件实现跨浏览器拖拽交互。
 */

const instances = new Map();

/**
 * 按元素 ID 为树视图初始化拖放。
 * @param {string} elementId - 树容器元素的 ID
 * @param {object} dotNetRef - 用于 C# 回调的 DotNetObjectReference
 * @param {string} instanceId - 唯一实例 ID
 */
export function initializeDragDropById(elementId, dotNetRef, instanceId) {
  const containerElement = document.getElementById(elementId);
  initializeDragDrop(containerElement, dotNetRef, instanceId);
}

/**
 * 为树视图初始化拖放。
 * @param {HTMLElement} containerElement - 树容器元素
 * @param {object} dotNetRef - 用于 C# 回调的 DotNetObjectReference
 * @param {string} instanceId - 唯一实例 ID
 */
export function initializeDragDrop(containerElement, dotNetRef, instanceId) {
  if (!containerElement || !dotNetRef) return;

  const DRAG_THRESHOLD = 5; // 拖拽开始前需要移动的像素数

  const state = {
    containerElement,
    dotNetRef,
    isDragging: false,
    pendingDrag: false,
    startX: 0,
    startY: 0,
    sourceElement: null,
    sourceValue: null,
    indicator: null,
    ghostElement: null,
    pointerId: null
  };

  const getTreeItemFromPoint = (x, y) => {
    const elements = document.elementsFromPoint(x, y);
    for (const el of elements) {
      const item = el.closest('[role="treeitem"]');
      if (item && containerElement.contains(item) && item !== state.sourceElement) {
        return item;
      }
    }
    return null;
  };

  const getDropPosition = (targetEl, clientY) => {
    const rect = targetEl.querySelector('[data-tree-node]')?.getBoundingClientRect() || targetEl.getBoundingClientRect();
    const relativeY = clientY - rect.top;
    const height = rect.height;

    if (relativeY < height * 0.25) return 'before';
    if (relativeY > height * 0.75) return 'after';
    return 'inside';
  };

  const isDescendantOf = (childEl, parentEl) => {
    if (!childEl || !parentEl) return false;
    let el = childEl.parentElement;
    while (el) {
      if (el === parentEl) return true;
      el = el.parentElement;
    }
    return false;
  };

  const createIndicator = () => {
    const indicator = document.createElement('div');
    indicator.className = 'atom-tree-drop-indicator';
    indicator.style.cssText = 'position:absolute;left:0;right:0;height:2px;background:hsl(var(--primary));pointer-events:none;z-index:50;display:none;';
    // 仅当计算位置为 static 且不存在内联覆盖时才设置定位
    const computedPosition = window.getComputedStyle(containerElement).position;
    if (computedPosition === 'static' && !containerElement.style.position) {
      containerElement.style.position = 'relative';
    }
    containerElement.appendChild(indicator);
    return indicator;
  };

  const updateIndicator = (targetEl, position) => {
    if (!state.indicator) {
      state.indicator = createIndicator();
    }

    // 清除上一个目标上残留的拖放目标类
    containerElement.querySelectorAll('.atom-tree-drop-target').forEach(el => {
      el.classList.remove('atom-tree-drop-target');
    });

    const nodeEl = targetEl.querySelector('[data-tree-node]') || targetEl;
    const containerRect = containerElement.getBoundingClientRect();
    const targetRect = nodeEl.getBoundingClientRect();

    state.indicator.style.display = 'block';
    state.indicator.style.left = `${targetRect.left - containerRect.left}px`;
    state.indicator.style.width = `${targetRect.width}px`;

    if (position === 'before') {
      state.indicator.style.top = `${targetRect.top - containerRect.top}px`;
      state.indicator.style.height = '2px';
    } else if (position === 'after') {
      state.indicator.style.top = `${targetRect.bottom - containerRect.top}px`;
      state.indicator.style.height = '2px';
    } else {
      state.indicator.style.display = 'none';
      targetEl.classList.add('atom-tree-drop-target');
    }
  };

  const clearIndicator = () => {
    if (state.indicator) {
      state.indicator.style.display = 'none';
    }
    containerElement.querySelectorAll('.atom-tree-drop-target').forEach(el => {
      el.classList.remove('atom-tree-drop-target');
    });
  };

  const startDrag = () => {
    state.pendingDrag = false;
    state.isDragging = true;

    // 在容器上设置指针捕获以获得平滑拖拽
    containerElement.setPointerCapture(state.pointerId);

    // 视觉反馈：降低源元素的不透明度
    state.sourceElement.style.opacity = '0.5';

    document.body.style.userSelect = 'none';
    document.body.style.cursor = 'grabbing';
  };

  const handlePointerDown = (e) => {
    const draggableNode = e.target.closest('[data-draggable="true"]');
    if (!draggableNode) return;

    const treeItem = draggableNode.closest('[role="treeitem"]');
    if (!treeItem) return;

    // 点击展开切换或复选框时不开始拖拽
    if (e.target.closest('[data-tree-toggle]') || e.target.closest('[data-tree-checkbox]')) return;

    // 记录待处理的拖拽——实际拖拽在移动超过阈值后才开始
    state.pendingDrag = true;
    state.startX = e.clientX;
    state.startY = e.clientY;
    state.sourceElement = treeItem;
    state.sourceValue = treeItem.getAttribute('data-value');
    state.pointerId = e.pointerId;
  };

  const handlePointerMove = (e) => {
    if (!state.isDragging && !state.pendingDrag) return;

    // 开始实际拖拽前先检查阈值
    if (state.pendingDrag && !state.isDragging) {
      const dx = e.clientX - state.startX;
      const dy = e.clientY - state.startY;
      if (Math.abs(dx) < DRAG_THRESHOLD && Math.abs(dy) < DRAG_THRESHOLD) return;
      startDrag();
    }

    const targetItem = getTreeItemFromPoint(e.clientX, e.clientY);
    if (!targetItem) {
      clearIndicator();
      return;
    }

    // 禁止拖放到自身的后代节点上
    if (isDescendantOf(targetItem, state.sourceElement)) {
      clearIndicator();
      return;
    }

    const position = getDropPosition(targetItem, e.clientY);
    updateIndicator(targetItem, position);
  };

  const resetState = () => {
    if (state.sourceElement) {
      state.sourceElement.style.opacity = '';
    }
    clearIndicator();
    document.body.style.userSelect = '';
    document.body.style.cursor = '';

    if (state.isDragging && state.pointerId !== null) {
      try {
        containerElement.releasePointerCapture(state.pointerId);
      } catch {
        // 指针捕获可能已经释放
      }
    }

    state.isDragging = false;
    state.pendingDrag = false;
    state.sourceElement = null;
    state.sourceValue = null;
    state.pointerId = null;
  };

  const handlePointerUp = (e) => {
    if (!state.isDragging && !state.pendingDrag) return;

    // 如果拖拽从未真正开始（未超过阈值），直接重置——让点击事件正常触发
    if (!state.isDragging) {
      resetState();
      return;
    }

    const targetItem = getTreeItemFromPoint(e.clientX, e.clientY);

    if (targetItem && !isDescendantOf(targetItem, state.sourceElement)) {
      const targetValue = targetItem.getAttribute('data-value');
      const position = getDropPosition(targetItem, e.clientY);

      if (state.sourceValue && targetValue) {
        dotNetRef.invokeMethodAsync('JsOnNodeDrop', state.sourceValue, targetValue, position);
      }
    }

    resetState();
  };

  const handlePointerCancel = () => {
    resetState();
  };

  containerElement.addEventListener('pointerdown', handlePointerDown);
  containerElement.addEventListener('pointermove', handlePointerMove);
  containerElement.addEventListener('pointerup', handlePointerUp);
  containerElement.addEventListener('pointercancel', handlePointerCancel);

  state.handlePointerDown = handlePointerDown;
  state.handlePointerMove = handlePointerMove;
  state.handlePointerUp = handlePointerUp;
  state.handlePointerCancel = handlePointerCancel;

  instances.set(instanceId, state);
}

/**
 * 释放某个树视图实例的拖放。
 * @param {string} instanceId
 */
export function disposeDragDrop(instanceId) {
  const state = instances.get(instanceId);
  if (!state) return;

  if (state.containerElement) {
    state.containerElement.removeEventListener('pointerdown', state.handlePointerDown);
    state.containerElement.removeEventListener('pointermove', state.handlePointerMove);
    state.containerElement.removeEventListener('pointerup', state.handlePointerUp);
    state.containerElement.removeEventListener('pointercancel', state.handlePointerCancel);
  }

  if (state.indicator && state.indicator.parentElement) {
    state.indicator.parentElement.removeChild(state.indicator);
  }

  instances.delete(instanceId);
}
