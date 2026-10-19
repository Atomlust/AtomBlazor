// 可调整尺寸面板的拖拽处理程序
// 在 document 层面处理指针拖拽事件，让调整尺寸交互保持平滑。
//
// 拖拽循环直接把 flex-basis 写到面板元素上，并且每个 pointermove 都不
// 回调进 Blazor。按帧一次往返在 Blazor Server 上不可用：interop 会在电路
// 的同步上下文上串行派发，于是比服务端处理能力到达得更快的移动会排起长队，
// 延迟在整次拖拽里不断累积。
//
// 因此 C# 在拖拽期间是刻意陈旧的，只在 pointerup/pointercancel 时精确同步
// 一次。这依赖 Blazor 与它上一次渲染的结果做 diff，而不是与 DOM 做 diff：
// 只要渲染出的尺寸没变，重渲染就不会动我们写下的 flex-basis。

let resizableStates = new Map();

/**
 * 为一个面板组初始化调整尺寸的处理
 * @param {HTMLElement} groupElement - 面板组的容器元素
 * @param {DotNetObject} dotNetRef - 对 Blazor 组件的引用
 * @param {string} groupId - 组的唯一标识符
 * @param {boolean} isHorizontal - 布局是否为水平
 */
export function initializeResizable(groupElement, dotNetRef, groupId, isHorizontal) {
    if (!groupElement || !dotNetRef) {
        console.error('initializeResizable: missing required parameters');
        return;
    }

    const state = {
        groupElement,
        dotNetRef,
        groupId,
        isHorizontal,
        isDragging: false,
        activeHandleIndex: -1,
        startPosition: 0,
        startSizes: [],
        currentSizes: [],
        pointerId: null,
        panelElements: [],
        useDomWrites: true,
        pendingPosition: 0,
        frameHandle: 0,
        dirty: false
    };

    resizableStates.set(groupId, state);
}

/**
 * 开始一次调整尺寸操作
 * @param {string} groupId - 组标识符
 * @param {number} handleIndex - 被拖拽手柄的索引
 * @param {number} clientX - 指针 X 坐标
 * @param {number} clientY - 指针 Y 坐标
 * @param {number[]} currentSizes - 当前面板尺寸（百分比）
 * @param {number} pointerId - 本次调整尺寸操作的指针 ID
 * @param {number[]} [minSizes] - 每个面板的最小尺寸（百分比）
 * @param {number[]} [maxSizes] - 每个面板的最大尺寸（百分比）
 * @param {boolean} [isHorizontal] - 当前朝向，以防 Direction 自初始化以来变了
 * @param {boolean[]} [collapsible] - 哪些面板允许被拖过最小值一路合拢
 */
export function startResize(groupId, handleIndex, clientX, clientY, currentSizes, pointerId, minSizes, maxSizes, isHorizontal, collapsible) {
    const state = resizableStates.get(groupId);
    if (!state) return;

    if (state.isDragging) return; // 忽略次级触摸

    if (typeof isHorizontal === 'boolean') {
        state.isHorizontal = isHorizontal;
    }

    state.isDragging = true;
    state.activeHandleIndex = handleIndex;
    state.startPosition = state.isHorizontal ? clientX : clientY;
    state.pendingPosition = state.startPosition;
    state.startSizes = [...currentSizes];
    state.currentSizes = [...currentSizes];
    state.pointerId = pointerId;
    state.minSizes = minSizes || currentSizes.map(() => 10);
    state.maxSizes = maxSizes || currentSizes.map(() => 100);
    state.collapsible = collapsible || currentSizes.map(() => false);
    state.frameHandle = 0;
    state.dirty = false;

    // 每次拖拽只解析一次面板元素，让移动循环永不触碰 DOM 树。
    state.panelElements = resolvePanelElements(state);
    state.useDomWrites = !!(state.panelElements[handleIndex] && state.panelElements[handleIndex + 1]);

    if (!state.useDomWrites) {
        console.warn(
            `resizable: could not resolve the panel elements for handle ${handleIndex} in group ${groupId}. ` +
            'Falling back to per-frame interop, which is slow on Blazor Server.');
    }

    // 拖拽期间防止文本选中
    document.body.style.userSelect = 'none';
    document.body.style.cursor = state.isHorizontal ? 'col-resize' : 'row-resize';

    // 添加 document 级监听器
    const handlePointerMove = (e) => onPointerMove(groupId, e);
    const handlePointerUp = (e) => onPointerUp(groupId, e);
    const handlePointerCancel = (e) => onPointerCancel(groupId, e);

    state.handlePointerMove = handlePointerMove;
    state.handlePointerUp = handlePointerUp;
    state.handlePointerCancel = handlePointerCancel;

    document.addEventListener('pointermove', handlePointerMove);
    document.addEventListener('pointerup', handlePointerUp);
    document.addEventListener('pointercancel', handlePointerCancel);

    // 在组元素上设置指针捕获，保证拖拽跟踪可靠
    state.groupElement.setPointerCapture(pointerId);
}

