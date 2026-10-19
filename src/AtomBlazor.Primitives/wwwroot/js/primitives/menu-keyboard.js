/**
 * 菜单键盘导航模块。
 * 完全在 JavaScript 侧处理菜单容器（DropdownMenu、ContextMenu、Menubar）的所有键盘事件。
 * 只在 Escape（关闭）与水平菜单切换时调用 C#。
 *
 * 模式：
 *   "vertical" — DropdownMenu、ContextMenu（ArrowDown/ArrowUp/Home/End/Enter/Space/Escape）
 *   "menubar"  — Menubar 内容（与 vertical 相同，另加 ArrowLeft/ArrowRight 切换菜单）
 */

const instances = new Map();

const menuItemSelector = '[role="menuitem"], [role="menuitemcheckbox"], [role="menuitemradio"]';

/** type-ahead 检索的停顿毫秒数：超过此时长没有输入就清空检索缓冲。 */
const TYPE_AHEAD_DELAY = 350;

/**
 * 按 DOM 顺序取回容器内所有可用（未禁用）的菜单项。
 * 以 aria-disabled !== 'true' 过滤。
 */
function getEnabledMenuItems(container) {
  if (!container) return [];
  return Array.from(container.querySelectorAll(menuItemSelector))
    .filter(item => item.getAttribute('aria-disabled') !== 'true' && item.closest('[role="menu"]') === container);
}

/**
 * 可靠地聚焦一个菜单项。使用 preventScroll；若首次 focus() 失败，
 * 临时设置 tabindex="0" 重试（某些浏览器/框架环境下的锚元素会这样）。
 */
function focusMenuItem(item) {
  if (!item) return;
  item.focus({ preventScroll: true });
  if (document.activeElement !== item) {
    const prev = item.getAttribute('tabindex');
    item.setAttribute('tabindex', '0');
    item.focus({ preventScroll: true });
    item.setAttribute('tabindex', prev ?? '-1');
  }
}

/**
 * 导航到下一个可用菜单项。
 */
function navigateNext(container, loop) {
  const items = getEnabledMenuItems(container);
  if (items.length === 0) return;

  const currentIndex = items.findIndex(item => item === document.activeElement);
  let nextIndex;

  if (currentIndex === -1) {
    nextIndex = 0;
  } else if (currentIndex === items.length - 1) {
    nextIndex = loop ? 0 : currentIndex;
  } else {
    nextIndex = currentIndex + 1;
  }

  focusMenuItem(items[nextIndex]);
}

/**
 * 导航到上一个可用菜单项。
 */
function navigatePrevious(container, loop) {
  const items = getEnabledMenuItems(container);
  if (items.length === 0) return;

  const currentIndex = items.findIndex(item => item === document.activeElement);
  let prevIndex;

  if (currentIndex === -1) {
    prevIndex = items.length - 1;
  } else if (currentIndex === 0) {
    prevIndex = loop ? items.length - 1 : 0;
  } else {
    prevIndex = currentIndex - 1;
  }

  focusMenuItem(items[prevIndex]);
}

/**
 * 导航到第一个可用菜单项。
 */
function navigateFirst(container) {
  const items = getEnabledMenuItems(container);
  if (items.length > 0) {
    focusMenuItem(items[0]);
  }
}

/**
 * 导航到最后一个可用菜单项。
 */
function navigateLast(container) {
  const items = getEnabledMenuItems(container);
  if (items.length > 0) {
    focusMenuItem(items[items.length - 1]);
  }
}

/**
 * 用双重 requestAnimationFrame 聚焦元素，在 Blazor 渲染周期与门户挂载之后
 * 取得可靠的时机。
 */
function focusWithDoubleRaf(element) {
  requestAnimationFrame(() => {
    requestAnimationFrame(() => {
      if (element) {
        element.focus({ preventScroll: true });
      }
    });
  });
}

/**
 * 处理 type-ahead 检索：累积键入的字符，聚焦下一个匹配的菜单项。
 * 静默超过 TYPE_AHEAD_DELAY 毫秒后清空缓冲重新开始。
 */
