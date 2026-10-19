// 基于 Floating UI（本地 vendored）的定位服务

// Floating UI 刻意采用静态导入。它过去写在 loadFloatingUI() 里用 `await import(...)` 动态加载，
// 这意味着第一次 computePosition 调用要多付一次模块加载，而 C# 侧根本看不见它：
// 在 Blazor Server 上，C# 对本文件的 `import` 已经是一次电路往返，嵌套导入又在上面叠了一次等待。
// 改为静态导入后，浏览器在取回本文件时就自行解析整个导入图，一对文件共一次往返而不是两次。
import * as floatingUIBundled from '../vendor/floating-ui-dom.esm.min.js';

// 清理函数存放在以唯一 ID 索引的注册表里，避免把函数本身穿过 JS interop
const cleanupRegistry = new Map();

/**
 * 返回 Floating UI；宿主页面若预加载了全局副本则优先使用。
 * 不依赖任何外部 CDN——库随包分发。
 */
function loadFloatingUI() {
    // 先找预加载的全局（来自 App.razor 的 script）
    return window.FloatingUIDOM || floatingUIBundled;
}

/**
 * 检查元素是否已就绪、可以定位（存在于 DOM 且有效）。
 * @param {HTMLElement} element - 待检查的元素
 * @returns {boolean} 就绪则为 true
 */
export function isElementReady(element) {
    return element &&
           element instanceof Element &&
           document.body.contains(element);
}

/**
 * 等待元素在 DOM 中就绪。
 * @param {string} elementId - 要等待的元素 ID
 * @param {number} maxWaitMs - 最长等待毫秒数
 * @param {number} intervalMs - 轮询间隔毫秒数
 * @returns {Promise<boolean>} 找到返回 true，否则 false
 */
export async function waitForElement(elementId, maxWaitMs = 100, intervalMs = 10) {
    const startTime = Date.now();

    while (Date.now() - startTime < maxWaitMs) {
        const element = document.getElementById(elementId);
        if (element && document.body.contains(element)) {
            return true;
        }
        await new Promise(resolve => setTimeout(resolve, intervalMs));
    }

    return false;
}

/**
 * 计算浮动元素的最优位置。
 * @param {HTMLElement} reference - 参照元素
 * @param {HTMLElement} floating - 待定位的浮动元素
 * @param {Object} options - 定位选项
 * @returns {Promise<Object>} 含 x、y、placement 的位置结果
 */
export async function computePosition(reference, floating, options = {}) {
    // 先校验两个元素再继续
    if (!isElementReady(reference)) {
        throw new Error('Reference element is not ready or not in DOM');
    }
    if (!isElementReady(floating)) {
        throw new Error('Floating element is not ready or not in DOM');
    }

    try {
        const lib = loadFloatingUI();

    const {
        placement = 'bottom',
        offset: offsetValue = 8,
        flip = true,
        shift = true,
        padding = 8,
        strategy = 'absolute',
        matchReferenceWidth = false
    } = options;

    const middleware = [
        lib.offset(offsetValue)
    ];

    if (flip) {
        middleware.push(lib.flip({ padding }));
    }

    if (shift) {
        middleware.push(lib.shift({ padding }));
    }

    // 提供了 arrow 元素时加入 arrow 中间件
    if (options.arrow) {
        middleware.push(lib.arrow({ element: options.arrow }));
    }

    // 加入 size 中间件，让浮动元素宽度与参照元素一致
    if (matchReferenceWidth) {
        middleware.push(lib.size({
            apply({ rects, elements }) {
                Object.assign(elements.floating.style, {
                    width: `${rects.reference.width}px`,
                    minWidth: `${rects.reference.width}px`,
                    maxWidth: `${rects.reference.width}px`
                });
            }
        }));
    }

    const result = await lib.computePosition(reference, floating, {
        placement,
        middleware,
        strategy
    });

        return {
            x: result.x,
            y: result.y,
            placement: result.placement,
            transformOrigin: getTransformOrigin(result.placement),
            strategy: result.strategy || strategy
        };
    } catch (error) {
        console.error('Failed to compute position:', error);
        throw error;  // 原样抛回，交给调用方处理
    }
}

/**
 * 把算好的位置应用到浮动元素上。
 * @param {HTMLElement} floating - 浮动元素
 * @param {Object} position - computePosition 产出的位置结果
 * @param {boolean} makeVisible - 定位完成后是否把元素设为可见
 */
