// 拖拽反馈留在浏览器里。只有最新的、与此前不同的值会跨越 interop，
// 每 50ms 至多一次，即便在慢速 Server 电路上也始终只有一个在途回调。
export function createDragUpdates(send, interval = 50) {
  let generation = 0;
  let pending = null;
  let lastSent = null;
  let lastSentAt = -Infinity;
  let inFlight = false;
  let timer = null;
  let disposed = false;
  let flushing = false;
  let waiters = [];

  const same = (a, b) => a && b && a.length === b.length && a.every((v, i) => Object.is(v, b[i]));
  const settle = () => {
    if (!inFlight && !pending || disposed) {
      flushing = false;
      const current = waiters;
      waiters = [];
      current.forEach(resolve => resolve());
    }
  };
  const drain = () => {
    if (disposed || inFlight) return;
    if (!pending) { settle(); return; }
    const delay = flushing ? 0 : Math.max(0, interval - (performance.now() - lastSentAt));
    if (delay > 0) {
      timer ??= setTimeout(() => { timer = null; drain(); }, delay);
      return;
    }
    const args = pending;
    pending = null;
    lastSent = args;
    lastSentAt = performance.now();
    inFlight = true;
    const current = generation;
    Promise.resolve().then(() => { if (!disposed && current === generation) return send(...args); })
      .catch(() => {}) // 已断开的电路不能把拖拽队列留在运行状态。
      .finally(() => { inFlight = false; drain(); settle(); });
  };
  return {
    begin() { lastSent = null; lastSentAt = -Infinity; },
    push(args) {
      if (disposed || same(args, pending ?? lastSent)) return;
      pending = same(args, lastSent) ? null : args;
      drain();
    },
    flush() {
      if (disposed) return Promise.resolve();
      if (timer != null) { clearTimeout(timer); timer = null; }
      flushing = true;
      const completion = new Promise(resolve => waiters.push(resolve));
      drain();
      settle();
      return completion;
    },
    cancel() {
      generation++;
      pending = null;
      lastSent = null;
      lastSentAt = -Infinity;
      if (timer != null) clearTimeout(timer);
      timer = null;
      settle();
    },
    dispose() {
      disposed = true;
      pending = null;
      if (timer != null) clearTimeout(timer);
      timer = null;
      settle();
    }
  };
}

// 让 Blazor 的最终渲染先到、再把视觉所有权交还。新手势或释放会使这次清理失效，
// 所以较早的回调不会擦掉较新的预览。
export function releaseDragPreview(updates, element, properties, isCurrent) {
  updates.flush().then(() => requestAnimationFrame(() => requestAnimationFrame(() => {
    if (isCurrent()) properties.forEach(property => element.style.removeProperty(property));
  })));
}

export function snapSliderValue(percentage, min, max, step) {
  let value = min + Math.max(0, Math.min(1, percentage)) * (max - min);
  if (step > 0 && Number.isFinite(step)) {
    const quotient = value / step;
    const floor = Math.floor(quotient);
    // 与 Math.Round 的中点取偶行为保持一致，负值区间也不例外。
    value = (quotient - floor === 0.5 ? floor + (Math.abs(floor % 2)) : Math.round(quotient)) * step;
  }
  return Math.max(min, Math.min(max, value));
}