function handleTypeAhead(instance, container, key) {
  clearTimeout(instance.typeAheadTimer);
  instance.typeAheadBuffer = (instance.typeAheadBuffer || '') + key.toLowerCase();

  instance.typeAheadTimer = setTimeout(() => {
    instance.typeAheadBuffer = '';
  }, TYPE_AHEAD_DELAY);

  const items = getEnabledMenuItems(container);
  if (items.length === 0) return;

  const search = instance.typeAheadBuffer;
  const currentIndex = items.findIndex(item => item === document.activeElement);
  const startIndex = currentIndex >= 0 ? currentIndex + 1 : 0;

  // 从当前位置向前查找，越过末尾后回绕
  for (let i = 0; i < items.length; i++) {
    const index = (startIndex + i) % items.length;
    const text = items[index].textContent?.trim().toLowerCase() || '';
    if (text.startsWith(search)) {
      focusMenuItem(items[index]);
      return;
    }
  }
}

/**
 * 为菜单容器建立键盘处理。
 * @param {HTMLElement} container - [role="menu"] 元素
 * @param {DotNetObjectReference} dotNetRef - 用于 Escape/菜单切换回调
 * @param {string} instanceId - 本实例的唯一 ID
 * @param {object} config
 * @param {string} config.mode - "vertical"（默认）或 "menubar"
 * @param {boolean} config.loop - 导航是否回绕（默认 true）
 * @param {string|null} config.initialFocus - "first"、"last" 或 null
 */
export function initialize(container, dotNetRef, instanceId, config) {
  if (!container || !dotNetRef) return;

  // 重新初始化前先释放旧实例
  dispose(instanceId);

  const mode = config?.mode || 'vertical';
  const loop = config?.loop !== false;
  const initialFocus = config?.initialFocus || null;
  const instance = { container, handler: null, typeAheadBuffer: '', typeAheadTimer: null };

  const handleKeyDown = (e) => {
    if (e.defaultPrevented || e.isComposing || e.ctrlKey || e.metaKey || e.altKey) return;
    if (e.target?.closest('[role="menu"]') !== container) return;
    const active = document.activeElement;
    const rtl = getComputedStyle(container).direction === 'rtl';
    const forward = rtl ? 'ArrowLeft' : 'ArrowRight';
    const back = rtl ? 'ArrowRight' : 'ArrowLeft';
    const submenuTrigger = active?.hasAttribute('data-atom-submenu-trigger') && active.getAttribute('aria-disabled') !== 'true';
    if (submenuTrigger && (e.key === forward || e.key === 'Enter' || e.key === ' ')) {
      e.preventDefault();
      e.stopPropagation();
      const submenu = document.getElementById(active.getAttribute('aria-controls'));
      if (active.getAttribute('aria-expanded') === 'true' && submenu) {
        navigateFirst(submenu);
      } else {
        active.click();
      }
      return;
    }
    if (mode === 'submenu' && e.key === back) {
      e.preventDefault();
      e.stopPropagation();
      dotNetRef.invokeMethodAsync('JsOnEscapeKey').catch(() => {});
      return;
    }
    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        navigateNext(container, loop);
        break;

      case 'ArrowUp':
        e.preventDefault();
        navigatePrevious(container, loop);
        break;

      case 'Home':
        e.preventDefault();
        navigateFirst(container);
        break;

      case 'End':
        e.preventDefault();
        navigateLast(container);
        break;

      case 'Enter':
      case ' ':
        e.preventDefault();
        if (document.activeElement && container.contains(document.activeElement)) {
          document.activeElement.click();
        }
        break;

      case 'Escape':
        e.preventDefault();
        // 归我们处理，就绝不能让它再到达 document 层的 Escape 栈——
        // 否则 Dialog 里的菜单一按 Escape 会把两层同时关掉。
        e.stopPropagation();
        dotNetRef.invokeMethodAsync('JsOnEscapeKey').catch(() => {});
        break;

      case 'ArrowRight':
        if (mode === 'menubar') {
          e.preventDefault();
          dotNetRef.invokeMethodAsync(rtl ? 'JsOnPreviousMenu' : 'JsOnNextMenu').catch(() => {});
        }
        break;

      case 'ArrowLeft':
        if (mode === 'menubar') {
          e.preventDefault();
          dotNetRef.invokeMethodAsync(rtl ? 'JsOnNextMenu' : 'JsOnPreviousMenu').catch(() => {});
        }
        break;

      default:
        // type-ahead：单个可打印字符触发检索
        if (e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey) {
          e.preventDefault();
          handleTypeAhead(instance, container, e.key);
        }
        break;
    }
  };

  // 悬停打开的子菜单在触发元素被点击时已经存在。直接移动焦点，
  // 不等另一次渲染，也不重新打开浮动层。
  const handleClick = e => {
    const trigger = e.target?.closest('[data-atom-submenu-trigger]');
    if (trigger?.closest('[role="menu"]') !== container || trigger.getAttribute('aria-disabled') === 'true') return;
    if (trigger.getAttribute('aria-expanded') === 'true') {
      navigateFirst(document.getElementById(trigger.getAttribute('aria-controls')));
    }
  };
  instance.clickHandler = handleClick;
  container.addEventListener('click', handleClick);
  instance.handler = handleKeyDown;
  container.addEventListener('keydown', handleKeyDown);
  instances.set(instanceId, instance);

  // 用双重 rAF 施加初始焦点，保证时机可靠
  if (initialFocus === 'first') {
    focusWithDoubleRaf(container);
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        navigateFirst(container);
      });
    });
  } else if (initialFocus === 'last') {
    focusWithDoubleRaf(container);
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        navigateLast(container);
      });
    });
  } else if (initialFocus === 'container') {
    // 聚焦容器本身（让键盘事件可达），不导航到任何条目。
    // 按 ArrowDown 会移动到第一个条目。
    focusWithDoubleRaf(container);
  }
}

