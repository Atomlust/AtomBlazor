const watchers = new Map();
function resolveTarget(selector) {
    if (!selector) return { element: document.scrollingElement || document.documentElement, listenOn: window };
    const element = document.querySelector(selector);
    return element ? { element, listenOn: element } : null;
}
export function observe(id, selector, threshold, dotNetRef) {
    dispose(id);
    let target = null;
    let visible = null;
    const report = () => {
        const next = !!target && target.element.scrollTop > threshold;
        if (next === visible) return;
        visible = next;
        dotNetRef.invokeMethodAsync('JsSetVisible', next).catch(() => {});
    };
    const sync = () => {
        const next = resolveTarget(selector);
        if (next?.element === target?.element) { report(); return; }
        target?.listenOn.removeEventListener('scroll', report);
        target = next;
        target?.listenOn.addEventListener('scroll', report, { passive: true });
        report();
    };
    sync();
    // 条件渲染的面板可能在初始化之后才出现、消失或被替换。
    const observer = new MutationObserver(sync);
    observer.observe(document.documentElement, { childList: true, subtree: true });
    watchers.set(id, () => { observer.disconnect(); target?.listenOn.removeEventListener('scroll', report); });
    return !!target;
}
export async function scrollToTop(selector, smooth) {
    const target = resolveTarget(selector);
    if (!target) return false;
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    // 先移动键盘焦点，免得阈值判定把正在激活的按钮移走。
    if (document.activeElement?.closest('[data-slot="scroll-to-top"]')) {
        const previous = target.element.getAttribute('tabindex');
        target.element.setAttribute('tabindex', '-1');
        target.element.focus({ preventScroll: true });
        if (previous === null) target.element.removeAttribute('tabindex');
        else target.element.setAttribute('tabindex', previous);
    }
    return new Promise(resolve => {
        let frame;
        let timer;
        let done = false;
        const finish = reached => {
            if (done) return;
            done = true;
            cancelAnimationFrame(frame);
            clearTimeout(timer);
            target.listenOn.removeEventListener('scrollend', end);
            resolve(reached);
        };
        // 早先一次滚动排队的 scrollend，可能在这次滚动开始之后才到达。
        const end = () => { if (target.element.scrollTop <= 0) finish(true); };
        const check = () => {
            if (target.element.scrollTop <= 0) finish(true);
            else if (!target.element.isConnected) finish(false);
            else frame = requestAnimationFrame(check);
        };
        target.listenOn.addEventListener('scrollend', end);
        timer = setTimeout(() => finish(false), 5000);
        target.element.scrollTo({ top: 0, behavior: smooth && !reduced ? 'smooth' : 'auto' });
        frame = requestAnimationFrame(check);
    });
}
export function dispose(id) { watchers.get(id)?.(); watchers.delete(id); }
