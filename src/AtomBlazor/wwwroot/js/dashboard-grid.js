// 仪表盘网格（Dashboard Grid）——拖拽、缩放与响应式 breakpoint 处理器。
// 使用带 pointer capture 的指针事件，实现平滑的拖拽/缩放交互。

const instances = new Map();
const DRAG_THRESHOLD = 5;

// 当 JS->.NET 回调因 .NET 侧已消失而失败时返回 true——即网格的
// DotNetObjectReference 已释放或 circuit（电路）已断。这属于与释放
// 相竞态的过期回调，而非值得上报的失败。
function isDisposedReferenceError(err) {
  const msg = (err && (err.message || err.toString())) || '';
  return msg.includes('no tracked object')
    || msg.includes('DotNetObjectReference')
    || msg.includes('JSDisconnectedException');
}

// 调用 .NET 侧网格回调。过期回调的拒绝（引用已释放或 circuit 已断）
// 被静默丢弃；其余拒绝一律记录日志，使真正的 JS->.NET 失败
// 保持可见。
function invokeSafely(state, method, ...args) {
  return state.dotNetRef.invokeMethodAsync(method, ...args)
    .catch(err => {
      if (isDisposedReferenceError(err)) return;
      console.error(`${method} failed:`, err);
    });
}

// 观察器可能比网格活得更久——resize 观察器监视的是 document.body
// 而非网格元素，导航之后仍会持续触发。
// 网格元素一旦离开 DOM，该实例即成孤儿：应将其释放，
// 令其观察器停下、不再触发过期的 .NET 回调。
function disposeIfOrphaned(state) {
  if (state.gridEl && state.gridEl.isConnected) return false;
  disposeDashboardGrid(state.instanceId);
  return true;
}

export function initializeDashboardGrid(dotNetRef, instanceId, options) {
  if (!dotNetRef) return;

  // 若在重新初始化，先释放既有实例
  if (instances.has(instanceId)) disposeDashboardGrid(instanceId);

  const gridEl = document.querySelector(`[data-dashboard-id="${instanceId}"]`);
  if (!gridEl) return;

  const state = {
    dotNetRef,
    instanceId,
    options,
    gridEl,
    isDragging: false,
    isResizing: false,
    pendingDrag: false,
    pendingResize: false,
    startX: 0,
    startY: 0,
    pointerId: null,
    activeWidgetId: null,
    activeHandle: null,
    ghostElement: null,
    placeholderElement: null,
    originalWidget: null,
    startCol: 0,
    startRow: 0,
    startColSpan: 0,
    startRowSpan: 0,
  };

  // 保留配置好的拖拽/缩放取值，供 setEditable 恢复时使用
  state.configuredAllowDrag = options.allowDrag;
  state.configuredAllowResize = options.allowResize;

  instances.set(instanceId, state);
  setupResizeObserver(instanceId, state);
  setupEventListeners(instanceId, state);
  setupMutationObserver(state);
  syncGridGuide(state);

  // 执行初次压紧（compact）并显示控件
  runCompactAndReveal(state);
}

export function updateGridOptions(instanceId, options) {
  const state = instances.get(instanceId);
  if (!state) return;
  state.configuredAllowDrag = options.allowDrag;
  state.configuredAllowResize = options.allowResize;
  state.options = options;
  syncGridGuide(state);
}

export function updateWidgetPositions(instanceId, positions) {
  // .NET 状态恢复之后，同步 DOM 内联样式
  if (!positions) return;
  const state = instances.get(instanceId);
  const container = state ? state.gridEl : null;
  for (const pos of positions) {
    const widget = container
      ? container.querySelector(`[data-widget-id="${pos.widgetId}"]`)
      : document.querySelector(`[data-widget-id="${pos.widgetId}"]`);
    if (widget) {
      widget.style.gridColumn = `${pos.column} / span ${pos.columnSpan}`;
      widget.style.gridRow = `${pos.row} / span ${pos.rowSpan}`;
    }
  }
}

export function requestCompact(instanceId) {
  const state = instances.get(instanceId);
  if (!state || state.isDragging || state.isResizing) return;
  runCompactAndReveal(state);
}

export function setEditable(instanceId, editable) {
  const state = instances.get(instanceId);
  if (!state) return;
  state.options.editable = editable;
  // 依据最初配置的取值（初始化/更新时传入）重新计算，
  // 使 editable 关掉再打开后能恢复预期行为。
  state.options.allowDrag = editable && (state.configuredAllowDrag ?? state.options.allowDrag);
  state.options.allowResize = editable && (state.configuredAllowResize ?? state.options.allowResize);
}

export function disposeDashboardGrid(instanceId) {
  const state = instances.get(instanceId);
  if (!state) return;

  if (state.resizeObserver) {
    state.resizeObserver.disconnect();
  }
  if (state.mutationObserver) {
    state.mutationObserver.disconnect();
  }

  cleanupListeners(state);
  instances.delete(instanceId);
}

// --- 用于控件增删的 MutationObserver ---

function setupMutationObserver(state) {
  state.mutationObserver = new MutationObserver((mutations) => {
    if (disposeIfOrphaned(state)) return;
    if (state.isDragging || state.isResizing) return;

    // 仅当控件元素被添加或移除时才响应
    let widgetChanged = false;
    for (const mutation of mutations) {
      for (const node of mutation.addedNodes) {
        if (node.nodeType === 1 && node.hasAttribute('data-widget-id')) {
          widgetChanged = true;
          break;
        }
      }
      if (widgetChanged) break;
      for (const node of mutation.removedNodes) {
        if (node.nodeType === 1 && node.hasAttribute('data-widget-id')) {
          widgetChanged = true;
          break;
        }
      }
      if (widgetChanged) break;
    }

    if (widgetChanged) {
      runCompactAndReveal(state);
    }
  });

  state.mutationObserver.observe(state.gridEl, { childList: true });
}

function getActiveColumns(state) {
  return getColumnsForBreakpoint(state, state.currentBreakpoint || 'large');
}

