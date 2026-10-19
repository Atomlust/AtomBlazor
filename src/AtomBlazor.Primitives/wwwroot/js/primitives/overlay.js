/**
 * 浮层的打开与关闭：各自都是单次 interop 调用，且不被任何地方 await。
 *
 * 本模块为何存在
 * ----------------
 * 在 Blazor Server 上，每个来自 C# 的 `InvokeAsync` 都是服务器发往浏览器、随后由服务器等待的
 * 消息，因此要付一次网络往返。过去打开一个浮层要背靠背花掉三次——计算位置、应用位置并揭示
 * 元素、再启动 auto-update——中间还夹着一次为解析后方位而做的 Blazor 重渲染，全在元素可见之前。
 * 这些步骤没有一步需要服务器的意见：它们全是浏览器自己就能做的决定。
 *
 * 把它们折叠成一次调用消掉了三次中的两次；剩下那次仍要付往返，因为 C# 在等它的结果。
 * 现在不等了。`open()` 用 `InvokeVoidAsync` 从渲染浮层内容的同一个同步过程派发，
 * 于是 `BeginInvokeJS` 消息与渲染批次同批送达，服务器从不等待回应。
 *
 * 这么做的代价，以及如何补偿
 * -----------------------------------
 * 发后即忘意味着消息会*先于*浏览器应用承载内容元素的渲染批次到达——因为服务器先发了它。
 * C# 因此不能把一个还不存在的内容元素的 `ElementReference` 交出来——参数反序列化会在本模块
 * 代码运行之前就于客户端抛错。于是改按名字指认内容元素：门户永远渲染的 `data-atom-portal`
 * 属性，再由这里带等待地解析。这个等待发生在客户端，分文不花；一帧之内就会结束，
 * 因为渲染批次正是总线上紧随其后的那条消息。
 *
 * 没有地方 await 结果，也就没有地方汇报失败。这里的每一条路径都会记日志。
 */

import * as clickOutside from './click-outside.js';
import * as themeScope from '../theme-scope.js';
import * as escapeKeydown from './escape-keydown.js';
import * as positioning from './positioning.js';
import * as menuKeyboard from './menu-keyboard.js';
import * as select from './select.js';

/** portalId -> 浮层关闭时要执行的清理函数列表。 */
const openOverlays = new Map();

/**
 * portalId -> 最近一次打开/关闭的单调递增令牌。
 *
 * `open()` 内部是异步的——先等元素，再测量——而 C# 侧早已继续往下走。过去在这个窗口里到达的
 * 关闭会先隐藏元素、随后 `open()` 又把它揭示出来，留下一个用户已经关掉的浮层挂在屏幕上。
 * 现在每次调用在入口处领一个令牌，一旦发现此后有更新的调用领过令牌，就自行放弃。
 */
const sequence = new Map();

/** 等待承载门户内容的渲染批次被应用的最长时间。 */
const CONTENT_WAIT_MS = 2000;

function nextToken(portalId) {
    const token = (sequence.get(portalId) ?? 0) + 1;
    sequence.set(portalId, token);
    return token;
}

function isCurrent(portalId, token) {
    return sequence.get(portalId) === token;
}

/**
 * 查找门户的内容元素，等待承载它的渲染批次。
 *
 * @param {string} portalId - 要查找的门户。
 * @param {number} timeoutMs - 放弃之前的等待时长。
 * @returns {Promise<HTMLElement|null>} 内容元素；始终未出现则为 null。
 */
function waitForContent(portalId, timeoutMs = CONTENT_WAIT_MS) {
    const selector = `[data-atom-portal="${CSS.escape(portalId)}"]`;

    const found = document.querySelector(selector);
    if (found) {
        return Promise.resolve(found);
    }

    return new Promise(resolve => {
        let settled = false;

        const finish = element => {
            if (settled) return;
            settled = true;
            observer.disconnect();
            clearTimeout(timer);
            resolve(element);
        };

        // 用 MutationObserver 而不是轮询：Blazor 在同一个同步过程里应用整个渲染批次，
        // 元素在一次变更中出现，这里也就在同一个任务里 resolve。10ms 的轮询会给每次打开
        // 平白加上最多 10ms 的空等。
        const observer = new MutationObserver(() => {
            const element = document.querySelector(selector);
            if (element) {
                finish(element);
            }
        });

        observer.observe(document.body, { childList: true, subtree: true });

        const timer = setTimeout(() => {
            console.warn(`overlay: portal content '${portalId}' did not render within ${timeoutMs}ms`);
            finish(null);
        }, timeoutMs);

        // 元素也可能恰好落在上面的 querySelector 与观察者启动之间。
        const late = document.querySelector(selector);
        if (late) {
            finish(late);
        }
    });
}

