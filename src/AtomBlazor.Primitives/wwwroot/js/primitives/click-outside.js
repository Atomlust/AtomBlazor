// 下拉菜单、浮层与对话框的点击外部检测

// 清理函数存放在以唯一 ID 索引的注册表里，避免把函数本身穿过 JS interop
const cleanupRegistry = new Map();

/**
 * 按 ID 等待元素出现在 DOM 里。
 * @param {string} elementId - 要等待的元素 ID
 * @param {number} maxWaitMs - 最长等待毫秒数
 * @param {number} intervalMs - 轮询间隔毫秒数
 * @returns {Promise<HTMLElement|null>} 找到则返回元素，否则 null
 */
async function waitForElementById(elementId, maxWaitMs = 200, intervalMs = 10) {
    const startTime = Date.now();

    while (Date.now() - startTime < maxWaitMs) {
        const element = document.getElementById(elementId);
        if (element && document.body.contains(element)) {
            return element;
        }
        await new Promise(resolve => setTimeout(resolve, intervalMs));
    }

    return null;
}

/**
 * 建立焦点外部检测（焦点离开某个元素时）。
 * @param {HTMLElement} element - 要监视的元素
 * @param {Object} dotNetRef - 用于回调的 DotNet 对象引用
 * @param {string} methodName - 焦点离开时要调用的方法名
 * @returns {Object} 带 dispose() 方法的可释放对象，用于移除监听器
 */
export function onFocusOutside(element, dotNetRef, methodName = 'HandleFocusOutside') {
    if (!element || !dotNetRef) {
        console.warn('focus-outside: element or dotNetRef is null');
        return {
            _cleanupId: -1,
            dispose: function() {}
        };
    }

    const handleFocusIn = (e) => {
        if (!element.contains(e.target)) {
            try {
                if (dotNetRef && !dotNetRef._disposed) {
                    dotNetRef.invokeMethodAsync(methodName);
                }
            } catch (error) {
                console.error('focus-outside callback error:', error);
            }
        }
    };

    document.addEventListener('focusin', handleFocusIn, true);

    // 把清理函数存入注册表
    const cleanupFunc = () => {
        document.removeEventListener('focusin', handleFocusIn, true);
    };

    const id = crypto.randomUUID();
    cleanupRegistry.set(id, cleanupFunc);

    return {
        _cleanupId: id,
        dispose: function() {
            const cleanup = cleanupRegistry.get(this._cleanupId);
            if (cleanup) {
                cleanup();
                cleanupRegistry.delete(this._cleanupId);
            }
        }
    };
}

/**
 * 组合的外部交互检测器（点击 + 焦点）。
 * @param {HTMLElement} element - 要监视的元素
 * @param {Object} dotNetRef - 用于回调的 DotNet 对象引用
 * @param {string} methodName - 要调用的方法名
 * @returns {Object} 带 dispose() 方法的可释放对象，移除全部监听器
 */
export function onInteractOutside(element, dotNetRef, methodName = 'HandleInteractOutside') {
    const cleanupClick = onClickOutside(element, dotNetRef, methodName);
    const cleanupFocus = onFocusOutside(element, dotNetRef, methodName);

    // 把组合清理函数存入注册表
    const cleanupFunc = () => {
        cleanupClick.dispose();
        cleanupFocus.dispose();
    };

    const id = crypto.randomUUID();
    cleanupRegistry.set(id, cleanupFunc);

    return {
        _cleanupId: id,
        dispose: function() {
            const cleanup = cleanupRegistry.get(this._cleanupId);
            if (cleanup) {
                cleanup();
                cleanupRegistry.delete(this._cleanupId);
            }
        }
    };
}

/**
 * 用元素 ID 建立点击外部检测。等待元素出现在 DOM 里，
 * 把 C# 的时序与门户渲染解耦——所有等待都由 JS 承担。
 * @param {string} elementId - 要监视的元素 ID
 * @param {Object} dotNetRef - 用于回调的 DotNet 对象引用
 * @param {string} methodName - 点击外部时要调用的方法名
 * @param {string} excludeElementId - 可选：从"外部"判定中排除的元素 ID（例如触发按钮）
 * @param {number} maxWaitMs - 等待元素的最长毫秒数
 * @returns {Promise<Object>} 带 dispose() 的可释放对象；元素找不到时返回 no-op
 */
export async function onClickOutsideById(elementId, dotNetRef, methodName = 'JsOnClickOutside', excludeElementId = null, maxWaitMs = 200) {
    const element = await waitForElementById(elementId, maxWaitMs);
    if (!element) {
        console.warn(`onClickOutsideById: Element ${elementId} not found within ${maxWaitMs}ms`);
        return {
            _cleanupId: -1,
            dispose: function() {}
        };
    }

    // 给了 ID 就取排除元素（不等待——它是触发元素，本就该已存在）
    const excludeElement = excludeElementId ? document.getElementById(excludeElementId) : null;

    return onClickOutside(element, dotNetRef, methodName, excludeElement);
}

/**
 * 用元素 ID 建立点击外部检测，并以 pointerdown + pointerup 成对校验。
 * 关键：只有 pointerdown 和 pointerup 都发生在外部才触发关闭。
 * 这样可防止 Blazor Server 重渲染引发的多余第二次 pointerdown（没有对应的
 * pointerup）误关闭浮层。
 *
 * 处理嵌套门户场景（例如 DatePicker → Calendar → Select 下拉）：
 * - 用 data-portal-content 属性做通用门户检测
 * - 在 pointerdown 时记录门户 ID，以应对门户在 pointerup 触发前就被移除的情形
 *   （基于事件的状态追踪，而不是基于时间的宽限期）
 *
 * @param {string} elementId - 要监视的元素 ID
 * @param {Object} dotNetRef - 用于回调的 DotNet 对象引用
 * @param {string} methodName - 点击外部时要调用的方法名
 * @param {string} excludeElementId - 可选：从"外部"判定中排除的元素 ID（例如触发按钮）
 * @returns {Object} 带 dispose() 的可释放对象
 */
