// Dock 的停靠交互。
//
// 处理三种指针驱动的手势：
// 1. 拖动面板标签以重新停靠（作为标签或分屏）、停靠到 dock 边缘、或摘出为
//    浮动窗口。绘制实时落点指示器，并把解析出的落点上报 .NET。
// 2. 通过标签带拖动浮动窗口以移位。
// 3. 拖动浮动窗口的缩放手柄以改尺寸。
//
// 布局模型归 .NET 所有；本模块只计算落点目标并上报结果。

const docks = new Map();
const DRAG_THRESHOLD = 4;
// 主题设计令牌（完整颜色值，oklch），使浮层在亮色与暗色主题下都协调。
const ACCENT_BG = "color-mix(in oklab, var(--primary) 25%, transparent)";
const ACCENT_BORDER = "2px solid var(--primary)";
const ACCENT_SOLID = "var(--primary)";

export function initializeDock(dockId, rootEl, dotNetRef) {
    if (!dockId || !rootEl || !dotNetRef) {
        return;
    }
    docks.set(dockId, { rootEl, dotNetRef, cancelSession: null });
}

export function disposeDock(dockId) {
    const dock = docks.get(dockId);
    if (dock && dock.cancelSession) {
        // 中止进行中的拖拽，避免组件在手势中途被释放时把幽灵/指示器元素
        // 与 document 级监听器变成孤儿。
        dock.cancelSession();
    }
    docks.delete(dockId);
}

// ---------------------------------------------------------------- 标签带溢出

// 每条被观察的标签带对应一个条目，以标签组的 id 为键。
const tabOverflowObservers = new Map();

// 为一条标签带建立溢出跟踪。标签带每次改变尺寸，重新计算放不下的
// 标签集合并上报 .NET，由它把这些标签隐藏，
// 改以溢出（“…”）下拉代替滚动条呈现。
export function initTabOverflow(groupId, stripEl, dotNetRef) {
    if (!groupId || !stripEl || !dotNetRef) {
        return;
    }

    disposeTabOverflow(groupId);

    const ro = new ResizeObserver(() => reportTabOverflow(groupId));
    ro.observe(stripEl);

    // 标签是 div，所以 tablist 要处理的按键（漫游焦点 + 激活，在 .NET 完成）
    // 否则还会连带滚动页面。标签内的按钮（如关闭按钮）
    // 不受干扰，Space/Enter 仍能点击它们。
    const onKeyDown = (e) => {
        if (!e.target.closest || !e.target.closest("[data-dock-tab]") || e.target.closest("button")) {
            return;
        }
        if (e.key === " " || e.key === "ArrowLeft" || e.key === "ArrowRight" || e.key === "Home" || e.key === "End") {
            e.preventDefault();
        }
    };
    stripEl.addEventListener("keydown", onKeyDown);

    tabOverflowObservers.set(groupId, { observer: ro, stripEl, dotNetRef, onKeyDown });
    reportTabOverflow(groupId);
}

// 重测一条已被观察标签带的溢出（例如标签增删之后）。
export function remeasureTabOverflow(groupId) {
    reportTabOverflow(groupId);
}

export function disposeTabOverflow(groupId) {
    const entry = tabOverflowObservers.get(groupId);
    if (entry) {
        entry.observer.disconnect();
        entry.stripEl.removeEventListener("keydown", entry.onKeyDown);
        tabOverflowObservers.delete(groupId);
    }
}

function reportTabOverflow(groupId) {
    const entry = tabOverflowObservers.get(groupId);
    if (!entry) {
        return;
    }

    const ids = computeOverflowIds(entry.stripEl);
    entry.dotNetRef.invokeMethodAsync("OnTabOverflowChanged", ids).catch(() => { });
}