/**
 * 释放指定实例，移除 keydown 监听器。
 * @param {string} instanceId
 */
export function dispose(instanceId) {
  const stored = instances.get(instanceId);
  if (!stored) return;

  stored.container.removeEventListener('keydown', stored.handler);
  stored.container.removeEventListener('click', stored.clickHandler);
  clearTimeout(stored.typeAheadTimer);
  instances.delete(instanceId);
}

/**
 * 用双重 rAF 聚焦第一个可用菜单项。
 * 供外部调用方使用（例如触发元素按 ArrowDown 打开菜单）。
 * @param {HTMLElement} container
 */
export function focusFirstItem(container) {
  requestAnimationFrame(() => {
    requestAnimationFrame(() => {
      navigateFirst(container);
    });
  });
}

/**
 * 用双重 rAF 聚焦最后一个可用菜单项。
 * 供外部调用方使用（例如触发元素按 ArrowUp 打开菜单）。
 * @param {HTMLElement} container
 */
export function focusLastItem(container) {
  requestAnimationFrame(() => {
    requestAnimationFrame(() => {
      navigateLast(container);
    });
  });
}

/** 在浮动层变得可见之后施加菜单焦点。 */
export function focusInitial(container, initialFocus) {
  if (!container) return;
  if (initialFocus === 'first') navigateFirst(container);
  else if (initialFocus === 'last') navigateLast(container);
  else if (initialFocus === 'container') container.focus({ preventScroll: true });
}

/**
 * 把按指针位置摆放的菜单保持在视口内。
 *
 * ContextMenu 直接把内容摆在原始点击坐标上，所以在靠近底部或右缘处右键，
 * 菜单会有部分渲染到屏幕外、条目无法触及。放不下时把菜单翻回指针另一侧——
 * 原生右键菜单就是这么做的——两侧都放不下时才退回夹取。
 *
 * @param {HTMLElement} container 已摆放在 (x, y) 的菜单面板。
 * @param {number} x 指针的 clientX。
 * @param {number} y 指针的 clientY。
 * @param {number} margin 菜单与视口边缘之间保留的像素数。
 */
export function clampToViewport(container, x, y, margin = 8) {
  if (!container) return;
  const { offsetWidth: width, offsetHeight: height } = container;
  if (!width || !height) return;

  const limitX = window.innerWidth - margin;
  const limitY = window.innerHeight - margin;

  // 先翻回指针另一侧；只有两侧都放不下才夹取。
  let left = x + width > limitX ? x - width : x;
  let top = y + height > limitY ? y - height : y;

  left = Math.max(margin, Math.min(left, limitX - width));
  top = Math.max(margin, Math.min(top, limitY - height));

  container.style.left = `${left}px`;
  container.style.top = `${top}px`;
}