/**
 * 把浮动元素相对锚元素定位、揭示出来，并让它一直保持就位。
 *
 * 调用之后就可以走人——C# 不 await 本函数，也不许 await。
 *
 * @param {string} portalId - 标识浮层，close() 凭它找到清理函数。
 * @param {HTMLElement} reference - 要相对定位的锚元素。浮层打开时它已在屏幕上，
 *   所以这个参数仍然可以用 ElementReference 传递。
 * @param {Object} options - 定位选项：placement、offset、flip、shift、padding、
 *   strategy、matchReferenceWidth。置 `autoUpdate: false` 可跳过滚动/缩放监听。
 *   `dismiss` 可选地要求接上关闭监听：{ contentId, triggerId,
 *   onOutsideInteraction, onEscapeKey }。`sideElementId` 指认携带 `data-side` 的元素。
 *   `reportPlacement` 要求回调 `JsOnPlacementChanged`——这是一次往返，只在 C# 确实要读
 *   方位时才设置。`listbox` 为 select 内容做准备：{ kind, contentId, callbackRef,
 *   selectedValue, loop, mode }。`scrollToCurrent` 在揭示之前把标记元素滚动进视野：
 *   { containerId, selector }。`autoFocusId` 指认揭示后一帧之内要聚焦的元素。
 * @param {Object} dotNetRef - 监听器与回调要调用回去的 .NET 引用。
 * @returns {Promise<Object|null>} 解析后的位置，供极少数确实要 await 的调用方使用。
 */