// 返回未能完全放入标签带可见宽度的标签 id，按顺序排列。
// 标签此刻可能因上一轮结果处于隐藏态（display:none），故逐个临时强制
// 可见进行测量、随后恢复原状——自然宽度保持稳定，
// 避免朴素测量会引发的隐藏/显示反馈回路。
function computeOverflowIds(stripEl) {
    if (!stripEl) {
        return [];
    }

    const tabs = Array.from(stripEl.querySelectorAll("[data-dock-tab]"));
    if (tabs.length === 0) {
        return [];
    }

    const saved = tabs.map((t) => t.style.display);
    for (const t of tabs) {
        t.style.display = "flex";
    }

    const available = stripEl.clientWidth;
    const overflow = [];
    let used = 0;
    for (const t of tabs) {
        used += t.offsetWidth;
        // 1px 容差吸收亚像素取整，恰好放得下的标签不会被隐藏。
        if (used > available + 1) {
            overflow.push(t.getAttribute("data-dock-tab"));
        }
    }

    for (let i = 0; i < tabs.length; i++) {
        tabs[i].style.display = saved[i];
    }

    return overflow;
}

// ---------------------------------------------------------------- 共享指针会话

// 为单一指针运行一次 document 级指针会话。其他指针的事件
// （第二根手指、鼠标拖拽期间来的触摸）被整体忽略，既不能提交
// 也不能拆毁会话。pointerup 经 handlers.up 提交；pointercancel
// 经 handlers.cancel 中止（不提交）。会话会在 dock 上登记一个取消
// 句柄，令 disposeDock 能中止进行中的手势。
function attachSession(dock, pointerId, handlers) {
    const onMove = (e) => {
        if (e.pointerId !== pointerId) {
            return;
        }
        handlers.move(e);
    };
    const onUp = (e) => {
        if (e.pointerId !== pointerId) {
            return;
        }
        detach();
        handlers.up(e);
    };
    const onCancel = (e) => {
        if (e.pointerId !== pointerId) {
            return;
        }
        cancel();
    };
    const detach = () => {
        document.removeEventListener("pointermove", onMove);
        document.removeEventListener("pointerup", onUp);
        document.removeEventListener("pointercancel", onCancel);
        document.body.style.userSelect = "";
        document.body.style.cursor = "";
        if (dock && dock.cancelSession === cancel) {
            dock.cancelSession = null;
        }
    };
    const cancel = () => {
        detach();
        if (handlers.cancel) {
            handlers.cancel();
        }
    };

    document.addEventListener("pointermove", onMove);
    document.addEventListener("pointerup", onUp);
    document.addEventListener("pointercancel", onCancel);
    document.body.style.userSelect = "none";

    if (dock) {
        dock.cancelSession = cancel;
    }
}

// ---------------------------------------------------------------- tab drag → docking

export function startTabDrag(dockId, title, clientX, clientY, pointerId) {
    const dock = docks.get(dockId);
    if (!dock) {
        return;
    }

    const state = {
        dock,
        title: title || "Panel",
        pointerId,
        startX: clientX,
        startY: clientY,
        active: false,
        ghost: null,
        indicator: null,
        target: null
    };

    attachSession(dock, pointerId, {
        move: (e) => onTabMove(state, e),
        up: (e) => onTabUp(state, e),
        cancel: () => abortTabDrag(state)
    });
}

// 被取消的手势（pointercancel，或 dock 在拖拽途中被释放）会中止拖拽：
// 幽灵与落点指示器被移除，且不向 .NET 上报任何落点。
function abortTabDrag(state) {
    removeEl(state.ghost);
    removeEl(state.indicator);
    state.ghost = null;
    state.indicator = null;
}

function onTabMove(state, e) {
    if (!state.active) {
        const dx = e.clientX - state.startX;
        const dy = e.clientY - state.startY;
        if (Math.hypot(dx, dy) < DRAG_THRESHOLD) {
            return;
        }
        state.active = true;
        state.ghost = createGhost(state.title);
        document.body.style.cursor = "default";
    }

    moveGhost(state.ghost, e.clientX, e.clientY);
    state.target = computeTarget(state.dock, e.clientX, e.clientY);
    drawIndicator(state, state.target);
}