function runCompactAndReveal(state) {
  const grid = state.gridEl;
  const positions = getWidgetPositions(grid, null);

  if (positions.length === 0) {
    grid.setAttribute('data-positioned', 'true');
    return;
  }

  const columns = getActiveColumns(state);

  // 将列跨度夹取到当前列数以内
  for (const pos of positions) {
    if (pos.colSpan > columns) {
      pos.colSpan = columns;
    }
    if (pos.col + pos.colSpan - 1 > columns) {
      pos.col = Math.max(1, columns - pos.colSpan + 1);
    }
  }

  if (state.options.compact) {
    compact(positions, null, columns);
    applyLayout(grid, positions);
  }

  grid.setAttribute('data-positioned', 'true');

  // 把压紧后的位置回传给 .NET 状态
  const dtos = positions.map(p => ({
    widgetId: p.id,
    col: p.col,
    row: p.row,
    colSpan: p.colSpan,
    rowSpan: p.rowSpan
  }));
  invokeSafely(state, 'JsOnCompactComplete', dtos);
}

// --- 用于响应式 breakpoint 的 ResizeObserver ---

function getColumnsForBreakpoint(state, bp) {
  if (bp === 'small') return state.options.smallColumns;
  if (bp === 'medium') return state.options.mediumColumns;
  return state.options.columns;
}

// 测量网格实际渲染出的尺寸，为背景方格生成严丝合缝的 SVG 遮罩。
// 在初始化与尺寸变化时调用。
function syncGridGuide(state) {
  const el = state.gridEl;
  if (!el) return;

  const cs = getComputedStyle(el);

  // 同步 padding，使 ::before 伪元素恰好覆盖内容区
  el.style.setProperty('--atom-grid-pad-top', cs.paddingTop);
  el.style.setProperty('--atom-grid-pad-right', cs.paddingRight);
  el.style.setProperty('--atom-grid-pad-bottom', cs.paddingBottom);
  el.style.setProperty('--atom-grid-pad-left', cs.paddingLeft);

  // 读取浏览器实际算出的列宽——避免 JS 计算产生漂移
  const tracks = cs.gridTemplateColumns.split(/\s+/);
  const cellWidth = parseFloat(tracks[0]) || 0;
  const gap = state.options.gap;
  const rowHeight = state.options.rowHeight;

  const tileW = cellWidth + gap;
  const tileH = rowHeight + gap;

  // 生成与实际网格单元尺寸一致的 SVG 瓦片
  const svg = `<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 ${tileW} ${tileH}'>`
    + `<rect x='1' y='1' width='${cellWidth - 2}' height='${rowHeight - 2}' rx='3' ry='3' fill='none' stroke='white'/>`
    + `</svg>`;
  const encoded = encodeURIComponent(svg);

  el.style.setProperty('--atom-dashboard-guide-svg', `url("data:image/svg+xml,${encoded}")`);
  el.style.setProperty('--atom-dashboard-cell-w', `${tileW}px`);
  el.style.setProperty('--atom-dashboard-cell-h', `${tileH}px`);
}

function setupResizeObserver(instanceId, state) {
  const checkBreakpoint = () => {
    const width = window.innerWidth;
    let bp = 'large';
    if (width < state.options.smallBreakpoint) {
      bp = 'small';
    } else if (width < state.options.mediumBreakpoint) {
      bp = 'medium';
    }

    if (state.currentBreakpoint !== bp) {
      const isInitial = state.currentBreakpoint === undefined;
      const prevBp = state.currentBreakpoint;
      state.currentBreakpoint = bp;
      invokeSafely(state, 'JsOnBreakpointChanged', bp);

      // 按新列数重新压紧控件（初次检测时跳过）
      if (!isInitial) {
        recompactForBreakpoint(state, prevBp, bp);
      }
    }
  };

  checkBreakpoint();
  state.resizeObserver = new ResizeObserver(() => {
    if (disposeIfOrphaned(state)) return;
    checkBreakpoint();
    syncGridGuide(state);
  });
  state.resizeObserver.observe(document.body);
}

function recompactForBreakpoint(state, prevBp, bp) {
  const grid = state.gridEl;
  if (!grid) return;

  const columns = getColumnsForBreakpoint(state, bp);

  // 以正要离开的 breakpoint 为键保存当前位置
  if (!state.savedPositions) {
    state.savedPositions = {};
  }
  state.savedPositions[prevBp] = getWidgetPositions(grid, null);

  // 若目标 breakpoint 存有位置快照，则将其恢复
  if (state.savedPositions[bp]) {
    const positions = state.savedPositions[bp];
    delete state.savedPositions[bp];

    if (state.options.compact) {
      compact(positions, null, columns);
    }

    applyLayout(grid, positions);
    syncPositionsToNet(state, positions);
    return;
  }

  const positions = getWidgetPositions(grid, null);
  if (positions.length === 0) return;

  // 将列跨度夹取到新的列数以内
  for (const pos of positions) {
    if (pos.colSpan > columns) {
      pos.colSpan = columns;
    }
    if (pos.col + pos.colSpan - 1 > columns) {
      pos.col = Math.max(1, columns - pos.colSpan + 1);
    }
  }

  if (state.options.compact) {
    compact(positions, null, columns);
  }

  applyLayout(grid, positions);
  syncPositionsToNet(state, positions);
}

function syncPositionsToNet(state, positions) {
  const dtos = positions.map(p => ({
    widgetId: p.id,
    col: p.col,
    row: p.row,
    colSpan: p.colSpan,
    rowSpan: p.rowSpan
  }));
  invokeSafely(state, 'JsOnCompactComplete', dtos);
}

// --- 事件监听器 ---

function setupEventListeners(instanceId, state) {
  state.handlePointerDown = (e) => onPointerDown(instanceId, state, e);
  state.handlePointerMove = (e) => onPointerMove(instanceId, state, e);
  state.handlePointerUp = (e) => onPointerUp(instanceId, state, e);
  state.handlePointerCancel = (e) => onPointerCancel(instanceId, state, e);
  state.handleKeyDown = (e) => onKeyDown(instanceId, state, e);
  state.handleFocusOut = (e) => onWidgetFocusOut(instanceId, state, e);

  // *指针（pointer）*拖拽期间按下 Escape 时，网格自身的 keydown 监听器看不到它：指针拖拽
  // 从不把焦点移入网格，按键事件因此被投递给此前聚焦的元素
  // ——通常是 <body>——永远到达不了后代元素上的监听器。文档写明"Escape 可取消
  // 进行中的拖拽或缩放"，而用鼠标操作时它此前根本不生效。
  //
  // 此处刻意收窄：仅响应 Escape，且仅当本网格正是拖拽/缩放方时生效，
  // 页面上的第二个网格便不会去干预不属于自身的交互。
  state.handleDocumentKeyDown = (e) => {
    if (e.key !== 'Escape') return;
    if (!state.isDragging && !state.isResizing) return;
    e.preventDefault();
    cancelInteraction(state);
  };

  state.gridEl.addEventListener('pointerdown', state.handlePointerDown);
  document.addEventListener('pointermove', state.handlePointerMove);
  document.addEventListener('pointerup', state.handlePointerUp);
  document.addEventListener('pointercancel', state.handlePointerCancel);
  state.gridEl.addEventListener('keydown', state.handleKeyDown);
  state.gridEl.addEventListener('focusout', state.handleFocusOut);
  document.addEventListener('keydown', state.handleDocumentKeyDown);
}