export async function open(portalId, reference, options = {}, dotNetRef = null) {
    const token = nextToken(portalId);

    try {
        // 上一次 open 的接线还没拆就重新打开，会泄漏它的 auto-update 监听器。
        runCleanups(portalId);

        const floating = await waitForContent(portalId);
        if (!floating || !isCurrent(portalId, token)) {
            return null;
        }

        if (!positioning.isElementReady(reference)) {
            console.warn(`overlay: anchor for '${portalId}' is not in the document`);
            return null;
        }

        const cleanups = [themeScope.inheritTheme(reference, floating)];
        openOverlays.set(portalId, cleanups);

        // 在揭示之前，而不是之后。列表出现时必须已经滚动到选中项；
        // 上屏之后再滚动就是一次肉眼可见的跳变。
        if (options.keyboard) {
            prepareKeyboard(options.keyboard, dotNetRef);
        }

        // 同样的规则，照顾那些不是列表框、用自己的方式标记选中项的内容。
        if (options.scrollToCurrent) {
            try {
                select.scrollMarkedIntoView(
                    options.scrollToCurrent.containerId, options.scrollToCurrent.selector);
            } catch (error) {
                console.error('overlay: failed to scroll to the current item', error);
            }
        }

        const position = await positioning.computePosition(reference, floating, options);
        if (!isCurrent(portalId, token)) {
            return null;
        }

        applySide(options.sideElementId, position.placement);
        positioning.applyPosition(floating, position, true);

        // 严格在揭示之后。applyPosition 在一个 requestAnimationFrame 里把元素设为可见，
        // 而对 `visibility: hidden` 的元素调用 focus() 是什么都不做、也不抛任何错误的静默失败
        // ——再早一步聚焦，结果就是列表框没有焦点，每个方向键都作为触发元素的 keydown
        // 发往服务器，高亮永远不动。
        //
        // 只针对列表框。菜单在条目之间移动真实焦点，是否随打开拿焦点由它自己的
        // initialFocus 设置决定。
        //
        // autoFocusId 是同一思路对任何其余内容的版本——比如组合框的搜索框。
        // 它的主人过去在门户的 ready 回调里聚焦它：那在 Blazor Server 上是内容渲染之后
        // 的一个往返，然后又睡了 50ms、再为 FocusAsync 付一个往返。在这里，
        // 它只是揭示之后的一帧，免费。
        const focusTarget = options.keyboard && options.keyboard.kind === 'Listbox'
            ? () => select.focusListbox(options.keyboard.contentId)
            : options.keyboard?.kind === 'Menu' && options.keyboard.initialFocus
                ? () => menuKeyboard.focusInitial(document.getElementById(options.keyboard.contentId), options.keyboard.initialFocus)
                : options.autoFocusId
                ? () => document.getElementById(options.autoFocusId)?.focus({ preventScroll: true })
                : null;

        if (focusTarget) {
            requestAnimationFrame(() => requestAnimationFrame(() => {
                if (isCurrent(portalId, token)) {
                    focusTarget();
                }
            }));
        }

        if (options.autoUpdate !== false) {
            // autoUpdate 随滚动与缩放重新定位。它自己的首轮更新在这里运行，且无害——
            // 不带 makeVisible 的 applyPosition 只写坐标，从不写可见性。
            // 滚动让浮层翻转方位时，它也负责让 data-side 保持准确。
            const handle = await positioning.autoUpdate(reference, floating, {
                ...options,
                onPlacement: placement => applySide(options.sideElementId, placement)
            });

            if (!isCurrent(portalId, token)) {
                handle.apply();
                return null;
            }

            cleanups.push(() => handle.apply());
        }

        // 在这里接线，而不是让使用者事后补。使用者自己注册的每个监听器都曾多付一次往返，
        // 而在它们送达之前，浮层已可见却对窗外点击没有反应。
        const dismiss = options.dismiss;
        if (dismiss && dotNetRef) {
            if (dismiss.onOutsideInteraction) {
                const handle = clickOutside.onClickOutsideByIds(
                    dismiss.contentId, dotNetRef, 'JsOnDismissOutside', dismiss.triggerId);
                cleanups.push(() => handle.dispose());
            }

            if (dismiss.onEscapeKey) {
                // 用共享的栈，而不是自己的监听器：Escape 只应关掉最顶层的浮层。
                // 开在 Dialog 里的 Popover 在它上面，先行响应；下一次按键才轮到 Dialog。
                escapeKeydown.initialize(dotNetRef, portalId, 'JsOnDismissEscape');
                cleanups.push(() => escapeKeydown.dispose(portalId));
            }
        }

        openOverlays.set(portalId, cleanups);

        // 刻意放在最后，且需显式选择。这是一次回调用 .NET，在 Blazor Server 上就是
        // 本函数其余部分费心避开的那次往返。消息发出时浮层已上屏且可以操作，
        // 真需要在 C# 里读方位的使用者付费，但用户不为它等待。
        if (options.reportPlacement && dotNetRef) {
            dotNetRef.invokeMethodAsync('JsOnPlacementChanged', position.placement)
                .catch(error => console.error('overlay: placement callback failed', error));
        }

        return position;
    } catch (error) {
        // 没人 await 这个调用，所以从这里逃出去的错误是无声的——浮层就是不出现。
        // 记日志就是唯一的汇报。
        if (isCurrent(portalId, token)) runCleanups(portalId);
        console.error(`overlay: failed to open '${portalId}'`, error);
        return null;
    }
}

/**
 * 停止保持浮层定位，把它恢复到隐藏状态，其间等待任何退场动画。
 *
 * 与 open() 一样，C# 不 await 本函数。退场动画一结束浮层就视觉上消失了，
 * 而这是浏览器的开销，不是网络的开销。
 *
 * @param {string} portalId - 要关闭的浮层。
 * @param {Object} options - `notifyClosed` 要求元素隐藏后回调 `JsOnClosed`，
 *   供此后要卸载内容的使用者使用。`keyboard` 指认需要释放按键处理器的内容。
 *   `restoreFocusToId` 指认隐藏后要聚焦的元素；组合式触发元素改写该 ID 时，
 *   `restoreFocusToElement` 兜底提供锚元素。
 * @param {Object} dotNetRef - `notifyClosed` 回调要调用回去的 .NET 引用。
 */