function onTabUp(state, e) {
    removeEl(state.ghost);
    removeEl(state.indicator);
    state.ghost = null;
    state.indicator = null;

    if (!state.active) {
        // 位移未过阈值——视作点击而非拖拽。
        return;
    }

    const t = state.target || { type: "none" };
    let floatX = 0;
    let floatY = 0;

    if (t.type === "float") {
        const r = state.dock.rootEl.getBoundingClientRect();
        floatX = e.clientX - r.left - 24;
        floatY = e.clientY - r.top - 12;
    }

    state.dock.dotNetRef
        .invokeMethodAsync("OnTabDropped", t.type, t.groupId || null, t.zone || "center", floatX, floatY, typeof t.index === "number" ? t.index : -1)
        .catch(() => { });
}

/**
 * dock 是否从右向左排布面板。dock 的模型是有序的——zone 为 "left" 意为
 * "排在该面板之前"——而 dir="rtl" 下 flex 行会反序，故对指针的水平判断做
 * 镜像，让模型与屏幕呈现保持一致。
 */
function isRtl(dock) {
    return getComputedStyle(dock.rootEl).direction === "rtl";
}

function computeTarget(dock, x, y) {
    const r = dock.rootEl.getBoundingClientRect();
    const rtl = isRtl(dock);

    if (x < r.left || x > r.right || y < r.top || y > r.bottom) {
        return { type: "float" };
    }

    // Hovering a tab strip reorders within (or into) that group without changing the layout.
    const stripEl = findClosestAt(dock, x, y, "[data-dock-tabstrip]");
    if (stripEl) {
        return {
            type: "reorder",
            groupId: stripEl.getAttribute("data-dock-tabstrip"),
            index: computeInsertIndex(stripEl, x, rtl)
        };
    }

    // A band along the dock's outer border docks against the whole dock.
    const edge = 26;
    if (x - r.left < edge) {
        return { type: "root", zone: rtl ? "right" : "left" };
    }
    if (r.right - x < edge) {
        return { type: "root", zone: rtl ? "left" : "right" };
    }
    if (y - r.top < edge) {
        return { type: "root", zone: "top" };
    }
    if (r.bottom - y < edge) {
        return { type: "root", zone: "bottom" };
    }

    const groupEl = findGroupAt(dock, x, y);
    if (!groupEl) {
        return { type: "float" };
    }

    const gid = groupEl.getAttribute("data-dock-group");
    const zone = zoneWithin(groupEl.getBoundingClientRect(), x, y, rtl);
    return { type: "group", groupId: gid, zone };
}

function findGroupAt(dock, x, y) {
    return findClosestAt(dock, x, y, "[data-dock-group]");
}

function findClosestAt(dock, x, y, selector) {
    const els = document.elementsFromPoint(x, y);
    for (const el of els) {
        if (!el.closest) {
            continue;
        }
        const match = el.closest(selector);
        if (match && dock.rootEl.contains(match)) {
            return match;
        }
    }
    return null;
}

// 指针 x 落在标签带上的插入槽位（0..标签数），用每个标签的中点计算。
function computeInsertIndex(stripEl, x, rtl) {
    const tabs = stripEl.querySelectorAll("[data-dock-tab]");
    for (let i = 0; i < tabs.length; i++) {
        const tr = tabs[i].getBoundingClientRect();
        // 标签按阅读方向以模型顺序绘制，故在从右向左的标签带上，
        // 指针向左移动才抵达下一个标签。
        const past = rtl ? x > tr.left + tr.width / 2 : x < tr.left + tr.width / 2;
        if (past) {
            return i;
        }
    }
    return tabs.length;
}

function zoneWithin(r, x, y, rtl) {
    const rx = rtl ? (r.right - x) / r.width : (x - r.left) / r.width;
    const ry = (y - r.top) / r.height;
    const m = 0.22;
    const inMidX = rx > m && rx < 1 - m;
    const inMidY = ry > m && ry < 1 - m;

    if (rx < m && inMidY) {
        return "left";
    }
    if (rx > 1 - m && inMidY) {
        return "right";
    }
    if (ry < m && inMidX) {
        return "top";
    }
    if (ry > 1 - m && inMidX) {
        return "bottom";
    }
    return "center";
}