export function onClickOutsideByIds(elementId, dotNetRef, methodName = 'JsOnClickOutside', excludeElementId = null) {
    if (!elementId || !dotNetRef) {
        console.warn('click-outside: elementId or dotNetRef is null');
        return {
            _cleanupId: -1,
            dispose: function() {}
        };
    }

    let isEnabled = false;
    let isPointerDownInside = false;
    // 记录交互起始于哪个门户（按 ID），而不只是记一个布尔值，
    // 这样即便门户已从 DOM 移除，事后仍然可以核实
    let nestedPortalIdOnPointerDown = null;

    // 检查目标是否位于某个嵌套门户（Select、Combobox 等）之内。
    // 用 data-portal-content 属性做通用、面向未来的检测。
    // 在嵌套门户内时返回该门户 ID，否则返回 null。
    // 重要：必须区分父门户（包含我们触发元素的那个）与子门户（从我们组件内部打开的）。
    // 只有子门户才应当阻止点击外部触发关闭。
    const getNestedPortalId = (target) => {
        // 用 data-portal-content 属性检查点击是否落在任何一个门户内容里
        const portalContent = target.closest('[data-portal-content]');
        if (portalContent && portalContent.id !== elementId) {
            // 找到了一个不是我们元素的门户
            // 看看我们的触发元素是否在其中，以判断它是不是父门户
            const ourTrigger = excludeElementId ? document.getElementById(excludeElementId) : null;
            if (ourTrigger && portalContent.contains(ourTrigger)) {
                // 我们的触发元素在这个门户里——它是父门户，不是嵌套门户
                // 按普通点击处理（不视为在嵌套门户内）
                return null;
            }
            // 是兄弟或子门户——返回它的 ID
            return portalContent.id;
        }
        return null;
    };

    const handlePointerDown = (e) => {
        if (!isEnabled) return;

        let target = e.target;
        if (target && target.nodeType === Node.TEXT_NODE) {
            target = target.parentElement;
        }

        const element = document.getElementById(elementId);
        const excludeElement = excludeElementId ? document.getElementById(excludeElementId) : null;

        const isInsideContent = element && element.contains(target);
        const isInsideTrigger = excludeElement && excludeElement.contains(target);

        // 只有点击既不在我们的触发元素上、也不在我们的内容里时，才检查嵌套门户。
        // 点在触发元素/内容上时，任何父门户（比如装着 Select 的 Popover）都无关紧要。
        const nestedPortalId = (isInsideContent || isInsideTrigger) ? null : getNestedPortalId(target);

        // 记录 pointerdown 是否发生在内部
        isPointerDownInside = isInsideContent || isInsideTrigger || nestedPortalId !== null;

        // 存下门户 ID（或 null）——这样即便那个门户已从 DOM 移除，
        // 我们依然知道这次交互起始于某个嵌套门户
        nestedPortalIdOnPointerDown = nestedPortalId;
    };

    const handlePointerUp = (e) => {
        if (!isEnabled) return;

        // pointerdown 若在嵌套门户里，就不关闭——哪怕那个门户现在已经没了。
        // 我们按 ID 追踪交互，不靠 DOM 里的存在性。
        if (nestedPortalIdOnPointerDown !== null) {
            isPointerDownInside = false;
            nestedPortalIdOnPointerDown = null;
            return;
        }

        let target = e.target;
        if (target && target.nodeType === Node.TEXT_NODE) {
            target = target.parentElement;
        }

        const element = document.getElementById(elementId);
        const excludeElement = excludeElementId ? document.getElementById(excludeElementId) : null;

        const isInsideContent = element && element.contains(target);
        const isInsideTrigger = excludeElement && excludeElement.contains(target);

        // 只有点击既不在触发元素也不在内容里时，才检查嵌套门户
        const nestedPortalId = (isInsideContent || isInsideTrigger) ? null : getNestedPortalId(target);
        const isOutside = !isInsideContent && !isInsideTrigger && nestedPortalId === null;

        // 只有 pointerdown 与 pointerup 都在外部才关闭——
        // 这可防止没有配对 pointerup 的多余第二次 pointerdown 触发关闭
        if (!isPointerDownInside && isOutside) {
            try {
                dotNetRef.invokeMethodAsync(methodName);
            } catch (error) {
                console.error('click-outside callback error:', error);
            }
        }

        // 为下一次交互复位
        isPointerDownInside = false;
        nestedPortalIdOnPointerDown = null;
    };

    // 用捕获阶段抢先拿到事件，赶在它们被 stop 之前
    document.addEventListener('pointerdown', handlePointerDown, true);
    document.addEventListener('pointerup', handlePointerUp, true);

    // 当前事件循环走完后才启用，避免把打开浮层的那一次点击也算成外部点击
    let disposed = false;
    const enableTimeout = setTimeout(() => {
        if (!disposed) {
            isEnabled = true;
        }
    }, 0);

    const cleanupFunc = () => {
        disposed = true;
        isEnabled = false;
        clearTimeout(enableTimeout);
        document.removeEventListener('pointerdown', handlePointerDown, true);
        document.removeEventListener('pointerup', handlePointerUp, true);
    };

    const id = crypto.randomUUID();
    cleanupRegistry.set(id, cleanupFunc);

    return {
        _cleanupId: id,
        dispose: function() {
            const cleanup = cleanupRegistry.get(this._cleanupId);
            if (cleanup) {
                cleanup();
                cleanupRegistry.delete(this._cleanupId);
            }
        }
    };
}
