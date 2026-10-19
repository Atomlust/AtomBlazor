/**
 * Numeric Input JavaScript interop 模块。
 * 在 JS 侧处理 input/blur/focus/keydown 事件，尽量减少 C# interop 调用。
 *
 * 四个 C# 回调：
 *   - JsOnInput(value)   —— 键入过程中调用（立即或防抖后）
 *   - JsOnBlur(value)    —— 失焦时调用（总是）
 *   - JsOnFocus()        —— 获得焦点时调用（总是）
 *   - JsOnKeyDown(key)   —— 步进按键时调用（ArrowUp/Down、PageUp/Down、Home/End）
 *
 * 当 config.enableWheelStep 为 true 时，输入框持有焦点期间的滚轮位移会被累积，
 * 并映射为 ArrowUp/Down，走同一个按键回调。之所以设为选择性启用：增减必须对
 * wheel 事件调用 preventDefault，这会把滚动从页面手里抢走。
 *
 * 输入法组合期间会暂缓清洗与 interop，compositionend 时统一冲刷一次。缘由见
 * composition-guard.js。
 */

import { createCompositionGuard } from './composition-guard.js?assets=1';

const instances = new Map();

/**
 * 折算为一个步长所需的滚轮累计距离（像素）。Chrome、Edge、Safari 把一格鼠标滚轮
 * 报告为 deltaY ±100，所以有物理刻度的滚轮仍会即刻步进一次；触控板一次轻扫会发出
 * 几十个小增量，不再会这样连跳。
 */
const WheelStepThreshold = 100;

/**
 * 超过这个空闲间隔就重置累加器，让两次刻意轻扫不会被合并成一步。
 * 远大于惯性批次内约 16 毫秒的事件间隔。
 */
const WheelIdleResetMs = 200;

/**
 * 单个 wheel 事件可产生的步数上限。防止一个异常大的 delta（整页模式滚轮，
 * 或合成事件）触发无上限的 interop 调用风暴。
 */
const WheelMaxStepsPerEvent = 10;

/**
 * 把 wheel 事件的 delta 换算成像素，让上面的阈值在各处含义一致。
 * Firefox 报告行模式（每格 3 行），偶尔还有页模式。
 * @param {WheelEvent} e - wheel 事件。
 * @returns {number} 以像素计的 deltaY。
 */
const normalizeWheelDelta = (e) => {
  switch (e.deltaMode) {
    case 1: return e.deltaY * (WheelStepThreshold / 3);
    case 2: return e.deltaY * WheelStepThreshold;
    default: return e.deltaY;
  }
};

/**
 * 把全角字符折算为对应的 ASCII 形态，一字进一字出，光标的偏移量得以保留。
 * 日语输入法在全角模式下会对数字键输出 ０-９，不折算的话通不过下面的 ASCII
 * 区间判断，会被当作垃圾字符剥掉。
 * @param {string} ch - 单个字符。
 * @returns {string} 对应的 ASCII 字符，或原字符。
 */
const foldFullWidth = (ch) => {
  const code = ch.charCodeAt(0);
  if (code >= 0xff10 && code <= 0xff19) {
    return String.fromCharCode(code - 0xff10 + 0x30);
  }
  switch (code) {
    case 0xff0e: return '.';
    case 0xff0c: return ',';
    case 0xff0d: return '-';
    default: return ch;
  }
};

/**
 * 为某个数字输入元素初始化 JS 事件处理。
 * @param {HTMLElement} element - input 元素。
 * @param {DotNetObject} dotNetRef - Blazor 组件的引用。
 * @param {string} instanceId - 本实例的唯一 ID。
 * @param {object} config - 配置对象。
 * @param {boolean} config.disableDebounce - 为 true 时立即触发 JsOnInput。
 * @param {number} config.debounceMs - 防抖间隔（disableDebounce 为 false 时）。
 * @param {string[]} config.stepKeys - 需要拦截的按键名（如 ['ArrowUp', 'ArrowDown']）。
 * @param {boolean} config.allowDecimal - 是否允许小数点。
 * @param {boolean} config.allowNegative - 是否允许负号。
 * @param {string} config.decimalSeparator - 输入清洗使用的小数分隔符（如 '.'）。
 * @param {boolean} config.enableWheelStep - 焦点持有期间滚轮是否增减值。默认关闭。
 */