function drawIndicator(state, t) {
    if (!t || t.type === "none" || t.type === "float") {
        if (state.indicator) {
            state.indicator.style.display = "none";
        }
        return;
    }

    if (!state.indicator) {
        const ind = document.createElement("div");
        ind.style.cssText =
            "position:fixed;z-index:10001;pointer-events:none;border-radius:6px;box-sizing:border-box;" +
            "transition:left .07s ease,top .07s ease,width .07s ease,height .07s ease;";
        ind.style.background = ACCENT_BG;
        ind.style.border = ACCENT_BORDER;
        document.body.appendChild(ind);
        state.indicator = ind;
    }

    const ind = state.indicator;
    ind.style.display = "block";
    const rtl = isRtl(state.dock);

    let box;
    if (t.type === "reorder") {
        const stripEl = state.dock.rootEl.querySelector(`[data-dock-tabstrip="${t.groupId}"]`);
        if (!stripEl) {
            ind.style.display = "none";
            return;
        }
        box = insertionLineBox(stripEl, t.index, rtl);
        // 标签之间是一条实心插入线，而非半透明填充。
        ind.style.background = ACCENT_SOLID;
        ind.style.border = "none";
    } else {
        ind.style.background = ACCENT_BG;
        ind.style.border = ACCENT_BORDER;
        if (t.type === "root") {
            box = zoneBox(state.dock.rootEl.getBoundingClientRect(), t.zone, 0.3, rtl);
        } else {
            const gEl = state.dock.rootEl.querySelector(`[data-dock-group="${t.groupId}"]`);
            if (!gEl) {
                ind.style.display = "none";
                return;
            }
            box = zoneBox(gEl.getBoundingClientRect(), t.zone, 0.5, rtl);
        }
    }

    ind.style.left = `${box.left}px`;
    ind.style.top = `${box.top}px`;
    ind.style.width = `${box.width}px`;
    ind.style.height = `${box.height}px`;
}

function insertionLineBox(stripEl, index, rtl) {
    const tabs = stripEl.querySelectorAll("[data-dock-tab]");
    const sr = stripEl.getBoundingClientRect();
    // 插入线落在本次放置会向后推挤的那个标签的前沿，
    // 而从右向左阅读的标签带上前沿是右缘。
    const leading = el => rtl ? el.getBoundingClientRect().right : el.getBoundingClientRect().left;
    const trailing = el => rtl ? el.getBoundingClientRect().left : el.getBoundingClientRect().right;
    let lineX;
    if (tabs.length === 0) {
        lineX = rtl ? sr.right : sr.left;
    } else if (index >= tabs.length) {
        lineX = trailing(tabs[tabs.length - 1]);
    } else {
        lineX = leading(tabs[index]);
    }
    return { left: lineX - 1, top: sr.top, width: 2, height: sr.height };
}

function zoneBox(r, zone, frac, rtl) {
    // zone 名是模型顺序而非屏幕方位，故按阅读方向映射回屏幕方位——
    // 与 computeTarget 进入时施加的同一映射。
    const side = rtl && (zone === "left" || zone === "right")
        ? (zone === "left" ? "right" : "left")
        : zone;
    switch (side) {
        case "left":
            return { left: r.left, top: r.top, width: r.width * frac, height: r.height };
        case "right":
            return { left: r.right - r.width * frac, top: r.top, width: r.width * frac, height: r.height };
        case "top":
            return { left: r.left, top: r.top, width: r.width, height: r.height * frac };
        case "bottom":
            return { left: r.left, top: r.bottom - r.height * frac, width: r.width, height: r.height * frac };
        default:
            return { left: r.left, top: r.top, width: r.width, height: r.height };
    }
}