function cleanupListeners(state) {
  if (state.gridEl) {
    state.gridEl.removeEventListener('pointerdown', state.handlePointerDown);
    state.gridEl.removeEventListener('keydown', state.handleKeyDown);
    state.gridEl.removeEventListener('focusout', state.handleFocusOut);
  }
  document.removeEventListener('pointermove', state.handlePointerMove);
  document.removeEventListener('pointerup', state.handlePointerUp);
  document.removeEventListener('pointercancel', state.handlePointerCancel);
  document.removeEventListener('keydown', state.handleDocumentKeyDown);
}

// --- 指针按下 ---

function onPointerDown(instanceId, state, e) {
  if (!state.options.editable || e.button !== 0) return;

  // 聚焦被点击的控件，使键盘导航即刻可用
  const clickedWidget = e.target.closest('[data-widget-id]');
  if (clickedWidget) {
    clickedWidget.focus();
  }

  // 优先检查缩放手柄（先于控件拖拽判定）
  const resizeHandle = e.target.closest('[data-dashboard-resize-handle]');
  if (resizeHandle && state.options.allowResize) {
    const widgetId = resizeHandle.getAttribute('data-widget-id');
    const widgetEl = document.querySelector(`[data-widget-id="${widgetId}"]`);
    if (!widgetEl) return;

    e.preventDefault();
    state.pendingResize = true;
    state.activeWidgetId = widgetId;
    state.activeHandle = resizeHandle.getAttribute('data-dashboard-resize-handle');
    state.originalWidget = widgetEl;
    state.startX = e.clientX;
    state.startY = e.clientY;
    state.pointerId = e.pointerId;

    const style = widgetEl.style;
    state.startCol = parseGridValue(style.gridColumn, 'start');
    state.startRow = parseGridValue(style.gridRow, 'start');
    state.startColSpan = parseGridValue(style.gridColumn, 'span');
    state.startRowSpan = parseGridValue(style.gridRow, 'span');
    return;
  }

  // 检查控件拖拽（点击控件任意位置）
  if (state.options.allowDrag) {
    const widgetEl = e.target.closest('[data-widget-id]');
    if (widgetEl && widgetEl.getAttribute('data-static') !== 'true') {
      const widgetId = widgetEl.getAttribute('data-widget-id');

      e.preventDefault();
      state.pendingDrag = true;
      state.activeWidgetId = widgetId;
      state.originalWidget = widgetEl;
      state.startX = e.clientX;
      state.startY = e.clientY;
      state.pointerId = e.pointerId;

      const style = widgetEl.style;
      state.startCol = parseGridValue(style.gridColumn, 'start');
      state.startRow = parseGridValue(style.gridRow, 'start');
      state.startColSpan = parseGridValue(style.gridColumn, 'span');
      state.startRowSpan = parseGridValue(style.gridRow, 'span');
      return;
    }
  }
}

// --- 指针移动 ---

function onPointerMove(instanceId, state, e) {
  if (e.pointerId !== state.pointerId) return;

  const dx = e.clientX - state.startX;
  const dy = e.clientY - state.startY;
  const distance = Math.sqrt(dx * dx + dy * dy);

  // 越过阈值后激活拖拽
  if (state.pendingDrag && !state.isDragging) {
    if (distance >= DRAG_THRESHOLD) {
      activateDrag(state);
    }
    return;
  }

  // 越过阈值后激活缩放
  if (state.pendingResize && !state.isResizing) {
    if (distance >= DRAG_THRESHOLD) {
      activateResize(state);
    }
    return;
  }

  if (state.isDragging) {
    e.preventDefault();
    updateDrag(state, e);
  }

  if (state.isResizing) {
    e.preventDefault();
    updateResize(state, e);
  }
}

// --- 指针抬起 ---

function onPointerUp(instanceId, state, e) {
  if (e.pointerId !== state.pointerId) return;

  if (state.isDragging) {
    finishDrag(state);
  } else if (state.isResizing) {
    finishResize(state);
  }

  resetState(state);
}

function onPointerCancel(instanceId, state, e) {
  if (e.pointerId !== state.pointerId) return;
  cancelInteraction(state);
}

// --- 拖拽逻辑 ---

function activateDrag(state) {
  state.isDragging = true;
  state.pendingDrag = false;
  state.gridEl.setPointerCapture(state.pointerId);

  // 拖拽起始时快照全部控件位置，以便稳定判定交换
  const grid = state.originalWidget.closest('[role="region"][aria-roledescription="dashboard grid"]');
  if (grid) {
    state.dragGrid = grid;
    state.originalPositions = getWidgetPositions(grid, null);
  }

  // 记录抓取点偏移（指针相对控件前导上角的位置）。dir="rtl" 下网格首列画在右侧，
  // 故水平偏移在那里改从控件右缘量起，此后所有累加
  // 都保持在网格坐标空间内进行。
  const widgetRect = state.originalWidget.getBoundingClientRect();
  state.rtl = getComputedStyle(state.originalWidget).direction === 'rtl';
  state.grabOffsetX = state.rtl
    ? widgetRect.right - state.startX
    : state.startX - widgetRect.left;
  state.grabOffsetY = state.startY - widgetRect.top;

  document.body.style.userSelect = 'none';
  document.body.style.cursor = 'grabbing';

  state.originalWidget.setAttribute('data-dragging', 'true');
  state.originalWidget.style.opacity = '0.5';
  state.originalWidget.style.zIndex = '50';
}