export async function close(portalId, options = {}, dotNetRef = null) {
    const token = nextToken(portalId);

    try {
        runCleanups(portalId);

        // 折叠进来，而不是留给使用者。释放这些处理器曾是它自己在退场路上 await 的一次
        // interop——花一次往返，去移除一个用户早已关掉的浮层上的事件监听器。
        if (options.keyboard) {
            try {
                if (options.keyboard.kind === 'Menu') {
                    menuKeyboard.dispose(options.keyboard.contentId);
                } else {
                    select.cleanupKeyboardNavigation(options.keyboard.contentId);
                }
            } catch (error) {
                console.error('overlay: failed to release keyboard handlers', error);
            }
        }

        // 已经不在了，或从未打开过。仍值得告知使用者——它可能正等着这条来卸载自己的内容。
        const floating = document.querySelector(`[data-atom-portal="${CSS.escape(portalId)}"]`);

        if (floating) {
            await positioning.hidePosition(floating);
        }

        if (!isCurrent(portalId, token)) {
            // 退场动画期间被重新打开了。浮层又回到屏幕上，它的内容必须保持挂载。
            return;
        }

        // 焦点在这里归还给触发元素——只限用户用 Escape 或选中项主动要求的关闭。
        // 使用者过去在关闭渲染之后于 C# 里做这件事：一次 await 的 FocusAsync——
        // 花一次往返，把焦点送到浏览器本来就能送到的地方。点击外部的关闭不归还：
        // 焦点留在用户点击的位置。
        if (options.restoreFocusToId || options.restoreFocusToElement) {
            const target = (options.restoreFocusToId && document.getElementById(options.restoreFocusToId))
                || options.restoreFocusToElement;
            if (target) {
                try {
                    target.focus({ preventScroll: true });
                } catch (error) {
                    // 一个已经无法聚焦的触发元素，不值得再惊动控制台。
                }
            }
        }

        if (options.notifyClosed && dotNetRef) {
            // 刻意不在关键路径上：浮层已经不可见，这条只是释放它身后的 DOM。
            dotNetRef.invokeMethodAsync('JsOnClosed')
                .catch(error => console.error('overlay: close callback failed', error));
        }
    } catch (error) {
        console.error(`overlay: failed to close '${portalId}'`, error);
    }
}

/**
 * 本浮层当前是否已接线。让 C# 可以对从未打开的东西跳过关闭。
 *
 * @param {string} portalId - 要检查的浮层。
 * @returns {boolean} open() 运行过且 close() 没运行过则为 true。
 */
export function isOpen(portalId) {
    return openOverlays.has(portalId);
}

/**
 * 把解析后的方位写到那个据它做样式渲染的元素上。
 *
 * 这个属性过去由 C# 持有，并为此付一次渲染——在 Server 上即一次往返——的代价，
 * 卡在计算位置与揭示元素之间。方位是浏览器的测量结果，所以由浏览器来写。
 *
 * @param {string|null} elementId - 携带 data-side 的元素，若使用者指定了的话。
 * @param {string} placement - 解析后的方位，例如 "top-start"。
 */
function applySide(elementId, placement) {
    if (!elementId || !placement) {
        return;
    }

    const element = document.getElementById(elementId);
    if (element) {
        element.setAttribute('data-side', placement.split('-')[0]);
    }
}

/**
 * 在打开浮层的那一次调用里，接好内容的键盘行为。
 *
 * 它曾是使用者单独 await 的一次 interop，所以在 Blazor Server 上，浮层先出现，
 * 一个往返之后才变得可以用键盘操作。
 *
 * @param {Object} keyboard - { kind, contentId, callbackRef, selectedValue, loop, mode }。
 * @param {Object} dotNetRef - 门户的引用，仅在使用者没有指定自己的引用时兜底使用。
 */
function prepareKeyboard(keyboard, dotNetRef) {
    // 内容自己的引用，门户的引用兜底。列表框或菜单上的 Escape 调用的是使用者的方法，
    // 不是门户的。
    const ref = keyboard.callbackRef || dotNetRef;
    if (!ref) {
        return;
    }

    try {
        if (keyboard.kind === 'Menu') {
            const container = document.getElementById(keyboard.contentId);
            if (container) {
                menuKeyboard.initialize(container, ref, keyboard.contentId, {
                    mode: keyboard.mode || 'vertical',
                    loop: keyboard.loop !== false
                });
            }
            return;
        }

        // autoFocus 关闭：此刻列表框还停在暗处、不可见。open() 会在揭示它之后聚焦它。
        select.openListbox(keyboard.contentId, keyboard.selectedValue, true, ref);
    } catch (error) {
        console.error('overlay: failed to wire keyboard handling', error);
    }
}

function runCleanups(portalId) {
    const cleanups = openOverlays.get(portalId);
    if (!cleanups) {
        return;
    }

    openOverlays.delete(portalId);

    for (const cleanup of cleanups) {
        try {
            cleanup();
        } catch (error) {
            // 一个监听器失败不能连累其余的，否则浮层就把它们全泄漏了。
            console.error('overlay: cleanup failed', error);
        }
    }
}
