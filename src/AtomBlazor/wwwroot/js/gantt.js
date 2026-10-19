// 横条手势留在浏览器内。唯有完成的手势才越过 Blazor 电路，因为
// 在服务器电路上逐帧发 pointermove，得到的只会是跟不上指针的横条。
const instances = new WeakMap();

/** @returns {boolean} 图表是否自右向左阅排。 */
function isRtl(root) {
    return getComputedStyle(root).direction === 'rtl';
}

/**
 * 指针沿时间轴行过的距离，以时间流向为准。
 * @param {HTMLElement} root - 图表的滚动容器。
 * @param {number} from - 手势起点，客户区像素。
 * @param {number} to - 指针现处，客户区像素。
 * @returns {number} 距离（像素），向晚为正。
 */
function travelled(root, from, to) {
    return isRtl(root) ? from - to : to - from;
}

/**
 * 滚动时间轴，使某一内容位置居于可见时间轴正中。
 * @param {HTMLElement} root - 滚动容器。
 * @param {number} x - 位置，自内容起始缘量起。
 * @param {number} stickyWidth - 任务列表占据视口的宽度。
 */
export function scrollTo(root, x, stickyWidth) {
    if (!root) return;
    // 任务列表覆于最初 stickyWidth 像素之上，故读者实际可见的
    // 时间轴恰比容器窄出那么多。
    const visible = Math.max(0, root.clientWidth - stickyWidth);
    const target = Math.max(0, x - stickyWidth - visible / 2);
    // 在从右至左的盒中，背离起始缘滚动取负值。
    root.scrollLeft = isRtl(root) ? -target : target;
}

/**
 * 接线拖拽、缩放、进度与依赖绘制。
 * @param {HTMLElement} root - 图表的滚动容器。
 * @param {object} dotNet - Blazor 组件引用。
 */