function updateDrag(state, e) {
  const grid = state.dragGrid;
  if (!grid) return;

  const gridRect = grid.getBoundingClientRect();
  const cols = getActiveColumns(state);
  const gap = state.options.gap;
  const cellWidth = (gridRect.width - (cols - 1) * gap) / cols;
  const rowHeight = state.options.rowHeight;

  // 由指针位置换算目标行列，并按抓取点偏移作修正
  const relX = state.rtl
    ? gridRect.right - e.clientX - (state.grabOffsetX || 0)
    : e.clientX - gridRect.left - (state.grabOffsetX || 0);
  const relY = e.clientY - gridRect.top - (state.grabOffsetY || 0);

  let targetCol = Math.round(relX / (cellWidth + gap)) + 1;
  let targetRow = Math.round(relY / (rowHeight + gap)) + 1;

  // 夹取到网格边界之内
  targetCol = Math.max(1, Math.min(targetCol, cols - state.startColSpan + 1));
  targetRow = Math.max(1, targetRow);

  // 目标未变则跳过
  if (targetCol === state.lastResolvedCol && targetRow === state.lastResolvedRow) return;

  let resolved = resolveLayoutAfterDrag(
    state.originalPositions, state.activeWidgetId,
    targetCol, targetRow, state.startCol, state.startRow, cols
  );

  // resolve 返回 null 即拒绝该拖拽落点（保留上一个合法布局）
  if (!resolved) {
    state.lastResolvedCol = targetCol;
    state.lastResolvedRow = targetRow;
    return;
  }

  if (state.options.compact) {
    compact(resolved, state.activeWidgetId, cols);
  }
  applyLayout(grid, resolved);
  state.originalWidget.style.opacity = '0.5';
  state.originalWidget.style.zIndex = '50';

  state.resolvedLayout = resolved;
  state.targetCol = targetCol;
  state.targetRow = targetRow;
  state.lastResolvedCol = targetCol;
  state.lastResolvedRow = targetRow;
}

function finishDrag(state) {
  const widgetEl = state.originalWidget;
  widgetEl.style.opacity = '';
  widgetEl.style.zIndex = '';
  widgetEl.setAttribute('data-dragging', 'false');

  document.body.style.userSelect = '';
  document.body.style.cursor = '';

  // 重新聚焦控件，让键盘导航继续可用
  widgetEl.focus();

  if (state.resolvedLayout && state.originalPositions) {
    const finalLayout = state.resolvedLayout;
    const grid = state.dragGrid;

    // 交互结束后执行压紧
    if (state.options.compact && grid) {
      compact(finalLayout, null, getActiveColumns(state));
      applyLayout(grid, finalLayout);
    }

    // 找出所有位置发生改变的控件
    const changes = [];
    for (const resolved of finalLayout) {
      const original = state.originalPositions.find(p => p.id === resolved.id);
      if (!original) continue;
      if (resolved.col !== original.col || resolved.row !== original.row) {
        changes.push({ id: resolved.id, col: resolved.col, row: resolved.row });
      }
    }

    if (changes.length > 0) {
      invokeSafely(state, 'JsOnLayoutResolved', state.activeWidgetId, changes);
      announceChange(state, `Dashboard layout updated`);
    }
  }
}

// --- 缩放逻辑 ---

function activateResize(state) {
  state.isResizing = true;
  state.pendingResize = false;
  state.gridEl.setPointerCapture(state.pointerId);

  // 缩放起始时快照全部控件位置，以便稳定解析
  const grid = state.originalWidget.closest('[role="region"][aria-roledescription="dashboard grid"]');
  if (grid) {
    state.resizeGrid = grid;
    state.originalResizePositions = getWidgetPositions(grid, null);
  }

  document.body.style.userSelect = 'none';
  state.originalWidget.setAttribute('data-resizing', 'true');
}

function updateResize(state, e) {
  const grid = state.resizeGrid;
  if (!grid || !state.originalResizePositions) return;

  const gridRect = grid.getBoundingClientRect();
  const cols = getActiveColumns(state);
  const gap = state.options.gap;
  const cellWidth = (gridRect.width - (cols - 1) * gap) / cols;
  const rowHeight = state.options.rowHeight;

  // 手柄位于控件的逻辑边缘上，向书尾边缘方向拖拽时两个方向的跨度都会增长：
  // 指针位移沿阅读方向读取。
  const rtl = getComputedStyle(state.originalWidget ?? state.dragGrid).direction === 'rtl';
  const dx = (e.clientX - state.startX) * (rtl ? -1 : 1);
  const dy = e.clientY - state.startY;

  const handle = state.activeHandle;
  const resizeRight = handle === 'e' || handle === 'se' || handle === 'ne';
  const resizeLeft = handle === 'w' || handle === 'sw' || handle === 'nw';
  const resizeBottom = handle === 's' || handle === 'se' || handle === 'sw';
  const resizeTop = handle === 'n' || handle === 'ne' || handle === 'nw';

  let newCol = state.startCol;
  let newRow = state.startRow;
  let newColSpan = state.startColSpan;
  let newRowSpan = state.startRowSpan;

  // 右/下：沿拖拽方向增大跨度
  if (resizeRight) {
    newColSpan = Math.round((state.startColSpan * (cellWidth + gap) + dx) / (cellWidth + gap));
  }
  if (resizeBottom) {
    newRowSpan = Math.round((state.startRowSpan * (rowHeight + gap) + dy) / (rowHeight + gap));
  }

  // 左/上：移动位置并反向调整跨度（对侧边缘保持不动）
  if (resizeLeft) {
    const colDelta = Math.round(dx / (cellWidth + gap));
    newCol = state.startCol + colDelta;
    newColSpan = state.startColSpan - colDelta;
  }
  if (resizeTop) {
    const rowDelta = Math.round(dy / (rowHeight + gap));
    newRow = state.startRow + rowDelta;
    newRowSpan = state.startRowSpan - rowDelta;
  }

  // 应用来自控件 data 属性的 min/max
  const widget = state.originalWidget;
  const minColSpan = parseInt(widget.getAttribute('data-min-col-span') || '1', 10);
  const minRowSpan = parseInt(widget.getAttribute('data-min-row-span') || '1', 10);
  const maxColSpan = parseInt(widget.getAttribute('data-max-col-span') || '0', 10);
  const maxRowSpan = parseInt(widget.getAttribute('data-max-row-span') || '0', 10);

  newColSpan = Math.max(minColSpan, newColSpan);
  newRowSpan = Math.max(minRowSpan, newRowSpan);
  if (maxColSpan > 0) { newColSpan = Math.min(maxColSpan, newColSpan); }
  if (maxRowSpan > 0) { newRowSpan = Math.min(maxRowSpan, newRowSpan); }

  // 从左/上边缘缩放时重算位置，使对侧边缘保持不动
  if (resizeLeft) {
    newCol = state.startCol + state.startColSpan - newColSpan;
  }
  if (resizeTop) {
    newRow = state.startRow + state.startRowSpan - newRowSpan;
  }

  // 网格边界
  newCol = Math.max(1, newCol);
  newRow = Math.max(1, newRow);
  newColSpan = Math.min(newColSpan, cols - newCol + 1);

  // 无任何变化则跳过
  if (newCol === state.lastResizeCol && newRow === state.lastResizeRow &&
      newColSpan === state.lastResizeColSpan && newRowSpan === state.lastResizeRowSpan) return;

  let resolved = resolveLayoutAfterResize(
    state.originalResizePositions, state.activeWidgetId,
    newCol, newRow, newColSpan, newRowSpan,
    state.startCol, state.startRow, state.startColSpan, state.startRowSpan,
    cols
  );

  // resolve 返回 null 即拒绝本次缩放增量（保留上一个合法布局）
  if (!resolved) {
    state.lastResizeCol = newCol;
    state.lastResizeRow = newRow;
    state.lastResizeColSpan = newColSpan;
    state.lastResizeRowSpan = newRowSpan;
    return;
  }

  if (state.options.compact) {
    compact(resolved, state.activeWidgetId, cols);
  }
  applyLayout(grid, resolved);

  state.resolvedResizeLayout = resolved;
  state.targetCol = newCol;
  state.targetRow = newRow;
  state.targetColSpan = newColSpan;
  state.targetRowSpan = newRowSpan;
  state.lastResizeCol = newCol;
  state.lastResizeRow = newRow;
  state.lastResizeColSpan = newColSpan;
  state.lastResizeRowSpan = newRowSpan;
}

