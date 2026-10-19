/**
 * 元素级通用 DOM 操作工具
 * 提供可复用函数，替代 eval() 调用
 */

/**
 * 通过设置 opacity 与 pointer-events 显示元素。
 * 定位初始化失败时作为兜底手段。
 * @param {string} elementId - 要显示的元素 id
 */
export function showElement(elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.style.opacity = '1';
        element.style.pointerEvents = 'auto';
    }
}

/**
 * 将元素滚入可视区，行为可配置。
 * @param {string} elementId - 要滚入视野的元素 id
 * @param {string} block - 块对齐方式（'nearest'、'start'、'center'、'end'）
 * @param {string} behavior - 滚动行为（'instant'、'smooth'、'auto'）
 */
export function scrollIntoView(elementId, block = 'nearest', behavior = 'instant') {
    const element = document.getElementById(elementId);
    if (element) {
        element.scrollIntoView({
            block: block,
            behavior: behavior
        });
    }
}

/**
 * 按 id 聚焦元素。
 * @param {string} elementId - 要聚焦的元素 id
 */
export async function focusElement(elementId) {
    const element = document.getElementById(elementId);
    // Portal 内容可能在异步定位展示之前就已存在。聚焦一个隐藏元素会静默失败
    // 导致嵌套的日历永远够不着。这里在本地等待元素现身，不再额外加 .NET 往返。
    for (let frame = 0; element && element.isConnected && frame < 30; frame++) {
        const style = window.getComputedStyle(element);
        if (style.visibility !== 'hidden' && style.display !== 'none' && element.getClientRects().length > 0) {
            element.focus();
            return;
        }
        await new Promise(resolve => requestAnimationFrame(resolve));
    }
}

/**
 * 当可滚动容器距底边不超过 `threshold` 像素时返回 true。
 * flex 列布局和多列 CSS 网格都适用：测量对象是*滚动容器*本身，而不是单个条目。
 * * @param {HTMLElement} element - 可滚动容器元素。
 * @param {number} threshold - 距底边多少像素算「接近底部」（默认 80）。
 * @returns {boolean}
 */
export function isNearBottom(element, threshold = 80) {
    if (!element) return false;
    return element.scrollTop + element.clientHeight >= element.scrollHeight - threshold;
}

// ============================================================================
// 近底部观察
//
// 无限滚动列表过去绑 @onscroll，再在 C# 端 await isNearBottom。在 Blazor Server 上
// 每个滚动事件都是一条 circuit 消息——滚动时每秒约六十条——而且每条还要再等一个
// 往返拿答复，才能决定是否加载下一页。每次滚动期间整条管道都被占满，每个列表都如此。
//
// 「到底部附近了吗」浏览器自己就能回答。这里用被动的 scroll 监听观察元素
// 按动画帧合并成每帧一次检查，进入底部区域时恰好回调 .NET 一次。位置离开底部区域时
// 重新武装，内容长高时（也就是加载完成的表象）也重新武装——所以一直往下拉的用户
// 会持续加载，而只是停在原地的用户不会猛敲服务器。
// ============================================================================

/** 观察器 id -> 摘除函数 */
const nearBottomObservers = new Map();
let nextNearBottomId = 1;

/**
 * 元素每次滚入近底部区域时，回调 .NET 恰好一次。
 * * @param {HTMLElement} element - 滚动容器。
 * @param {Object} dotNetRef - 要通知的组件。
 * @param {string} methodName - 要调用的 [JSInvokable] 方法名，不带参数。
 * @param {number} threshold - 距底部多少像素算「接近」。
 * @returns {number} 供 unobserveNearBottom 使用的句柄；什么都没挂则返回 0。
 */
export function observeNearBottom(element, dotNetRef, methodName = 'JsOnNearBottom', threshold = 80) {
    if (!element || !dotNetRef) return 0;

    const id = nextNearBottomId++;
    let armed = true;
    let lastHeight = element.scrollHeight;
    let scheduled = false;

    const check = () => {
        scheduled = false;

        // 内容长高了：说明一页已落地，那么下一次到底就是新的请求。
        if (element.scrollHeight !== lastHeight) {
            lastHeight = element.scrollHeight;
            armed = true;
        }

        if (isNearBottom(element, threshold)) {
            if (armed) {
                armed = false;
                dotNetRef.invokeMethodAsync(methodName).catch(() => {
                    // circuit 断了，或组件随之而去，没有可汇报的对象。
                });
            }
        } else {
            armed = true;
        }
    };

    const onScroll = () => {
        if (!scheduled) {
            scheduled = true;
            requestAnimationFrame(check);
        }
    };

    element.addEventListener('scroll', onScroll, { passive: true });
    nearBottomObservers.set(id, () => element.removeEventListener('scroll', onScroll));
    return id;
}

/**
 * 停止一个近底部观察器。
 * * @param {number} id - observeNearBottom 返回的句柄。
 */
export function unobserveNearBottom(id) {
    const detach = nearBottomObservers.get(id);
    if (detach) {
        nearBottomObservers.delete(id);
        detach();
    }
}