export function applyPosition(floating, position, makeVisible = false) {
    if (!floating || !position) return;

    // 所有定位样式一次性写入，防止闪动。
    // fixed 策略用更高的 z-index（嵌套下拉），确保它们盖在父级浮层之上。
    const zIndex = position.strategy === 'fixed' ? '9999' : '50';
    Object.assign(floating.style, {
        position: position.strategy || 'absolute',
        left: `${position.x}px`,
        top: `${position.y}px`,
        zIndex: zIndex,
        transformOrigin: position.transformOrigin || ''
    });

    // makeVisible 为 true 时，定位完成后把元素显示出来。
    // 把可见性相关的属性全部设置到位，确保元素完全可见。
    if (makeVisible) {
        // 用 requestAnimationFrame 保证位置先于可见性生效
        requestAnimationFrame(() => {
            // 用带 'important' 的 setProperty 压过任何 CSS 动画/过渡
            floating.style.setProperty('visibility', 'visible', 'important');
            floating.style.setProperty('opacity', '1', 'important');
            floating.style.setProperty('pointer-events', 'auto', 'important');

            // 派发事件，宣告元素已完成定位并可见
            floating.dispatchEvent(new CustomEvent('atom:visible', { bubbles: true }));
        });
    }
}

/**
 * 为动态定位建立自动更新。
 * @param {HTMLElement} reference - 参照元素
 * @param {HTMLElement} floating - 浮动元素
 * @param {Object} options - 定位选项
 * @returns {Object} 带 id 与 apply() 清理方法的可释放对象
 */
/**
 * 让浮动元素回到隐藏的离屏状态。
 *
 * 与 applyPosition(..., makeVisible = true) 互为镜像。那个调用会把位置、可见性、透明度与
 * 指针事件直接写的元素上——其中好几个带 `!important`——从而把 style 属性的所有权从 Blazor
 * 手里拿走。Blazor 的 diff 对照的是它上次*渲染*出的内容而非 DOM 里的实际内容，所以关闭时
 * 若组件的标记与打开前逐字节一致，就不会发出任何 style 更新，这里写下的可见值将永远留存。
 *
 * 与 FloatingPortal.GetParkedStyle() 的隐藏样式保持一致。
 *
 * 会先等退场动画放完。浮层内容带着 `data-[state=closed]:animate-out` 类，
 * 而状态一翻转就立刻写 `visibility: hidden`，动画就没有可以播放的时间窗——
 * 这些动画声明在带样式的层里，却从未真正被看过一眼。
 *
 * 无论如何 pointer-events 都即刻掐死，正在退场的元素休想吞掉一次点击。
 *
 * @param {HTMLElement} floating - 要隐藏的浮动元素。
 */
export async function hidePosition(floating) {
    if (!floating) return;

    // 立即生效、绝不延后：正在关闭的浮层必须马上停止截获输入。
    floating.style.setProperty('pointer-events', 'none', 'important');

    await waitForExitAnimation(floating);

    // 动画长到足以让用户在其中重新打开，而重新打开会在本函数恢复之前重新写入可见样式。
    // 此时再隐藏，就会把用户刚要的浮层抹掉。
    if (floating.dataset.state === 'open') return;

    floating.style.setProperty('visibility', 'hidden', 'important');
    floating.style.setProperty('opacity', '0', 'important');
    floating.style.top = '-9999px';
    floating.style.left = '-9999px';
}

/** 退场动画最长可挽留元素可见的时间，超时后无论如何都隐藏。 */
const EXIT_ANIMATION_TIMEOUT_MS = 1000;

/**
 * 在元素的退场动画播完后 resolve；没有动画则立即 resolve。
 *
 * @param {HTMLElement} el - 要等待其动画的元素。
 */
async function waitForExitAnimation(el) {
    // 浏览器处理完启动动画的类与属性变化之前，getAnimations 什么都报不出来，
    // 所以先给它一帧的时间。
    await new Promise(resolve => requestAnimationFrame(() => resolve()));

    if (typeof el.getAnimations !== 'function') return;

    // 退场动画属于包装层或它的直接内容元素。后代过渡（树形箭头、悬停行、复选框）
    // 可能比退场淡出活得久，加载指示器也可能。等它们的话，已完成的淡出会回弹到
    // 完全不透明，把弹窗短暂地再露出来一次，然后才被我们藏掉。
    const running = el.getAnimations({ subtree: true }).filter(a => {
        if (a.playState !== 'running') return false;
        const target = a.effect?.target;
        if (target !== el && !(target?.parentElement === el && target.dataset.state === 'closed')) return false;
        const iterations = a.effect?.getTiming?.().iterations;
        return iterations !== Infinity;
    });
    if (running.length === 0) return;

    // 用 allSettled 而不是 all：被取消的动画会 reject，而退场动画被取消同样意味着
    // 我们等完了。超时是动画永无休止（无限循环、元素被暂停）时的兜底——
    // 没有它浮层会一直留在屏幕上。
    await Promise.race([
        Promise.allSettled(running.map(a => a.finished)),
        new Promise(resolve => setTimeout(resolve, EXIT_ANIMATION_TIMEOUT_MS)),
    ]);
}

