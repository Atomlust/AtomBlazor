/**
 * ResponsiveNav JavaScript 模块
 * 为响应式导航处理移动端检测
 */

const MOBILE_BREAKPOINT = 768;

let dotNetRef = null;
let resizeObserver = null;

/**
 * 初始化响应式导航并进行移动端检测
 * @param {DotNetObject} componentRef - 指向 ResponsiveNavProvider 组件的引用
 */
export function initialize(componentRef) {
    dotNetRef = componentRef;
    setupMobileDetection();
}

/**
 * 使用 ResizeObserver 建立移动端检测
 */
function setupMobileDetection() {
    if (!dotNetRef) return;

    const checkMobile = () => {
        const isMobile = window.innerWidth < MOBILE_BREAKPOINT;
        dotNetRef.invokeMethodAsync('OnMobileChange', isMobile);
    };

    // 初次检查
    checkMobile();

    // 监听尺寸变化事件
    resizeObserver = new ResizeObserver(() => {
        checkMobile();
    });

    resizeObserver.observe(document.body);
}

/**
 * 清理事件监听器与观察器
 */
export function cleanup() {
    if (resizeObserver) {
        resizeObserver.disconnect();
        resizeObserver = null;
    }

    dotNetRef = null;
}