/**
 * 找到本组自己的面板元素，按声明顺序。
 *
 * 以组 id 而非后代位置限定范围，因为嵌套面板组的面板同样是本组元素的
 * 后代，绝不能被一并拾取。
 */
function resolvePanelElements(state) {
    const ordered = [];

    for (const element of state.groupElement.querySelectorAll('[data-atom-resizable-panel]')) {
        if (element.getAttribute('data-atom-resizable-group') !== state.groupId) continue;

        const index = Number.parseInt(element.getAttribute('data-atom-resizable-panel'), 10);
        if (Number.isInteger(index) && index >= 0) {
            ordered[index] = element;
        }
    }

    return ordered;
}

function onPointerMove(groupId, e) {
    const state = resizableStates.get(groupId);
    if (!state || !state.isDragging || e.pointerId !== state.pointerId) return;

    e.preventDefault();

    state.pendingPosition = state.isHorizontal ? e.clientX : e.clientY;

    // 合并为每帧一次布局写入；pointermove 的触发频率远高于显示刷新率。
    if (state.frameHandle) return;
    state.frameHandle = requestAnimationFrame(() => {
        state.frameHandle = 0;
        applyDrag(state);
    });
}

function applyDrag(state) {
    if (!state.isDragging) return;

    const rect = state.groupElement.getBoundingClientRect();
    const totalSize = state.isHorizontal ? rect.width : rect.height;
    if (!(totalSize > 0)) return;

    const index = state.activeHandleIndex;
    const start1 = state.startSizes[index];
    const start2 = state.startSizes[index + 1];
    if (start1 === undefined || start2 === undefined) return;

    // 可折叠面板可以一路降到零；它声明的最小值是返程时 snaps 越过的位置，
    // 不是下限。
    const min1 = state.collapsible[index] ? 0 : state.minSizes[index];
    const max1 = state.maxSizes[index];
    const min2 = state.collapsible[index + 1] ? 0 : state.minSizes[index + 1];
    const max2 = state.maxSizes[index + 1];

    // 把 delta 钳制到两个面板都能满足的范围，越界的拖拽于是钉在限位上。
    // 此前的代码会整帧拒绝这次移动，结果手柄被冻结在上一个恰好落在
    // 范围内的帧所在的位置。
    const lowerBound = Math.max(min1 - start1, start2 - max2);
    const upperBound = Math.min(max1 - start1, start2 - min2);
    if (lowerBound > upperBound) return; // 约束无法同时被满足

    // 水平组沿阅读方向排布面板，所以无论哪一侧，把手柄朝前缘拖动
    // 都必须增大前缘的面板。
    const rtl = state.isHorizontal
        && getComputedStyle(state.groupElement).direction === 'rtl';
    const deltaPixels = (state.pendingPosition - state.startPosition) * (rtl ? -1 : 1);
    const deltaPercent = clamp((deltaPixels / totalSize) * 100, lowerBound, upperBound);

    // 在这里而非渲染时做四舍五入，让 C# 最终格式化的值（"F2"）与已经写到
    // DOM 上的值精确一致——否则面板在拖拽结束时会跳动一个亚像素。
    let size1 = round2(start1 + deltaPercent);
    let size2 = round2(start1 + start2 - size1);

    // 宁可合拢也不留一条窄缝：低于声明最小值的一半面板就折叠，
    // 返程时再在那一最小值处重新张开。
    size1 = snapCollapse(state, index, size1);
    size2 = round2(start1 + start2 - size1);
    const snapped2 = snapCollapse(state, index + 1, size2);
    if (snapped2 !== size2) {
        size2 = snapped2;
        size1 = round2(start1 + start2 - size2);
    }

    if (size1 === state.currentSizes[index] && size2 === state.currentSizes[index + 1]) return;

    state.currentSizes[index] = size1;
    state.currentSizes[index + 1] = size2;
    state.dirty = true;

    if (state.useDomWrites) {
        state.panelElements[index].style.flexBasis = `${size1}%`;
        state.panelElements[index + 1].style.flexBasis = `${size2}%`;
    } else {
        notifyBlazor(state, 'UpdatePanelSizes');
    }
}