export async function autoUpdate(reference, floating, options = {}) {
    try {
        const lib = loadFloatingUI();

    let lastPlacement = null;

    const update = async () => {
        // 防范陈旧元素——autoUpdate 的监听器（scroll、resize、intersection）
        // 可能在 Blazor 把元素移出 DOM 之后、清理函数尚未执行之前触发。静默退出。
        if (!isElementReady(reference) || !isElementReady(floating)) {
            return;
        }
        const position = await computePosition(reference, floating, options);
        applyPosition(floating, position);

        // 滚动可能把浮层翻转到另一侧，而方位驱动着它的样式。调用方不必再发起 interop
        // 也能保持自己写入的值同步。
        if (position.placement !== lastPlacement) {
            lastPlacement = position.placement;
            if (typeof options.onPlacement === 'function') {
                options.onPlacement(position.placement);
            }
        }
    };

    // 首次定位
    await update();

        // 建立自动更新——它返回一个清理函数
        const cleanupFunc = lib.autoUpdate(reference, floating, update);

        // 把清理函数以唯一 ID 存入注册表
        const id = crypto.randomUUID();
        cleanupRegistry.set(id, cleanupFunc);

        // 返回带 ID 的可释放对象（对象里不含函数）
        return {
            _cleanupId: id,
            apply: function() {
                const cleanup = cleanupRegistry.get(this._cleanupId);
                if (cleanup) {
                    cleanup();
                    cleanupRegistry.delete(this._cleanupId);
                }
            }
        };
    } catch (error) {
        console.error('Failed to set up auto-update:', error);
        throw error;  // 原样抛回，交给调用方处理
    }
}

/**
 * 按方位返回供动画使用的 transform-origin。
 * @param {string} placement - 方位取值
 * @returns {string} CSS transform-origin 值
 */
function getTransformOrigin(placement) {
    const placements = {
        'top': 'bottom center',
        'top-start': 'bottom left',
        'top-end': 'bottom right',
        'bottom': 'top center',
        'bottom-start': 'top left',
        'bottom-end': 'top right',
        'left': 'center right',
        'left-start': 'top right',
        'left-end': 'bottom right',
        'right': 'center left',
        'right-start': 'top left',
        'right-end': 'bottom left'
    };

    return placements[placement] || 'center';
}

/**
 * 用元素 ID 建立定位。等待元素出现在 DOM 后才开始定位，
 * 把 C# 的时序与门户渲染解耦——所有等待都由 JS 承担。
 * @param {string} referenceId - 参照（触发）元素的 ID
 * @param {string} floatingId - 浮动（内容）元素的 ID
 * @param {Object} options - 定位选项（placement、offset、flip、shift、padding、strategy、maxWaitMs）
 * @returns {Promise<Object>} 带清理方法的可释放对象；元素找不到时返回 no-op
 */
export async function setupPositioningById(referenceId, floatingId, options = {}) {
    const maxWaitMs = options.maxWaitMs || 200;

    // 等两个元素都出现在 DOM 里
    const [refReady, floatReady] = await Promise.all([
        waitForElement(referenceId, maxWaitMs),
        waitForElement(floatingId, maxWaitMs)
    ]);

    if (!refReady || !floatReady) {
        console.warn(`setupPositioningById: Elements not found within ${maxWaitMs}ms - ref: ${refReady}, float: ${floatReady}`);
        return {
            _cleanupId: -1,
            apply: function() {}
        };
    }

    const reference = document.getElementById(referenceId);
    const floating = document.getElementById(floatingId);

    // 复用现有的 autoUpdate 逻辑
    return await autoUpdate(reference, floating, options);
}

/**
 * 按 ID 聚焦元素，必要时等待它出现在 DOM 里。
 * @param {string} elementId - 要聚焦的元素 ID
 * @param {number} maxWaitMs - 最长等待毫秒数
 * @returns {Promise<boolean>} 聚焦成功返回 true，否则 false
 */
export async function focusById(elementId, maxWaitMs = 200) {
    const ready = await waitForElement(elementId, maxWaitMs);
    if (!ready) {
        return false;
    }

    const element = document.getElementById(elementId);
    if (element) {
        try {
            element.focus();
            return true;
        } catch (e) {
            console.warn(`focusById: Could not focus element ${elementId}:`, e);
            return false;
        }
    }
    return false;
}