function finishResize(state) {
  state.originalWidget.setAttribute('data-resizing', 'false');
  document.body.style.userSelect = '';

  // 重新聚焦控件，让键盘导航继续可用
  state.originalWidget.focus();

  const finalLayout = state.resolvedResizeLayout;
  const originalPositions = state.originalResizePositions;

  if (!finalLayout || !originalPositions) return;

  // 交互结束后执行压紧
  const grid = state.resizeGrid;
  if (state.options.compact && grid) {
    compact(finalLayout, null, getActiveColumns(state));
    applyLayout(grid, finalLayout);
  }

  // 找到被缩放控件的最终位置
  const resized = finalLayout.find(p => p.id === state.activeWidgetId);
  if (!resized) return;

  const resizeChanged = resized.col !== state.startCol || resized.row !== state.startRow ||
      resized.colSpan !== state.startColSpan || resized.rowSpan !== state.startRowSpan;

  if (resizeChanged) {
    // 为被缩放控件发送 resize 事件
    invokeSafely(state, 'JsOnWidgetResizeEnd',
      state.activeWidgetId, resized.col, resized.row, resized.colSpan, resized.rowSpan);

    // 为所有被挤开的控件发送布局变更
    const changes = [];
    for (const final of finalLayout) {
      if (final.id === state.activeWidgetId) continue;
      const original = originalPositions.find(p => p.id === final.id);
      if (!original) continue;
      if (final.col !== original.col || final.row !== original.row) {
        changes.push({ id: final.id, col: final.col, row: final.row });
      }
    }
    if (changes.length > 0) {
      invokeSafely(state, 'JsOnLayoutResolved', state.activeWidgetId, changes);
    }

    announceChange(state, `Widget resized to ${resized.colSpan} columns, ${resized.rowSpan} rows`);
  }
}

// --- 键盘导航 ---

