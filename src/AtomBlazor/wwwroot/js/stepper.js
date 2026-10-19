const observers = new WeakMap();
export function initialize(root, dotNetRef) {
    dispose(root);
    let previous = '';
    const report = () => {
        const ids = [...root.querySelectorAll('[data-step-id]')]
            .filter(el => el.closest('[data-slot="stepper"]') === root).map(el => el.dataset.stepId);
        const key = JSON.stringify(ids);
        if (key === previous) return;
        previous = key;
        dotNetRef.invokeMethodAsync('SynchronizeOrder', ids).catch(() => {});
    };
    const observer = new MutationObserver(report);
    observer.observe(root, { childList: true, subtree: true });
    observers.set(root, observer);
    report();
}
export function dispose(root) { observers.get(root)?.disconnect(); observers.delete(root); }