// ============================================================================
// 悬停观察
//
// 命令面板或 combobox 的条目过去把 @onmouseenter 和 @onmousemove 绑到服务器。
// 在 Blazor Server 上，指针每移动一像素都是一条 circuit 消息，而且被扫过的每一项
// 都会重渲染整个列表。mousemove 只为回答一个问题：指针是真的动了，还是键盘导航时
// 列表从静止的指针底下自己挪了？滚动会让指针下方的新元素收到 mouseover，而照单全收
// 会把高亮从方向键手里抢走。
//
// 这个问题浏览器自己就能回答。每个列表只挂一层委托监听：mousemove 置标志位
// mouseover 消费标志位。上一次没有 mousemove 的 mouseover 是列表在动而不是指针在动
// 直接忽略。只有真正换了条目才回调 .NET——每个条目一次，带上元素 id。
// ============================================================================

/** 观察器 id -> 摘除函数 */
const hoverObservers = new Map();
let nextHoverId = 1;

/**
 * 指针移动到另一个条目、且确系指针自己移动过去时，回调 .NET。
 * * @param {HTMLElement} container - 包含条目的列表元素。
 * @param {Object} dotNetRef - 要通知的组件。
 * @param {string} methodName - 要调用的 [JSInvokable] 方法名，接收条目 id。
 * @param {string} itemSelector - 匹配条目的 CSS 选择器。
 * @returns {number} 供 unobserveHover 使用的句柄；什么都没挂则返回 0。
 */
export function observeHover(container, dotNetRef, methodName = 'JsOnHover', itemSelector = '[role="option"]') {
    if (!container || !dotNetRef) return 0;

    const id = nextHoverId++;
    let moved = false;
    let lastItem = null;

    const onMove = () => { moved = true; };

    const onOver = (e) => {
        const item = e.target instanceof Element ? e.target.closest(itemSelector) : null;
        if (!item || !container.contains(item)) return;

        // 列表从静止的指针底下移动了。这不是悬停。
        if (!moved) return;
        moved = false;

        if (item === lastItem) return;
        lastItem = item;

        if (item.getAttribute('data-disabled') === 'true' || item.getAttribute('aria-disabled') === 'true') return;
        if (!item.id) return;

        dotNetRef.invokeMethodAsync(methodName, item.id).catch(() => {
            // circuit 断了，或组件随之而去，没有可汇报的对象。
        });
    };

    const onLeave = () => { lastItem = null; };

    container.addEventListener('mousemove', onMove, { passive: true });
    container.addEventListener('mouseover', onOver, { passive: true });
    container.addEventListener('mouseleave', onLeave, { passive: true });

    hoverObservers.set(id, () => {
        container.removeEventListener('mousemove', onMove);
        container.removeEventListener('mouseover', onOver);
        container.removeEventListener('mouseleave', onLeave);
    });
    return id;
}

/**
 * 停止一个悬停观察器。
 * * @param {number} id - observeHover 返回的句柄。
 */
export function unobserveHover(id) {
    const detach = hoverObservers.get(id);
    if (detach) {
        hoverObservers.delete(id);
        detach();
    }
}

// ============================================================================
// 键盘到达追踪
//
// `:focus-visible` 回答不了「用户是不是 Tab 到这里的?」。实测（而非想当然）：
// 在真实鼠标点击之后对 div[tabindex="0"] 调用程序化 .focus()，Chrome 依然把
// :focus-visible 报为 true——于是依赖它门禁的 hover card 在焦点陷阱移动焦点时照样打开。
// 同样的局限记录在 Primitives/Tables/TableRow.cs（源仓库 .razor:58）。
//
// Tab 是键盘用户到达 trigger 的方式，而响应 Tab 时没有任何程序会程序化移动焦点
// 所以在 :focus-visible 无能为力时，「本次 focus 刚发生过一次 Tab keydown」就能区分这两种情况。
// ============================================================================

let lastTabKeyAt = 0;
let keyboardArrivalListenerInitialized = false;

function initKeyboardArrivalListener() {
    if (keyboardArrivalListenerInitialized) return;

    document.addEventListener('keydown', (e) => {
        if (e.key === 'Tab') {
            lastTabKeyAt = performance.now();
        }
    }, true);

    keyboardArrivalListenerInitialized = true;
}

initKeyboardArrivalListener();

/**
 * 判断元素是否由键盘主动聚焦到达——而不是被焦点陷阱或显式 .focus() 程序化移动过来的。
 * * 检查无法执行时返回 true，确保不受支持的浏览器绝不把 hover card 挡在键盘用户面前。
 * WCAG 1.4.13 才是这条 focus 路径存在的理由。
 * * @param {HTMLElement} element - 待检测的元素
 * @param {number} [withinMs=500] - Tab keydown 必须发生在多久之内
 * @returns {boolean} 焦点由键盘到达时为 true
 */
export function isKeyboardFocus(element, withinMs = 500) {
    if (!element) return false;

    // 文本输入框永远是 focus-visible 的，而往里打字是不含糊的意图表达。
    try {
        if (element.matches('input:not([type="button"]):not([type="submit"]), textarea, select')) {
            return true;
        }
    } catch {
        return true;
    }

    return (performance.now() - lastTabKeyAt) <= withinMs;
}