function onKeyDown(instanceId, state, e) {
  if (!state.options.editable) return;

  const target = e.target;

  // 处理缩放手柄的键盘操作（shift+方向键缩放）。
  // 必须先于控件移动处理分支检查，因为缩放手柄
  // 同样带有 data-widget-id，会命中移动分支的选择器。
  const resizeHandle = target.closest('[data-dashboard-resize-handle]');
  if (resizeHandle && state.options.allowResize) {
    const widgetId = resizeHandle.getAttribute('data-widget-id');
    const widget = document.querySelector(`[data-widget-id="${widgetId}"]`);
    if (!widget) return;

    if (!e.shiftKey) return;

    const grid = widget.closest('[role="region"][aria-roledescription="dashboard grid"]');
    if (!grid) return;

    const style = widget.style;
    const col = parseGridValue(style.gridColumn, 'start');
    const row = parseGridValue(style.gridRow, 'start');
    const colSpan = parseGridValue(style.gridColumn, 'span');
    const rowSpan = parseGridValue(style.gridRow, 'span');
    const cols = getActiveColumns(state);

    const minColSpan = parseInt(widget.getAttribute('data-min-col-span') || '1', 10);
    const minRowSpan = parseInt(widget.getAttribute('data-min-row-span') || '1', 10);
    const maxColSpan = parseInt(widget.getAttribute('data-max-col-span') || '0', 10);
    const maxRowSpan = parseInt(widget.getAttribute('data-max-row-span') || '0', 10);

    let newColSpan = colSpan;
    let newRowSpan = rowSpan;
    let handled = false;

    if (e.key === 'ArrowRight') { newColSpan = colSpan + 1; handled = true; }
    else if (e.key === 'ArrowLeft') { newColSpan = colSpan - 1; handled = true; }
    else if (e.key === 'ArrowDown') { newRowSpan = rowSpan + 1; handled = true; }
    else if (e.key === 'ArrowUp') { newRowSpan = rowSpan - 1; handled = true; }

    if (handled) {
      e.preventDefault();
      newColSpan = Math.max(minColSpan, Math.min(newColSpan, cols - col + 1));
      newRowSpan = Math.max(minRowSpan, newRowSpan);
      if (maxColSpan > 0) newColSpan = Math.min(maxColSpan, newColSpan);
      if (maxRowSpan > 0) newRowSpan = Math.min(maxRowSpan, newRowSpan);

      if (newColSpan !== colSpan || newRowSpan !== rowSpan) {
        const allPositions = getWidgetPositions(grid, null);

        let resolved = resolveLayoutAfterResize(
          allPositions, widgetId, col, row, newColSpan, newRowSpan,
          col, row, colSpan, rowSpan, cols
        );
        if (resolved && state.options.compact) {
          compact(resolved, widgetId, cols);
        }

        if (!resolved) return;
        applyLayout(grid, resolved);

        // 为被缩放控件发送 resize
        invokeSafely(state, 'JsOnWidgetResizeEnd', widgetId, col, row, newColSpan, newRowSpan);

        // 为被挤开的控件发送位置变更
        const changes = [];
        for (const r of resolved) {
          if (r.id === widgetId) continue;
          const orig = allPositions.find(p => p.id === r.id);
          if (!orig) continue;
          if (r.col !== orig.col || r.row !== orig.row) {
            changes.push({ id: r.id, col: r.col, row: r.row });
          }
        }
        if (changes.length > 0) {
          invokeSafely(state, 'JsOnLayoutResolved', widgetId, changes);
        }
        announceChange(state, `Widget resized to ${newColSpan} columns, ${newRowSpan} rows`);
      }
    }
    return;
  }

  // 处理控件的键盘导航（方向键移动）
  const widgetTarget = target.closest('[data-widget-id]');
  if (widgetTarget && state.options.allowDrag && widgetTarget.getAttribute('data-static') !== 'true') {
    const widgetId = widgetTarget.getAttribute('data-widget-id');
    const widget = widgetTarget;

    const grid = widget.closest('[role="region"][aria-roledescription="dashboard grid"]');
    if (!grid) return;

    const style = widget.style;
    const col = parseGridValue(style.gridColumn, 'start');
    const row = parseGridValue(style.gridRow, 'start');
    const colSpan = parseGridValue(style.gridColumn, 'span');
    const rowSpan = parseGridValue(style.gridRow, 'span');
    const cols = getActiveColumns(state);

    let newCol = col;
    let newRow = row;
    let handled = false;

    if (e.key === 'ArrowLeft') { newCol = Math.max(1, col - 1); handled = true; }
    else if (e.key === 'ArrowRight') { newCol = Math.min(cols - colSpan + 1, col + 1); handled = true; }
    else if (e.key === 'ArrowUp') { newRow = Math.max(1, row - 1); handled = true; }
    else if (e.key === 'ArrowDown') { newRow = row + 1; handled = true; }

    if (handled) {
      e.preventDefault();
      if (newCol !== col || newRow !== row) {
        const allPositions = getWidgetPositions(grid, null);

        let resolved = resolveLayoutAfterDrag(allPositions, widgetId, newCol, newRow, col, row, cols);
        if (resolved && state.options.compact) {
          compact(resolved, widgetId, cols);
        }

        if (!resolved) return;
        applyLayout(grid, resolved);

        const changes = [];
        for (const r of resolved) {
          const orig = allPositions.find(p => p.id === r.id);
          if (!orig) continue;
          if (r.col !== orig.col || r.row !== orig.row) {
            changes.push({ id: r.id, col: r.col, row: r.row });
          }
        }
        if (changes.length > 0) {
          invokeSafely(state, 'JsOnLayoutResolved', widgetId, changes);
        }
        announceChange(state, `Widget moved to column ${newCol}, row ${newRow}`);
      }
    }
    return;
  }

  // Escape 取消进行中的拖拽/缩放
  if (e.key === 'Escape' && (state.isDragging || state.isResizing)) {
    e.preventDefault();
    cancelInteraction(state);
  }
}

// --- 焦点移出（失焦时压紧） ---

function onWidgetFocusOut(instanceId, state, e) {
  if (!state.options.compact || !state.options.editable) return;
  if (state.isDragging || state.isResizing) return;

  // 仅在控件失去焦点时行动
  const widget = e.target.closest('[data-widget-id]');
  if (!widget) return;

  // 焦点移向同一控件内的其他元素时忽略不办
  if (e.relatedTarget && widget.contains(e.relatedTarget)) return;

  const grid = state.gridEl;
  if (!grid) return;

  const positions = getWidgetPositions(grid, null);
  if (positions.length === 0) return;

  // 压紧前先取快照，以便检测变化
  const before = positions.map(p => ({ ...p }));
  compact(positions, null, getActiveColumns(state));

  // 检查是否真的发生了移动
  const changes = [];
  for (const pos of positions) {
    const orig = before.find(p => p.id === pos.id);
    if (!orig) continue;
    if (pos.col !== orig.col || pos.row !== orig.row) {
      changes.push({ id: pos.id, col: pos.col, row: pos.row });
    }
  }

  if (changes.length > 0) {
    applyLayout(grid, positions);
    invokeSafely(state, 'JsOnLayoutResolved', '', changes);
  }
}

// --- 碰撞检测与布局解析 ---

function getWidgetPositions(grid, excludeWidgetId) {
  // 用 :scope > 只选取直接子控件，不选取同样带 data-widget-id
  // 属性的嵌套缩放手柄/拖拽元素。
  const widgets = grid.querySelectorAll(':scope > [data-widget-id]');
  const positions = [];
  for (const w of widgets) {
    const id = w.getAttribute('data-widget-id');
    if (id === excludeWidgetId) continue;
    const col = parseGridValue(w.style.gridColumn, 'start');
    const row = parseGridValue(w.style.gridRow, 'start');
    const colSpan = parseGridValue(w.style.gridColumn, 'span');
    const rowSpan = parseGridValue(w.style.gridRow, 'span');
    const isStatic = w.getAttribute('data-static') === 'true';
    positions.push({ id, col, row, colSpan, rowSpan, isStatic });
  }
  return positions;
}

function wouldOverlap(col, row, colSpan, rowSpan, others) {
  for (const o of others) {
    if (
      col < o.col + o.colSpan &&
      o.col < col + colSpan &&
      row < o.row + o.rowSpan &&
      o.row < row + rowSpan
    ) {
      return true;
    }
  }
  return false;
}

/**
 * 完全压紧：控件先向左、再向上滑动，消除所有空隙。
 * fixedId 控件（正在被拖拽/缩放的那个）留在原地。
 */