function createGhost(title) {
    const g = document.createElement("div");
    g.textContent = title;
    g.style.cssText =
        "position:fixed;z-index:10002;pointer-events:none;padding:4px 10px;font-size:12px;font-weight:500;" +
        "border-radius:6px;background:var(--background);color:var(--foreground);border:1px solid var(--border);" +
        "box-shadow:0 6px 20px rgba(0,0,0,0.28);opacity:.92;" +
        "white-space:nowrap;transform:translate(10px,10px);";
    document.body.appendChild(g);
    return g;
}

function moveGhost(g, x, y) {
    if (g) {
        g.style.left = `${x}px`;
        g.style.top = `${y}px`;
    }
}

function removeEl(el) {
    if (el && el.parentNode) {
        el.parentNode.removeChild(el);
    }
}

// ---------------------------------------------------------------- 浮动窗口移动

export function startWindowDrag(dockId, windowId, clientX, clientY, pointerId) {
    const dock = docks.get(dockId);
    if (!dock) {
        return;
    }

    const winEl = dock.rootEl.querySelector(`[data-dock-window="${windowId}"]`);
    if (!winEl) {
        return;
    }

    const rootRect = dock.rootEl.getBoundingClientRect();
    const startLeft = parseFloat(winEl.style.left) || 0;
    const startTop = parseFloat(winEl.style.top) || 0;
    const offsetX = clientX - (rootRect.left + startLeft);
    const offsetY = clientY - (rootRect.top + startTop);

    attachSession(dock, pointerId, {
        move: (e) => {
            const left = Math.max(0, e.clientX - rootRect.left - offsetX);
            const top = Math.max(0, e.clientY - rootRect.top - offsetY);
            winEl.style.left = `${left}px`;
            winEl.style.top = `${top}px`;
        },
        up: () => {
            const left = parseFloat(winEl.style.left) || 0;
            const top = parseFloat(winEl.style.top) || 0;
            dock.dotNetRef.invokeMethodAsync("OnWindowMoved", windowId, left, top).catch(() => { });
        },
        cancel: () => {
            // 被取消的手势：把窗口放回原处，不提交任何东西。
            winEl.style.left = `${startLeft}px`;
            winEl.style.top = `${startTop}px`;
        }
    });
    document.body.style.cursor = "move";
}

// ---------------------------------------------------------------- 浮动窗口缩放

// minWidth/minHeight 默认取可用窗口最小值；max 为 0（或缺省）表示
// “不设上限”。这些与 .NET 侧强制的每面板尺寸约束相互镜像。
export function startWindowResize(dockId, windowId, clientX, clientY, pointerId, minWidth, minHeight, maxWidth, maxHeight) {
    const dock = docks.get(dockId);
    if (!dock) {
        return;
    }

    const winEl = dock.rootEl.querySelector(`[data-dock-window="${windowId}"]`);
    if (!winEl) {
        return;
    }

    const minW = minWidth > 0 ? minWidth : 180;
    const minH = minHeight > 0 ? minHeight : 120;
    const maxW = maxWidth > 0 ? maxWidth : Infinity;
    const maxH = maxHeight > 0 ? maxHeight : Infinity;

    const startWidth = winEl.offsetWidth;
    const startHeight = winEl.offsetHeight;
    const startX = clientX;
    const startY = clientY;

    attachSession(dock, pointerId, {
        move: (e) => {
            const width = Math.min(maxW, Math.max(minW, startWidth + (e.clientX - startX)));
            const height = Math.min(maxH, Math.max(minH, startHeight + (e.clientY - startY)));
            winEl.style.width = `${width}px`;
            winEl.style.height = `${height}px`;
        },
        up: () => {
            dock.dotNetRef
                .invokeMethodAsync("OnWindowResized", windowId, winEl.offsetWidth, winEl.offsetHeight)
                .catch(() => { });
        },
        cancel: () => {
            // 被取消的手势：恢复原始尺寸，不提交任何东西。
            winEl.style.width = `${startWidth}px`;
            winEl.style.height = `${startHeight}px`;
        }
    });
    document.body.style.cursor = "nwse-resize";
}