export function initialize(element, dotNetRef, instanceId, config) {
  if (!element || !dotNetRef) {
    return;
  }

  const state = {
    element,
    dotNetRef,
    config,
    debounceTimer: null,
    wheelAccumulator: 0,
    wheelLastEventAt: 0
  };

  const stepKeySet = new Set(config.stepKeys || []);

  /**
   * 剥掉输入值里的非数字字符，同时保留光标位置。
   * 允许数字、至多一个小数点（allowDecimal 开启时），以及一个前导负号（allowNegative 开启时）。
   */
  const sanitizeInput = () => {
    const cfg = state.config;
    const raw = element.value;
    const cursorPos = element.selectionStart ?? raw.length;

    let sanitized = '';
    let hasDecimal = false;
    let removed = 0;

    for (let i = 0; i < raw.length; i++) {
      const ch = foldFullWidth(raw[i]);
      const decSep = cfg.decimalSeparator || '.';
      if (ch >= '0' && ch <= '9') {
        sanitized += ch;
      } else if ((ch === '.' || ch === ',' || ch === decSep) && cfg.allowDecimal && !hasDecimal) {
        sanitized += decSep;
        hasDecimal = true;
      } else if (ch === '-' && cfg.allowNegative && sanitized.length === 0) {
        sanitized += ch;
      } else {
        if (i < cursorPos) {
          removed++;
        }
      }
    }

    if (sanitized !== raw) {
      element.value = sanitized;
      const newPos = Math.max(0, Math.min(cursorPos - removed, sanitized.length));
      try {
        element.setSelectionRange(newPos, newPos);
      } catch {
        // 该 input 类型不支持 setSelectionRange
      }
    }
  };

  const cancelPending = () => {
    if (state.debounceTimer) {
      clearTimeout(state.debounceTimer);
      state.debounceTimer = null;
    }
  };

  const handleInput = () => {
    // sanitizeInput 会写 element.value，那会重置组合缓冲；
    // guard.onFlush 会在输入法上屏后再执行一次本函数。
    if (guard.isComposing) {
      return;
    }

    sanitizeInput();
    const value = element.value;

    if (state.config.disableDebounce) {
      dotNetRef.invokeMethodAsync('JsOnInput', value).catch(() => {});
    } else {
      cancelPending();
      state.debounceTimer = setTimeout(() => {
        state.debounceTimer = null;
        dotNetRef.invokeMethodAsync('JsOnInput', value).catch(() => {});
      }, state.config.debounceMs);
    }
  };

  const handleBlur = () => {
    cancelPending();
    dotNetRef.invokeMethodAsync('JsOnBlur', element.value).catch(() => {});
  };

  const handleFocus = () => {
    dotNetRef.invokeMethodAsync('JsOnFocus').catch(() => {});
  };

  const handleKeyDown = (e) => {
    // 组合期间 Arrow/Home/End 属于输入法候选列表——在这里增减值会把它们抢走，
    // 还会 preventDefault 掉输入法自己的处理。
    if (guard.isComposing || e.isComposing === true || e.keyCode === 229) {
      return;
    }

    if (stepKeySet.has(e.key)) {
      e.preventDefault();
      dotNetRef.invokeMethodAsync('JsOnKeyDown', e.key).catch(() => {});
    }
  };

  /**
   * 滚轮累计距离越过一格刻度时增减值，复用键盘步进通路，
   * 让钳制、min/max 与步长规则在 C# 侧只存在一处。
   */
  const handleWheel = (e) => {
    if (document.activeElement !== element || e.deltaY === 0) {
      return;
    }

    // 手势归我们管——只要输入框还持有焦点，就让累加器而不是滚动去接它，
    // 一次轻扫不会滚到一半页面。
    e.preventDefault();

    const delta = normalizeWheelDelta(e);
    const now = Date.now();

    // 新手势从零开始：一段空闲间隔，或方向反转。
    if (now - state.wheelLastEventAt > WheelIdleResetMs ||
        (state.wheelAccumulator !== 0 && Math.sign(delta) !== Math.sign(state.wheelAccumulator))) {
      state.wheelAccumulator = 0;
    }

    state.wheelLastEventAt = now;
    state.wheelAccumulator += delta;

    let steps = Math.trunc(state.wheelAccumulator / WheelStepThreshold);
    if (steps === 0) {
      return;
    }

    // 余数结转，让一串低于阈值的增量随时间也能累加成步。
    state.wheelAccumulator -= steps * WheelStepThreshold;

    steps = Math.max(-WheelMaxStepsPerEvent, Math.min(WheelMaxStepsPerEvent, steps));

    const key = steps < 0 ? 'ArrowUp' : 'ArrowDown';
    for (let i = 0; i < Math.abs(steps); i++) {
      dotNetRef.invokeMethodAsync('JsOnKeyDown', key).catch(() => {});
    }
  };

  const guard = createCompositionGuard(element, { onFlush: handleInput });

  element.addEventListener('input', handleInput);
  element.addEventListener('blur', handleBlur);
  element.addEventListener('focus', handleFocus);
  element.addEventListener('keydown', handleKeyDown);

  const stored = {
    state,
    handleInput,
    handleBlur,
    handleFocus,
    handleKeyDown,
    handleWheel,
    wheelAttached: false,
    guard,
    element
  };

  instances.set(instanceId, stored);

  // 只按需启用：滚轮增减关闭时根本不存在监听器，没有任何东西会调用
  // preventDefault，指针经过输入框时页面滚动与原来完全一致。
  setWheelStepEnabled(instanceId, config.enableWheelStep === true);
}