function compact(positions, fixedId, columns) {
  const sorted = [...positions].sort((a, b) => a.row !== b.row ? a.row - b.row : a.col - b.col);
  const placed = [];

  // 预置固定控件
  const fixed = sorted.find(p => p.id === fixedId);
  if (fixed) placed.push(fixed);

  // 第一遍：在当前行左滑，必要时溢出到下一行
  for (const widget of sorted) {
    if (widget.id === fixedId) continue;

    let found = false;
    for (let col = 1; col <= columns - widget.colSpan + 1; col++) {
      if (!wouldOverlap(col, widget.row, widget.colSpan, widget.rowSpan, placed)) {
        widget.col = col;
        found = true;
        break;
      }
    }
    // 向下不限深度搜索，直到找到空位
    for (let row = widget.row + 1; !found; row++) {
      for (let col = 1; col <= columns - widget.colSpan + 1; col++) {
        if (!wouldOverlap(col, row, widget.colSpan, widget.rowSpan, placed)) {
          widget.row = row;
          widget.col = col;
          found = true;
          break;
        }
      }
    }
    placed.push(widget);
  }

  // 第二遍：上滑但不改列
  placed.length = 0;
  const sorted2 = [...positions].sort((a, b) => a.row !== b.row ? a.row - b.row : a.col - b.col);
  if (fixed) placed.push(fixed);

  for (const widget of sorted2) {
    if (widget.id === fixedId) continue;
    for (let row = 1; row <= widget.row; row++) {
      if (!wouldOverlap(widget.col, row, widget.colSpan, widget.rowSpan, placed)) {
        widget.row = row;
        break;
      }
    }
    placed.push(widget);
  }
}

function applyLayout(grid, layout) {
  for (const pos of layout) {
    const el = grid.querySelector(`:scope > [data-widget-id="${pos.id}"]`);
    if (el) {
      el.style.gridColumn = `${pos.col} / span ${pos.colSpan}`;
      el.style.gridRow = `${pos.row} / span ${pos.rowSpan}`;
    }
  }
}

/**
 * 检查两个矩形在网格上是否重叠。
 */
function rectsOverlap(a, b) {
  return a.col < b.col + b.colSpan &&
    b.col < a.col + a.colSpan &&
    a.row < b.row + b.rowSpan &&
    b.row < a.row + a.rowSpan;
}

/**
 * 找出与给定控件重叠的全部控件。
 */
function findCollisions(widget, allWidgets) {
  const collisions = [];
  for (const other of allWidgets) {
    if (other.id === widget.id) continue;
    if (rectsOverlap(widget, other)) {
      collisions.push(other);
    }
  }
  return collisions;
}

/**
 * 沿给定轴向/方向把单个控件从 `pusher` 前方推开。
 * 若被推开的控件又与其他控件重叠，则递归级联推动。
 * 推动不可行（画布边缘、static 控件、环路）时返回 false。
 */
function pushWidget(widget, pusher, axis, direction, allWidgets, columns, visited) {
  if (widget.isStatic) return false;
  if (visited.has(widget.id)) return false;
  visited.add(widget.id);

  // 保存原位以便回滚
  const origCol = widget.col;
  const origRow = widget.row;

  // 把控件移到 pusher 外侧紧邻的位置
  if (axis === 'col') {
    widget.col = direction > 0
      ? pusher.col + pusher.colSpan  // 向右推
      : pusher.col - widget.colSpan; // 向左推
  } else {
    widget.row = direction > 0
      ? pusher.row + pusher.rowSpan  // 向下推
      : pusher.row - widget.rowSpan; // 向上推
  }

  // 检查网格边界（仅水平方向——纵向向下自由生长）
  if (widget.col < 1 || widget.col + widget.colSpan - 1 > columns || widget.row < 1) {
    widget.col = origCol;
    widget.row = origRow;
    visited.delete(widget.id);
    return false;
  }

  // 找出本次推动引发的新碰撞
  const newCollisions = findCollisions(widget, allWidgets);
  for (const collider of newCollisions) {
    if (collider.id === pusher.id) continue;
    if (!pushWidget(collider, widget, axis, direction, allWidgets, columns, visited)) {
      // 级联失败——回滚
      widget.col = origCol;
      widget.row = origRow;
      visited.delete(widget.id);
      return false;
    }
  }

  return true;
}

/**
 * 解析把控件拖到新位置之后的布局。
 * 采用碰撞检测 + 局部位移，而非全局压紧。
 * 返回解析后的位置数组；应拒绝该移动时返回 null。
 */
function resolveLayoutAfterDrag(originalPositions, draggedId, targetCol, targetRow, origCol, origRow, columns) {
  const positions = originalPositions.map(p => ({...p}));
  const dragged = positions.find(p => p.id === draggedId);
  if (!dragged) return null;

  // 把被拖控件放到目标位置（夹取）
  dragged.col = Math.max(1, Math.min(targetCol, columns - dragged.colSpan + 1));
  dragged.row = Math.max(1, targetRow);

  // 找出重叠的控件
  const collisions = findCollisions(dragged, positions);

  if (collisions.length === 0) {
    return positions;
  }

  // 恰好 1 个碰撞时尝试交换
  if (collisions.length === 1) {
    const collider = collisions[0];
    if (!collider.isStatic) {
      // 尝试把碰撞者放到被拖控件的原始位置
      const swapPositions = originalPositions.map(p => ({...p}));
      const swapDragged = swapPositions.find(p => p.id === draggedId);
      const swapCollider = swapPositions.find(p => p.id === collider.id);

      swapDragged.col = dragged.col;
      swapDragged.row = dragged.row;
      swapCollider.col = origCol;
      swapCollider.row = origRow;

      // 检查碰撞者置于原位时是否不与其他任何控件重叠
      const swapConflicts = findCollisions(swapCollider, swapPositions);
      if (swapConflicts.length === 0) {
        return swapPositions;
      }
    }
  }

  // 沿拖拽方向推开碰撞者
  const dragDirCol = targetCol - origCol;
  const dragDirRow = targetRow - origRow;

  // 依据拖拽位移判定主推动轴向与方向
  let pushAxis, pushDirection;
  if (Math.abs(dragDirCol) >= Math.abs(dragDirRow)) {
    pushAxis = 'col';
    pushDirection = dragDirCol >= 0 ? 1 : -1;
  } else {
    pushAxis = 'row';
    pushDirection = dragDirRow >= 0 ? 1 : -1;
  }

  for (const collider of collisions) {
    const visited = new Set([draggedId]);
    if (!pushWidget(collider, dragged, pushAxis, pushDirection, positions, columns, visited)) {
      // 主方向失败——改试次级轴向
      const altAxis = pushAxis === 'col' ? 'row' : 'col';
      const altDirection = pushAxis === 'col' ? (dragDirRow >= 0 ? 1 : -1) : (dragDirCol >= 0 ? 1 : -1);
      const visited2 = new Set([draggedId]);

      // 前一次推动可能已造成部分改动，故从原始位置重新推导
      const retryPositions = originalPositions.map(p => ({...p}));
      const retryDragged = retryPositions.find(p => p.id === draggedId);
      retryDragged.col = dragged.col;
      retryDragged.row = dragged.row;

      const retryCollisions = findCollisions(retryDragged, retryPositions);
      let allPushed = true;
      for (const rc of retryCollisions) {
        if (!pushWidget(rc, retryDragged, altAxis, altDirection, retryPositions, columns, visited2)) {
          // 万不得已时尝试向下推
          const visited3 = new Set([draggedId]);
          if (!pushWidget(rc, retryDragged, 'row', 1, retryPositions, columns, visited3)) {
            allPushed = false;
            break;
          }
        }
      }

      if (allPushed) {
        return retryPositions;
      }

      return null;
    }
  }

  return positions;
}

