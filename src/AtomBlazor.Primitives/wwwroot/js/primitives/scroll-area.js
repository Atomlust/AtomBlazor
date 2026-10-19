// ScrollArea primitive——负责滚动跟踪、滑块尺寸计算、拖拽行为
// 以及轨道点击跳转，用于实现自定义滚动条。

const instances = new Map();

/**
 * 按 ID 取得 viewport 元素（供 Blazor 互操作使用的辅助函数）。
 * @param {string} viewportId - viewport 元素的 ID。
 * @returns {HTMLElement|null}
 */
export function getViewportElement(viewportId) {
  return document.getElementById(viewportId);
}

/**
 * 为一个滚动条实例初始化滚动区域行为。
 * @param {string} instanceId - 唯一实例标识符。
 * @param {HTMLElement} viewportEl - 可滚动的 viewport 元素。
 * @param {HTMLElement} scrollbarEl - 滚动条轨道元素。
 * @param {object} dotNetRef - 对 Blazor 组件的引用。
 * @param {object} options - 配置选项。
 * @param {string} options.orientation - "vertical" 或 "horizontal"。
 */
export function initialize(instanceId, viewportEl, scrollbarEl, dotNetRef, options) {
  if (!viewportEl || !scrollbarEl || !dotNetRef) {
    return;
  }

  const isVertical = options?.orientation !== "horizontal";
  const thumbEl = scrollbarEl.querySelector("[data-scrollarea-thumb]");

  const state = {
    viewportEl,
    scrollbarEl,
    thumbEl,
    dotNetRef,
    isVertical,
    isDragging: false,
    dragStartPos: 0,
    dragStartScroll: 0,
    pointerId: null,
  };

  const update = () => {
    const contentSize = isVertical ? viewportEl.scrollHeight : viewportEl.scrollWidth;
    const viewportSize = isVertical ? viewportEl.clientHeight : viewportEl.clientWidth;
    const scrollPos = isVertical ? viewportEl.scrollTop : viewportEl.scrollLeft;
    const maxScroll = contentSize - viewportSize;

    const hasOverflow = contentSize > viewportSize;
    const dataState = hasOverflow ? "visible" : "hidden";

    if (thumbEl) {
      if (hasOverflow) {
        const trackSize = isVertical ? scrollbarEl.clientHeight : scrollbarEl.clientWidth;
        const thumbSize = Math.max(20, (viewportSize / contentSize) * trackSize);
        const thumbPos = maxScroll > 0
          ? ((scrollPos / maxScroll) * (trackSize - thumbSize))
          : 0;

        if (isVertical) {
          thumbEl.style.height = `${thumbSize}px`;
          thumbEl.style.transform = `translate3d(0, ${thumbPos}px, 0)`;
        } else {
          thumbEl.style.width = `${thumbSize}px`;
          thumbEl.style.transform = `translate3d(${thumbPos}px, 0, 0)`;
        }
      }

      thumbEl.setAttribute("data-state", dataState);
    }

    const scrollPercent = maxScroll > 0 ? Math.round((scrollPos / maxScroll) * 100) : 0;

    try {
      dotNetRef.invokeMethodAsync("JsUpdateScrollState", scrollPercent, dataState).catch(() => {});
    } catch {
      // 组件可能已释放
    }
  };

  // 滚动监听器
  const handleScroll = () => {
    requestAnimationFrame(update);
  };

  // 点击轨道跳转滚动位置
  const handleTrackPointerDown = (e) => {
    if (e.target === thumbEl || (thumbEl && thumbEl.contains(e.target))) {
      return;
    }

    e.preventDefault();
    const rect = scrollbarEl.getBoundingClientRect();
    const contentSize = isVertical ? viewportEl.scrollHeight : viewportEl.scrollWidth;
    const viewportSize = isVertical ? viewportEl.clientHeight : viewportEl.clientWidth;
    const maxScroll = contentSize - viewportSize;

    if (maxScroll <= 0) {
      return;
    }

    const trackSize = isVertical ? rect.height : rect.width;
    const clickPos = isVertical ? (e.clientY - rect.top) : (e.clientX - rect.left);
    const ratio = clickPos / trackSize;

    viewportEl.scrollTo({
      [isVertical ? "top" : "left"]: ratio * maxScroll,
      behavior: "smooth",
    });
  };

  // 滑块拖拽手柄
  const handleThumbPointerDown = (e) => {
    if (state.isDragging) {
      return;
    }

    e.preventDefault();
    e.stopPropagation();
    state.isDragging = true;
    state.pointerId = e.pointerId;
    state.dragStartPos = isVertical ? e.clientY : e.clientX;
    state.dragStartScroll = isVertical ? viewportEl.scrollTop : viewportEl.scrollLeft;

    if (thumbEl) {
      thumbEl.setPointerCapture(e.pointerId);
    }

    document.body.style.userSelect = "none";
    document.body.style.cursor = "grabbing";
  };

  const handleThumbPointerMove = (e) => {
    if (!state.isDragging || e.pointerId !== state.pointerId) {
      return;
    }

    e.preventDefault();
    const currentPos = isVertical ? e.clientY : e.clientX;
    const delta = currentPos - state.dragStartPos;

    const contentSize = isVertical ? viewportEl.scrollHeight : viewportEl.scrollWidth;
    const viewportSize = isVertical ? viewportEl.clientHeight : viewportEl.clientWidth;
    const trackSize = isVertical ? scrollbarEl.clientHeight : scrollbarEl.clientWidth;
    const thumbSize = Math.max(20, (viewportSize / contentSize) * trackSize);

    const scrollableTrack = trackSize - thumbSize;
    const maxScroll = contentSize - viewportSize;

    if (scrollableTrack > 0) {
      const scrollDelta = (delta / scrollableTrack) * maxScroll;
      viewportEl.scrollTo({
        [isVertical ? "top" : "left"]: state.dragStartScroll + scrollDelta,
      });
    }
  };

  const handleThumbPointerUp = (e) => {
    if (e.pointerId !== state.pointerId) {
      return;
    }

    state.isDragging = false;
    state.pointerId = null;

    if (thumbEl) {
      thumbEl.releasePointerCapture(e.pointerId);
    }

    document.body.style.userSelect = "";
    document.body.style.cursor = "";
  };

  const handleThumbPointerCancel = (e) => {
    if (e.pointerId !== state.pointerId) {
      return;
    }

    state.isDragging = false;
    state.pointerId = null;
    document.body.style.userSelect = "";
    document.body.style.cursor = "";
  };

  // ResizeObserver：尺寸变化时重新计算
  const resizeObserver = new ResizeObserver(() => {
    requestAnimationFrame(update);
  });
  resizeObserver.observe(viewportEl);
  if (viewportEl.firstElementChild) {
    resizeObserver.observe(viewportEl.firstElementChild);
  }

  // 挂载监听器
  viewportEl.addEventListener("scroll", handleScroll, { passive: true });
  scrollbarEl.addEventListener("pointerdown", handleTrackPointerDown);

  if (thumbEl) {
    thumbEl.addEventListener("pointerdown", handleThumbPointerDown);
    thumbEl.addEventListener("pointermove", handleThumbPointerMove);
    thumbEl.addEventListener("pointerup", handleThumbPointerUp);
    thumbEl.addEventListener("pointercancel", handleThumbPointerCancel);
  }

  instances.set(instanceId, {
    state,
    resizeObserver,
    handleScroll,
    handleTrackPointerDown,
    handleThumbPointerDown,
    handleThumbPointerMove,
    handleThumbPointerUp,
    handleThumbPointerCancel,
  });

  // 初始更新
  update();
}

/**
 * 释放一个滚动区域实例并清理全部监听器。
 * @param {string} instanceId - 要释放的实例。
 */
export function dispose(instanceId) {
  const stored = instances.get(instanceId);
  if (!stored) {
    return;
  }

  const { state, resizeObserver } = stored;

  state.viewportEl.removeEventListener("scroll", stored.handleScroll);
  state.scrollbarEl.removeEventListener("pointerdown", stored.handleTrackPointerDown);

  if (state.thumbEl) {
    state.thumbEl.removeEventListener("pointerdown", stored.handleThumbPointerDown);
    state.thumbEl.removeEventListener("pointermove", stored.handleThumbPointerMove);
    state.thumbEl.removeEventListener("pointerup", stored.handleThumbPointerUp);
    state.thumbEl.removeEventListener("pointercancel", stored.handleThumbPointerCancel);
  }

  resizeObserver.disconnect();
  instances.delete(instanceId);
}