/**
 * 对一个面板应用合拢吸附：可折叠面板被拖到低于其声明最小值的一半
 * 时归零，介于零与该最小值之间的一律重新张开到该最小值。
 */
function snapCollapse(state, index, size) {
    if (!state.collapsible[index]) return size;

    const min = state.minSizes[index];
    if (!(min > 0) || size >= min) return size;

    return size < min / 2 ? 0 : min;
}

function clamp(value, min, max) {
    return Math.min(Math.max(value, min), max);
}

function round2(value) {
    return Math.round(value * 100) / 100;
}

function notifyBlazor(state, method) {
    state.dotNetRef.invokeMethodAsync(method, [...state.currentSizes]).catch(err => {
        console.error('Error updating panel sizes:', err);
    });
}

function onPointerUp(groupId, e) {
    const state = resizableStates.get(groupId);
    if (!state || e.pointerId !== state.pointerId) return;

    cleanupResize(state, e.pointerId);
}

function onPointerCancel(groupId, e) {
    const state = resizableStates.get(groupId);
    if (!state || e.pointerId !== state.pointerId) return;

    cleanupResize(state, e.pointerId);
}

function cleanupResize(state, pointerId) {
    // 先冲掉排队中的一帧，让提交给 C# 的尺寸与屏幕上的保持一致。
    if (state.frameHandle) {
        cancelAnimationFrame(state.frameHandle);
        state.frameHandle = 0;
        applyDrag(state);
    }

    // 只有真正移动过东西的拖拽才算调整尺寸；按下再松开不算。
    const shouldCommit = state.dirty;

    state.isDragging = false;
    state.activeHandleIndex = -1;
    state.pointerId = null;
    state.dirty = false;

    document.body.style.userSelect = '';
    document.body.style.cursor = '';

    removeDocumentListeners(state);

    try {
        state.groupElement.releasePointerCapture(pointerId);
    } catch (err) {
        // 指针捕获可能已经释放过了
    }

    // 整次拖拽唯一的一次 interop 调用。在回退路径上，这是在按帧调用之后
    // 多出的又一次调用，好让 OnResizeEnd 在那里也恰好触发一次。
    if (shouldCommit) {
        notifyBlazor(state, 'CommitPanelSizes');
    }
}

function removeDocumentListeners(state) {
    if (state.handlePointerMove) {
        document.removeEventListener('pointermove', state.handlePointerMove);
        state.handlePointerMove = null;
    }
    if (state.handlePointerUp) {
        document.removeEventListener('pointerup', state.handlePointerUp);
        state.handlePointerUp = null;
    }
    if (state.handlePointerCancel) {
        document.removeEventListener('pointercancel', state.handlePointerCancel);
        state.handlePointerCancel = null;
    }
}

/**
 * 释放一个面板组的调整尺寸处理
 * @param {string} groupId - 组标识符
 */
export function disposeResizable(groupId) {
    const state = resizableStates.get(groupId);
    if (state) {
        if (state.frameHandle) {
            cancelAnimationFrame(state.frameHandle);
            state.frameHandle = 0;
        }

        // 若在拖拽中途释放，这些就会残留在 <body> 上直到页面生命周期结束。
        if (state.isDragging) {
            document.body.style.userSelect = '';
            document.body.style.cursor = '';
        }

        state.isDragging = false;
        removeDocumentListeners(state);
        resizableStates.delete(groupId);
    }
}