/**
 * 解析控件缩放之后的布局。
 * 沿增长边缘的方向推开碰撞者。
 * 返回解析后的位置数组；应拒绝该缩放时返回 null。
 */
function resolveLayoutAfterResize(originalPositions, resizedId, newCol, newRow, newColSpan, newRowSpan, startCol, startRow, startColSpan, startRowSpan, columns) {
  const positions = originalPositions.map(p => ({...p}));
  const resized = positions.find(p => p.id === resizedId);
  if (!resized) return null;

  resized.col = newCol;
  resized.row = newRow;
  resized.colSpan = newColSpan;
  resized.rowSpan = newRowSpan;

  const collisions = findCollisions(resized, positions);

  if (collisions.length === 0) {
    return positions;
  }

  // 判定哪些边缘在增长
  const grewRight = (newCol + newColSpan) > (startCol + startColSpan);
  const grewLeft = newCol < startCol;
  const grewDown = (newRow + newRowSpan) > (startRow + startRowSpan);
  const grewUp = newRow < startRow;

  for (const collider of collisions) {
    let pushed = false;
    const visited = new Set([resizedId]);

    // 尝试沿增长边缘的方向推动
    if (grewRight && !pushed) {
      pushed = pushWidget(collider, resized, 'col', 1, positions, columns, visited);
    }
    if (grewLeft && !pushed) {
      visited.clear(); visited.add(resizedId);
      pushed = pushWidget(collider, resized, 'col', -1, positions, columns, visited);
    }
    if (grewDown && !pushed) {
      visited.clear(); visited.add(resizedId);
      pushed = pushWidget(collider, resized, 'row', 1, positions, columns, visited);
    }
    if (grewUp && !pushed) {
      visited.clear(); visited.add(resizedId);
      pushed = pushWidget(collider, resized, 'row', -1, positions, columns, visited);
    }

    // 最后手段：向下推
    if (!pushed) {
      visited.clear(); visited.add(resizedId);
      pushed = pushWidget(collider, resized, 'row', 1, positions, columns, visited);
    }

    if (!pushed) {
      return null;
    }
  }

  return positions;
}

// --- 辅助函数 ---

function parseGridValue(gridProp, type) {
  if (!gridProp) return 1;
  // 格式为："col / span colSpan" 或 "row / span rowSpan"
  const parts = gridProp.split('/').map(s => s.trim());
  if (type === 'start') {
    return parseInt(parts[0], 10) || 1;
  }
  if (type === 'span') {
    const spanPart = parts[1] || '';
    const match = spanPart.match(/span\s+(\d+)/);
    return match ? parseInt(match[1], 10) : 1;
  }
  return 1;
}

function cancelInteraction(state) {
  // 将所有控件恢复到原始位置
  if (state.dragGrid && state.originalPositions) {
    applyLayout(state.dragGrid, state.originalPositions);
  }
  if (state.resizeGrid && state.originalResizePositions) {
    applyLayout(state.resizeGrid, state.originalResizePositions);
  }

  if (state.originalWidget) {
    state.originalWidget.style.opacity = '';
    state.originalWidget.style.zIndex = '';
    state.originalWidget.setAttribute('data-dragging', 'false');
    state.originalWidget.setAttribute('data-resizing', 'false');
  }

  document.body.style.userSelect = '';
  document.body.style.cursor = '';

  resetState(state);
}

function resetState(state) {
  if (state.pointerId != null && state.gridEl) {
    try { state.gridEl.releasePointerCapture(state.pointerId); } catch {}
  }
  state.isDragging = false;
  state.isResizing = false;
  state.pendingDrag = false;
  state.pendingResize = false;
  state.activeWidgetId = null;
  state.activeHandle = null;
  state.originalWidget = null;
  state.pointerId = null;
  state.targetCol = undefined;
  state.targetRow = undefined;
  state.targetColSpan = undefined;
  state.targetRowSpan = undefined;
  state.dragGrid = null;
  state.originalPositions = null;
  state.resolvedLayout = null;
  state.lastResolvedCol = undefined;
  state.lastResolvedRow = undefined;
  state.resizeGrid = null;
  state.originalResizePositions = null;
  state.resolvedResizeLayout = null;
  state.lastResizeCol = undefined;
  state.lastResizeRow = undefined;
  state.lastResizeColSpan = undefined;
  state.lastResizeRowSpan = undefined;
}

function announceChange(state, message) {
  // 找到归属于本网格实例的 live region（实时播报区域）
  const grid = state.gridEl;
  if (!grid) return;
  const id = grid.getAttribute('data-dashboard-id');
  const region = id ? document.getElementById(`${id}-live`) : null;
  if (region) {
    region.textContent = message;
    setTimeout(() => { region.textContent = ''; }, 1000);
  }
}
