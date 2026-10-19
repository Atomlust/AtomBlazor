// 滑动手势检测：把指针/触摸事件换算成滑动，并上报给 Blazor。
// 手势数学全部留在浏览器里。每个手势 C# 至多听到一个终结事件，
// 外加（仅当组件挂了 OnSwipeMove 时）每 50ms 至多一条的节流位置更新。

import { createDragUpdates } from '../drag-updates.js';

const areaStates = new Map();

/**
 * 快速轻扫可以在不足距离阈值时提交，但点按不行：3px 的抖动除以 5ms
 * 读作 0.6 px/ms，能越过任何值得设置的阈值下限，所以速度通道自带一个
 * 距离下限。
 */
const FLICK_MIN_DISTANCE = 16;

/**
 * 速度按窗口内最旧的采样计算，而非整个手势，这样"手指走很远、抬指前先停一下"
 * 的手势不会上报它出发时的速度。
 */
const VELOCITY_WINDOW_MS = 100;

/**
 * 监视一个元素上的滑动手势并上报给 Blazor。
 *
 * 手势数学全部在这里跑。手指移动期间 C# 最多每 50ms 收到一条节流更新——
 * 而且只在组件有 OnSwipeMove 处理器时——外加恰好一次终结调用。
 * 在 Blazor Server 上，这就是"一小撮电路消息"与"每个 pointermove 一条"的差别。
 *
 * 各可调项在每个事件上从 data 特性读取，而不是在 initialize 时捕获，
 * 所以 C# 侧改参数无需第二次 interop 调用即可生效。
 *
 * @param {HTMLElement} element - 要监视的元素。
 * @param {DotNetObject} dotNetRef - SwipeArea 组件的对象引用。
 * @param {string} areaId - 本浮区的唯一标识。
 */
export function initialize(element, dotNetRef, areaId) {
    if (!element || !dotNetRef) return;
    dispose(areaId);

    let pointerId = null;
    let startX = 0;
    let startY = 0;
    let samples = [];
    let reportedMove = false;
    let disposed = false;

    const moves = createDragUpdates((x, y) => dotNetRef.invokeMethodAsync('JsSwipeMove', x, y));

    // 只拒绝解析不出来的值，让 Infinity 存活：它是 MinVelocity 关闭速度通道的方式，
    // Number.isFinite 会悄悄把它换成默认值。
    const attribute = (name, fallback) => {
        const raw = element.getAttribute(name);
        if (raw === null) return fallback;
        const value = Number(raw);
        return Number.isNaN(value) ? fallback : value;
    };
    const disabled = () => element.getAttribute('data-disabled') === 'true';
    const axis = () => element.getAttribute('data-axis') ?? 'both';
    const reportsMove = () => element.getAttribute('data-report-move') === 'true';

    const reset = () => {
        pointerId = null;
        samples = [];
        reportedMove = false;
    };

    const release = e => {
        if (element.hasPointerCapture(e.pointerId)) element.releasePointerCapture(e.pointerId);
    };

    /** 组件自己的轴锁定，未锁定时取手势占优的那条轴。 */
    const isHorizontal = (dx, dy) => {
        const locked = axis();
        if (locked === 'horizontal') return true;
        if (locked === 'vertical') return false;
        return Math.abs(dx) >= Math.abs(dy);
    };

    const down = e => {
        if (pointerId != null || disabled() || !e.isPrimary || e.button > 0) return;
        pointerId = e.pointerId;
        startX = e.clientX;
        startY = e.clientY;
        samples = [{ x: e.clientX, y: e.clientY, t: e.timeStamp }];
        reportedMove = false;
        // 指针捕获让手势在手指离开元素后依然存活——这正是"滑出边缘的手势仍然算数、
        // 不在边缘悄悄死掉"的原因。
        element.setPointerCapture(pointerId);
        moves.begin();
    };

    const move = e => {
        if (e.pointerId !== pointerId) return;
        samples.push({ x: e.clientX, y: e.clientY, t: e.timeStamp });
        // 保留仍在窗口之外的最旧样本，让窗口始终被覆盖而不是仅仅被接近。
        while (samples.length > 2 && e.timeStamp - samples[1].t >= VELOCITY_WINDOW_MS) samples.shift();
        if (!reportsMove()) return;
        reportedMove = true;
        moves.push([e.clientX - startX, e.clientY - startY]);
    };

    const up = async e => {
        if (e.pointerId !== pointerId) return;
        release(e);

        const dx = e.clientX - startX;
        const dy = e.clientY - startY;
        const horizontal = isHorizontal(dx, dy);
        const primary = horizontal ? dx : dy;
        const distance = Math.abs(primary);

        const oldest = samples[0] ?? { x: startX, y: startY, t: e.timeStamp };
        const elapsed = Math.max(1, e.timeStamp - oldest.t);
        const recent = horizontal ? e.clientX - oldest.x : e.clientY - oldest.y;
        const velocity = Math.abs(recent) / elapsed;

        const threshold = attribute('data-threshold', 50);
        const minVelocity = attribute('data-min-velocity', 0.5);
        const committed = distance >= threshold
            || (distance >= FLICK_MIN_DISTANCE && velocity >= minVelocity);
        const hadMove = reportedMove;

        reset();
        // 让最后一次移动更新先落地，这样跟随 OnSwipeMove 做动画的消费者
        // 不会看到一个陈旧偏移量晚于其所属手势结束之后才到达。
        await moves.flush();
        if (disposed) return;

        if (committed) {
            const direction = horizontal
                ? (primary < 0 ? 'Left' : 'Right')
                : (primary < 0 ? 'Up' : 'Down');
            dotNetRef.invokeMethodAsync('JsSwipeEnd', direction, dx, dy, distance, velocity);
        } else if (hadMove) {
            // 给拖动画了东西的消费者一个回弹信号。只监听完成滑动的人被告知无事发生，
            // 因为确实无事发生。
            dotNetRef.invokeMethodAsync('JsSwipeCancel');
        }
    };

    const abort = async e => {
        if (e.pointerId !== pointerId) return;
        release(e);
        reset();
        await moves.flush();
        if (!disposed) dotNetRef.invokeMethodAsync('JsSwipeCancel');
    };

    const listeners = {
        pointerdown: down,
        pointermove: move,
        pointerup: up,
        pointercancel: abort,
        lostpointercapture: abort
    };
    Object.entries(listeners).forEach(([event, handler]) => element.addEventListener(event, handler));

    areaStates.set(areaId, {
        cancel: () => {
            if (pointerId == null) return;
            if (element.hasPointerCapture(pointerId)) element.releasePointerCapture(pointerId);
            reset();
            moves.cancel();
        },
        dispose: () => {
            disposed = true;
            moves.dispose();
            if (pointerId != null && element.hasPointerCapture(pointerId)) {
                element.releasePointerCapture(pointerId);
            }
            Object.entries(listeners).forEach(([event, handler]) => element.removeEventListener(event, handler));
        }
    });
}

/**
 * 丢弃进行中的手势且不上报。没有手势在跑时什么也不做。
 * @param {string} areaId - 要取消的浮区。
 */
export function cancel(areaId) {
    areaStates.get(areaId)?.cancel();
}

/**
 * 移除某个浮区的全部监听器。
 * @param {string} areaId - 要释放的浮区。
 */
export function dispose(areaId) {
    areaStates.get(areaId)?.dispose();
    areaStates.delete(areaId);
}