export function initialize(root, dotNet) {
    if (!root || !dotNet) return;
    if (instances.has(root)) {
        instances.get(root).dotNet = dotNet;
        return;
    }

    const state = { dotNet, gesture: null, suppressClickUntil: 0 };
    instances.set(root, state);

    const allowed = action => root.dataset[{
        move: 'allowDrag',
        start: 'allowResize',
        end: 'allowResize',
        progress: 'allowProgress',
        link: 'allowLink',
    }[action]] === 'true';

    const restore = gesture => {
        gesture.bar.style.insetInlineStart = gesture.inlineStart;
        gesture.bar.style.width = gesture.width;
        if (gesture.fill) gesture.fill.style.width = gesture.fillWidth;
        gesture.preview?.remove();
    };

    const slotsMoved = (gesture, clientX) => {
        const slotWidth = Number(root.dataset.slotWidth) || 1;
        const raw = travelled(root, gesture.startX, clientX) / slotWidth;
        return root.dataset.snap === 'true' ? Math.round(raw) : raw;
    };

    // 指针在横条上的位置，自任务起始那一端量起。
    const fractionAt = (bar, clientX) => {
        const rect = bar.getBoundingClientRect();
        if (rect.width <= 0) return 0;
        const along = isRtl(root) ? rect.right - clientX : clientX - rect.left;
        return Math.min(1, Math.max(0, along / rect.width));
    };

    // ── 行拖拽 ─────────────────────────────────────────────────────────────

    const dropAt = (event) => {
        const hit = document.elementFromPoint(event.clientX, event.clientY);
        const cell = hit?.closest('[data-gantt-row]');
        if (!cell || !root.contains(cell)) return null;

        // 上、下四分之一段重排序，中段改换父级。取四分之一而非三分之
        // 一，因为多数落点意在「放这里」，而非"放进去"。
        const rect = cell.getBoundingClientRect();
        const share = (event.clientY - rect.top) / rect.height;
        const position = share < 0.25 ? 'before' : share > 0.75 ? 'after' : 'inside';
        return { id: cell.dataset.ganttRow, cell, position };
    };

    const showDrop = drop => {
        const box = root.querySelector('table')?.parentElement;
        if (!box) return;

        let mark = state.indicator;
        if (!mark) {
            mark = document.createElement('div');
            mark.dataset.ganttDrop = '';
            Object.assign(mark.style, { position: 'absolute', pointerEvents: 'none', zIndex: '50' });
            box.append(mark);
            state.indicator = mark;
        }

        if (!drop) {
            mark.style.display = 'none';
            return;
        }

        const rect = drop.cell.getBoundingClientRect();
        const origin = box.getBoundingClientRect();
        const top = rect.top - origin.top;
        Object.assign(mark.style, {
            display: '',
            insetInlineStart: '0',
            // 内容盒恰为图表之宽，故标记贯通整行，
            // 而不会停在任务列表边缘。
            width: '100%',
            top: drop.position === 'after' ? `${top + rect.height - 1}px` : `${top}px`,
            height: drop.position === 'inside' ? `${rect.height}px` : '2px',
            background: drop.position === 'inside' ? 'color-mix(in oklab, var(--primary) 18%, transparent)' : 'var(--primary)',
            border: drop.position === 'inside' ? '1px solid var(--primary)' : 'none',
            borderRadius: drop.position === 'inside' ? '3px' : '0',
        });
    };

    const clearDrop = () => {
        state.indicator?.remove();
        state.indicator = null;
    };

    const onDown = event => {
        if (event.button !== 0 || state.gesture) return;

        const handle = event.target.closest('[data-gantt-row-handle]');
        if (handle && root.dataset.allowRowDrag === 'true') {
            const cell = handle.closest('[data-gantt-row]');
            if (!cell) return;
            event.preventDefault();
            state.gesture = { action: 'row', id: cell.dataset.ganttRow, pointerId: event.pointerId, moved: false };
            try { handle.setPointerCapture(event.pointerId); } catch { /* 指针捕获只是锦上添花 */ }
            state.handle = handle;
            return;
        }

        const bar = event.target.closest('[data-gantt-bar]');
        if (!bar || !root.contains(bar)) return;

        const connector = event.target.closest('[data-gantt-link]');
        const action = connector
            ? 'link'
            : event.target.closest('[data-gantt-progress]')
                ? 'progress'
                : event.target.closest('[data-gantt-resize]')?.dataset.ganttResize ?? 'move';

        if (!allowed(action)) return;

        event.preventDefault();

        const fill = bar.querySelector('[data-gantt-fill]');
        state.gesture = {
            bar,
            fill,
            action,
            anchor: connector?.dataset.ganttLink,
            id: bar.dataset.ganttBar,
            pointerId: event.pointerId,
            startX: event.clientX,
            // Blazor 写就的行内样式原样。被拒的手势须把它们
            // 一字不动地放回去：Blazor 只补它认为变了的东西，而它不知道
            // 本文件动过任何东西。
            inlineStart: bar.style.insetInlineStart,
            width: bar.style.width,
            fillWidth: fill ? fill.style.width : '',
            originalStart: parseFloat(bar.style.insetInlineStart) || 0,
            originalWidth: parseFloat(bar.style.width) || 0,
            moved: false,
        };

        try { bar.setPointerCapture(event.pointerId); } catch { /* 指针捕获只是锦上添花 */ }
    };

    // 自连接点到指针的直线，画在自己的遮层里、用客户区
    // 坐标。箭头本身在从右至左页中是镜像的，共享那套
    // 坐标系的预览便会走反方向。
    const drawPreview = (gesture, clientX, clientY) => {
        const layer = gesture.bar.parentElement;
        if (!gesture.preview) {
            const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
            svg.setAttribute('aria-hidden', 'true');
            svg.dataset.ganttPreview = '';
            Object.assign(svg.style, {
                position: 'absolute', inset: '0', width: '100%', height: '100%',
                pointerEvents: 'none', overflow: 'visible', direction: 'ltr',
            });
            const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            line.setAttribute('stroke', 'currentColor');
            line.setAttribute('stroke-width', '1.5');
            line.setAttribute('stroke-dasharray', '4 3');
            svg.append(line);
            layer.append(svg);
            gesture.preview = svg;
        }

        const box = layer.getBoundingClientRect();
        const from = gesture.bar.getBoundingClientRect();
        const line = gesture.preview.firstElementChild;
        line.setAttribute('x1', (gesture.anchor === 'start' ? from.left : from.right) - box.left);
        line.setAttribute('y1', from.top + from.height / 2 - box.top);
        line.setAttribute('x2', clientX - box.left);
        line.setAttribute('y2', clientY - box.top);
    };

    const onMove = event => {
        const gesture = state.gesture;
        if (!gesture || event.pointerId !== gesture.pointerId) return;

        if (gesture.action === 'row') {
            gesture.moved = true;
            const drop = dropAt(event);
            gesture.drop = drop && drop.id !== gesture.id ? drop : null;
            showDrop(gesture.drop);
            return;
        }

        if (event.clientX !== gesture.startX) gesture.moved = true;

        if (gesture.action === 'link') {
            drawPreview(gesture, event.clientX, event.clientY);
            return;
        }

        if (gesture.action === 'progress') {
            if (gesture.fill) gesture.fill.style.width = `${fractionAt(gesture.bar, event.clientX) * 100}%`;
            return;
        }

        const slotWidth = Number(root.dataset.slotWidth) || 1;
        const offset = slotsMoved(gesture, event.clientX) * slotWidth;

        if (gesture.action === 'move') {
            gesture.bar.style.insetInlineStart = `${gesture.originalStart + offset}px`;
        } else if (gesture.action === 'start') {
            // 绝不越过远端：拖成里外倒置的横条便是结束早于开始的任务，
            // 读者便看不出自己请求的是什么。
            const width = Math.max(2, gesture.originalWidth - offset);
            gesture.bar.style.insetInlineStart = `${gesture.originalStart + gesture.originalWidth - width}px`;
            gesture.bar.style.width = `${width}px`;
        } else {
            gesture.bar.style.width = `${Math.max(2, gesture.originalWidth + offset)}px`;
        }
    };

    const onUp = event => {
        const gesture = state.gesture;
        if (!gesture || event.pointerId !== gesture.pointerId) return;

        if (gesture.action === 'row') {
            state.gesture = null;
            try { state.handle?.releasePointerCapture(event.pointerId); } catch { /* 已不在 */ }
            state.handle = null;
            const drop = gesture.drop;
            clearDrop();
            if (!gesture.moved || !drop) return;
            state.suppressClickUntil = Date.now() + 300;
            state.dotNet
                .invokeMethodAsync('OnRowDropped', gesture.id, drop.id, drop.position)
                .catch(() => { /* 组件可能已销毁 */ });
            return;
        }

        state.gesture = null;
        try { gesture.bar.releasePointerCapture(event.pointerId); } catch { /* 已不在 */ }

        const target = gesture.action === 'link' ? barUnder(event) : null;
        restore(gesture);

        if (!gesture.moved) return;

        // 终于横条之上的手势若不拦，还会被读成对该条的一次点击。
        state.suppressClickUntil = Date.now() + 300;

        const failed = () => { /* 组件可能已销毁 */ };

        if (gesture.action === 'link') {
            if (!target || target.dataset.ganttBar === gesture.id) return;
            // 落在横条前半即其起始端，落在后半即其结束端。
            const toAnchor = fractionAt(target, event.clientX) < 0.5 ? 'start' : 'end';
            state.dotNet
                .invokeMethodAsync('OnLinkDrawn', gesture.id, gesture.anchor, target.dataset.ganttBar, toAnchor)
                .catch(failed);
            return;
        }

        if (gesture.action === 'progress') {
            state.dotNet
                .invokeMethodAsync('OnProgressDragged', gesture.id, fractionAt(gesture.bar, event.clientX))
                .catch(failed);
            return;
        }

        state.dotNet
            .invokeMethodAsync('OnBarDragged', gesture.id, gesture.action, slotsMoved(gesture, event.clientX))
            .catch(failed);
    };

    // 指针捕获把所有 move 都留在源横条上，故落点目标须问
    // 指针之下是什么来寻得，而非读事件自身的 target。
    const barUnder = event => {
        const hit = document.elementFromPoint(event.clientX, event.clientY);
        const bar = hit?.closest('[data-gantt-bar]');
        return bar && root.contains(bar) ? bar : null;
    };

    const onCancel = () => {
        if (!state.gesture) return;
        if (state.gesture.action === 'row') {
            clearDrop();
        } else {
            restore(state.gesture);
        }

        state.gesture = null;
    };

    const onClick = event => {
        if (Date.now() < state.suppressClickUntil) {
            event.stopPropagation();
            event.preventDefault();
        }
    };

    root.addEventListener('pointerdown', onDown);
    root.addEventListener('pointermove', onMove);
    root.addEventListener('pointerup', onUp);
    root.addEventListener('pointercancel', onCancel);
    root.addEventListener('click', onClick, true);

    state.teardown = () => {
        clearDrop();
        root.removeEventListener('pointerdown', onDown);
        root.removeEventListener('pointermove', onMove);
        root.removeEventListener('pointerup', onUp);
        root.removeEventListener('pointercancel', onCancel);
        root.removeEventListener('click', onClick, true);
    };
}

/**
 * 卸下一张图表的处理器。
 * @param {HTMLElement} root - 图表的滚动容器。
 */
export function dispose(root) {
    const state = root && instances.get(root);
    if (!state) return;
    state.teardown?.();
    instances.delete(root);
}
