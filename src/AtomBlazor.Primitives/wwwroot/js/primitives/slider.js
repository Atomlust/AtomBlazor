import { createDragUpdates, releaseDragPreview, snapSliderValue } from '../drag-updates.js';

const sliderStates = new Map();

export function initialize(trackElement, dotNetRef, sliderId, options) {
  if (!trackElement || !dotNetRef) return;
  dispose(sliderId);

  let pointerId = null;
  let generation = 0;
  let disposed = false;
  const properties = ['--atom-slider-position'];
  const updates = createDragUpdates(percentage => dotNetRef.invokeMethodAsync('JsUpdateValueFromPercentage', percentage));
  const disabled = () => trackElement.getAttribute('data-disabled') === 'true';
  const update = e => {
    if (disabled()) return;
    const rect = trackElement.getBoundingClientRect();
    const vertical = (trackElement.getAttribute('data-orientation') ?? options?.orientation) === 'vertical';
    const size = vertical ? rect.height : rect.width;
    if (size <= 0) return;
    // 水平轨道从阅读边开始填充，所以在从右到左的滑块里距离要从右边量起：
    // 点击左端必须意味着最大值。
    const rtl = getComputedStyle(trackElement).direction === 'rtl';
    const horizontal = rtl ? (rect.right - e.clientX) / size : (e.clientX - rect.left) / size;
    const percentage = Math.max(0, Math.min(1, vertical
      ? 1 - (e.clientY - rect.top) / size : horizontal));
    const min = Number(trackElement.getAttribute('data-min') ?? 0);
    const max = Number(trackElement.getAttribute('data-max') ?? 100);
    const step = Number(trackElement.getAttribute('data-step') ?? 1);
    const value = snapSliderValue(percentage, min, max, step);
    const snapped = max > min ? (value - min) / (max - min) : 0;
    trackElement.style.setProperty('--atom-slider-position', `${snapped * 100}%`);
    // 发送取整后的比例，让相同的步进不会反复跨越电路。
    updates.push([snapped]);
  };
  const down = e => {
    if (pointerId != null || disabled() || e.button > 0) return;
    e.preventDefault();
    generation++;
    pointerId = e.pointerId;
    updates.begin();
    trackElement.setPointerCapture(pointerId);
    document.body.style.userSelect = 'none';
    document.body.style.cursor = 'grabbing';
    update(e);
  };
  const move = e => {
    if (e.pointerId !== pointerId) return;
    e.preventDefault();
    update(e);
  };
  const finish = e => {
    if (e.pointerId !== pointerId) return;
    if (e.type === 'pointerup') update(e);
    pointerId = null;
    if (trackElement.hasPointerCapture(e.pointerId)) trackElement.releasePointerCapture(e.pointerId);
    document.body.style.userSelect = '';
    document.body.style.cursor = '';
    const current = generation;
    releaseDragPreview(updates, trackElement, properties, () => !disposed && generation === current && pointerId == null);
  };
  const keydown = e => {
    if (!disabled() && ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End', 'PageUp', 'PageDown'].includes(e.key)) {
      e.preventDefault();
    }
  };
  const listeners = { keydown, pointerdown: down, pointermove: move, pointerup: finish, pointercancel: finish, lostpointercapture: finish };
  Object.entries(listeners).forEach(([event, handler]) => trackElement.addEventListener(event, handler));
  sliderStates.set(sliderId, () => {
    disposed = true;
    updates.dispose();
    Object.entries(listeners).forEach(([event, handler]) => trackElement.removeEventListener(event, handler));
    if (pointerId != null) {
      if (trackElement.hasPointerCapture(pointerId)) trackElement.releasePointerCapture(pointerId);
      document.body.style.userSelect = '';
      document.body.style.cursor = '';
    }
    properties.forEach(property => trackElement.style.removeProperty(property));
  });
}

export function dispose(sliderId) {
  sliderStates.get(sliderId)?.();
  sliderStates.delete(sliderId);
}