/**
 * 在初始化之后开启或关闭滚轮增减：挂上或摘掉监听器，
 * 让关闭态零开销、绝不截获滚动。
 * @param {string} instanceId - 要更新的实例。
 * @param {boolean} enabled - 输入框持有焦点时滚轮是否增减值。
 */
export function setWheelStepEnabled(instanceId, enabled) {
  const stored = instances.get(instanceId);
  if (!stored) {
    return;
  }

  const shouldAttach = enabled === true;
  if (shouldAttach === stored.wheelAttached) {
    return;
  }

  if (shouldAttach) {
    stored.element.addEventListener('wheel', stored.handleWheel, { passive: false });
  } else {
    stored.element.removeEventListener('wheel', stored.handleWheel);
  }

  stored.wheelAttached = shouldAttach;
  stored.state.wheelAccumulator = 0;
  stored.state.wheelLastEventAt = 0;
}

/**
 * 更新既有实例的配置。
 * @param {string} instanceId - 要更新的实例。
 * @param {object} config - 新配置对象。
 */
export function updateConfig(instanceId, config) {
  const stored = instances.get(instanceId);
  if (stored) {
    stored.state.config = config;
  }
}

/**
 * 摘除事件处理器并清理状态。
 * @param {string} instanceId - 要释放的实例。
 */
export function dispose(instanceId) {
  const stored = instances.get(instanceId);
  if (!stored) {
    return;
  }

  stored.element.removeEventListener('input', stored.handleInput);
  stored.element.removeEventListener('blur', stored.handleBlur);
  stored.element.removeEventListener('focus', stored.handleFocus);
  stored.element.removeEventListener('keydown', stored.handleKeyDown);
  if (stored.wheelAttached) {
    stored.element.removeEventListener('wheel', stored.handleWheel);
  }
  stored.guard.dispose();

  if (stored.state.debounceTimer) {
    clearTimeout(stored.state.debounceTimer);
  }

  instances.delete(instanceId);
}
