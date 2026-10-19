/**
 * Sidebar JavaScript 模块
 * 负责移动端检测、键盘快捷键与状态持久化
 *
 * ES 模块是单例，页面里每个 SidebarProvider 都共享本模块。
 * 因此状态按实例保存而非置于模块级，keydown 监听器与
 * ResizeObserver 是共享的，并向各实例分发。
 */

const MOBILE_BREAKPOINT = 768;

let nextInstanceId = 1;
const instances = new Map();

let resizeObserver = null;
let keyboardHandler = null;

/**
 * 初始化侧边栏：移动端检测与键盘快捷键
 * @param {DotNetObject} componentRef - 指向 SidebarProvider 组件的引用
 * @param {boolean} enableToggleShortcut - Ctrl/Cmd + B 是否切换侧边栏
 * @returns {number} 实例 id，回传给 setToggleShortcutEnabled 与 cleanup
 */
export function initializeSidebar(componentRef, enableToggleShortcut) {
    const instanceId = nextInstanceId++;

    instances.set(instanceId, {
        dotNetRef: componentRef,
        shortcutEnabled: enableToggleShortcut !== false
    });

    attachSharedListeners();

    // 向新实例推送当前移动端状态
    notifyMobile(componentRef);

    return instanceId;
}

/**
 * 一次调用同时读取已存状态并登记实例。
 *
 * 此前从 C# 侧是两次分别 await 的调用，在 Blazor Server 上每一笔都是一次电路
 * 往返——每次页面加载都要付出，之后侧边栏才能以正确状态渲染。读取
 * cookie 并不需要服务器提供任何服务。
 *
 * @param {DotNetObject} componentRef - 指向 SidebarProvider 组件的引用。
 * @param {boolean} enableToggleShortcut - Ctrl/Cmd + B 是否切换侧边栏。
 * @param {string|null} cookieKey - 要从中恢复的 cookie 键，持久化关闭时为 null。
 * @returns {{ instanceId: number, savedOpen: boolean|null }} 实例 id 与已存状态，
 *   无可恢复内容时为 null。
 */
export function initialize(componentRef, enableToggleShortcut, cookieKey) {
    const savedOpen = cookieKey ? getSidebarState(cookieKey) : null;
    const instanceId = initializeSidebar(componentRef, enableToggleShortcut);

    return { instanceId, savedOpen };
}

/**
 * 初始化之后开启或关闭切换快捷键
 * @param {number} instanceId - initializeSidebar 返回的实例 id
 * @param {boolean} enabled - Ctrl/Cmd + B 是否切换侧边栏
 */
export function setToggleShortcutEnabled(instanceId, enabled) {
    const instance = instances.get(instanceId);
    if (instance) {
        instance.shortcutEnabled = enabled !== false;
    }
}

/**
 * 从 cookie 读取侧边栏状态
 * @param {string} key - Cookie 键
 * @returns {boolean|null} 已存状态，未找到时为 null
 */
export function getSidebarState(key) {
    const value = getCookie(key);
    if (value === 'true') return true;
    if (value === 'false') return false;
    return null;
}

/**
 * 把侧边栏状态保存到 cookie
 * @param {string} key - Cookie 键
 * @param {boolean} value - 要保存的状态
 */
export function saveSidebarState(key, value) {
    setCookie(key, value.toString(), 7); // 7 天过期
}

/**
 * 首次使用时挂上共享的 keydown 监听器与 ResizeObserver
 */
function attachSharedListeners() {
    if (!keyboardHandler) {
        keyboardHandler = (e) => {
            // 检查 Ctrl+B 或 Cmd+B
            if (!((e.ctrlKey || e.metaKey) && e.key === 'b')) {
                return;
            }

            let handled = false;

            for (const instance of instances.values()) {
                if (!instance.shortcutEnabled) {
                    continue;
                }

                handled = true;
                invoke(instance.dotNetRef, 'OnToggleShortcut');
            }

            // 只有确有实例响应时才吞掉按键——
            // 让关闭了快捷键的页面仍能用到原生 Ctrl/Cmd + B
            if (handled) {
                e.preventDefault();
            }
        };

        document.addEventListener('keydown', keyboardHandler);
    }

    if (!resizeObserver) {
        resizeObserver = new ResizeObserver(() => {
            for (const instance of instances.values()) {
                notifyMobile(instance.dotNetRef);
            }
        });

        resizeObserver.observe(document.body);
    }
}

/**
 * 向单个实例发送当前移动端状态
 * @param {DotNetObject} dotNetRef - 指向 SidebarProvider 组件的引用
 */
function notifyMobile(dotNetRef) {
    invoke(dotNetRef, 'OnMobileChange', window.innerWidth < MOBILE_BREAKPOINT);
}

/**
 * 调用 .NET 方法，忽略来自已释放组件或断线电路的失败。
 * 否则一个已拆除的实例就会打断共享的分发循环。
 * @param {DotNetObject} dotNetRef - 指向 SidebarProvider 组件的引用
 * @param {string} method - JSInvokable 方法名
 * @param {...any} args - 要转发的参数
 */
function invoke(dotNetRef, method, ...args) {
    try {
        const result = dotNetRef.invokeMethodAsync(method, ...args);
        if (result && typeof result.catch === 'function') {
            result.catch(() => { });
        }
    } catch {
        // 实例已不在；cleanup 会把它移除
    }
}

/**
 * 读取 cookie 值
 * @param {string} name - Cookie 名
 * @returns {string|null} Cookie 值或 null
 */
function getCookie(name) {
    const nameEQ = name + "=";
    const ca = document.cookie.split(';');
    for (let i = 0; i < ca.length; i++) {
        let c = ca[i];
        while (c.charAt(0) === ' ') c = c.substring(1, c.length);
        if (c.indexOf(nameEQ) === 0) return c.substring(nameEQ.length, c.length);
    }
    return null;
}

/**
 * 写入 cookie 值
 * @param {string} name - Cookie 名
 * @param {string} value - Cookie 值
 * @param {number} days - 过期天数
 */
function setCookie(name, value, days) {
    let expires = "";
    if (days) {
        const date = new Date();
        date.setTime(date.getTime() + (days * 24 * 60 * 60 * 1000));
        expires = "; expires=" + date.toUTCString();
    }
    document.cookie = name + "=" + (value || "") + expires + "; path=/; SameSite=Lax";
}

/**
 * 移除实例；最后一个实例离开时拆掉共享监听器
 * @param {number} instanceId - initializeSidebar 返回的实例 id
 */
export function cleanup(instanceId) {
    instances.delete(instanceId);

    if (instances.size > 0) {
        return;
    }

    if (resizeObserver) {
        resizeObserver.disconnect();
        resizeObserver = null;
    }

    if (keyboardHandler) {
        document.removeEventListener('keydown', keyboardHandler);
        keyboardHandler = null;
    }
}
