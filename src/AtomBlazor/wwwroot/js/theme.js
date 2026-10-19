/**
 * 主题 JavaScript 模块。
 * 符合 CSP 的 DOM 操作：暗色模式、基础色、主色与圆角。
 * 不使用 eval()——所有操作都走直接的 DOM API。
 */

const STORAGE_KEY = 'atom-theme';
const designOptions = {
  density: ['standard', 'compact', 'dense', 'spacious'],
  font: ['system', 'inter', 'geist', 'roboto', 'nunitosans', 'serif', 'mono'],
  surface: ['standard', 'flat', 'elevated', 'glass'],
  menuColor: ['default', 'muted', 'primary', 'inverse'],
  menuAccent: ['subtle', 'primary']
};
const designAttributes = {
  density: 'data-atom-density', font: 'data-atom-font', surface: 'data-atom-surface',
  menuColor: 'data-atom-menu-color', menuAccent: 'data-atom-menu-accent'
};
/** 把任意 design 输入规整为合法值；非法或缺失的取值回落到默认。 */
function validDesign(value, defaults = {}) {
  return Object.fromEntries(Object.entries(designOptions).map(([key, allowed]) => {
    const name = typeof value?.[key] === 'string' ? value[key].toLowerCase() : null;
    return [key, allowed.includes(name) ? name : (defaults[key] ?? allowed[0])];
  }));
}

/**
 * 文档本应持有的主题状态。
 *
 * 下面的一切都作用于 <html>，而服务器渲染时从不带这些属性——主题只有客户端知道
 * （localStorage）。Blazor 的增强页面刷新会把服务器新渲染的文档合并进实时 DOM 并
 * 同步 <html> 的属性，于是 class/data-base-color/--radius 会被整体剥掉。
 * `dotnet watch` 的每次热重载触发的正是这种合并，它曾把主题重置回亮色、
 * 令 C# ThemeService 的状态与 DOM 相左（#400）。
 *
 * 追踪"意图中的状态"，让外部清空时我们能够把主题放回去。
 * @type {{ isDark: boolean, baseColor: string|null, primaryColor: string|null, radius: number|null } | null}
 */
let desired = null;

/** @type {MutationObserver | null} */
let guard = null;

/** 是否正处于我们自己的写入过程中——守卫据此忽略自身引发的变更。 */
let writing = false;

/**
 * 把意图状态写入文档。
 */
function writeToDom() {
  if (!desired) {
    return;
  }

  writing = true;
  try {
    const root = document.documentElement;

    if (desired.isDark) {
      root.classList.add('dark');
    } else {
      root.classList.remove('dark');
    }

    if (desired.baseColor !== null) {
      root.setAttribute('data-base-color', desired.baseColor);
    }

    if (desired.primaryColor !== null) {
      if (desired.primaryColor === 'default') {
        root.removeAttribute('data-primary-color');
      } else {
        root.setAttribute('data-primary-color', desired.primaryColor);
      }
    }

    for (const [key, attribute] of Object.entries(designAttributes)) {
      if (desired.design) root.setAttribute(attribute, desired.design[key]);
    }

    if (desired.radius !== null) {
      root.style.setProperty('--radius', desired.radius + 'rem');
    }
  } finally {
    writing = false;
  }
}

/**
 * 文档是否已经偏离意图状态。
 * @returns {boolean}
 */
function hasDrifted() {
  if (!desired) {
    return false;
  }

  const root = document.documentElement;

  if (root.classList.contains('dark') !== desired.isDark) {
    return true;
  }

  if (desired.baseColor !== null && root.getAttribute('data-base-color') !== desired.baseColor) {
    return true;
  }

  if (desired.primaryColor !== null) {
    const current = root.getAttribute('data-primary-color');
    const expected = desired.primaryColor === 'default' ? null : desired.primaryColor;
    if (current !== expected) {
      return true;
    }
  }

  if (desired.radius !== null && root.style.getPropertyValue('--radius') !== desired.radius + 'rem') {
    return true;
  }

  if (desired.design && Object.entries(designAttributes).some(([key, attribute]) => root.getAttribute(attribute) !== desired.design[key])) return true;
  return false;
}

/**
 * 观察 <html>，一旦外部把主题重置就恢复之。幂等——每文档只建一个观察器。
 * 只在真正的漂移上重新应用，因此我们自己的写入不会引发自激反馈环。
 */
function startGuard() {
  if (guard || typeof MutationObserver === 'undefined') {
    return;
  }

  guard = new MutationObserver(() => {
    if (writing || !hasDrifted()) {
      return;
    }
    writeToDom();
  });

  guard.observe(document.documentElement, {
    attributes: true,
    attributeFilter: ['class', 'data-base-color', 'data-primary-color', 'style', ...Object.values(designAttributes)]
  });
}

/**
 * 把完整主题应用到文档。
 * @param {boolean} isDark
 * @param {string} baseColor
 * @param {string} primaryColor
 * @param {number} radius
 * @param {Object} design
 */
export function applyTheme(isDark, baseColor, primaryColor, radius, design) {
  desired = { isDark, baseColor, primaryColor, radius, design: validDesign(design) };
  writeToDom();
  startGuard();
}

/**
 * 应用或移除暗色模式。
 * @param {boolean} isDark
 */
export function applyDarkMode(isDark) {
  desired = desired ?? { isDark, baseColor: null, primaryColor: null, radius: null };
  desired.isDark = isDark;
  writeToDom();
  startGuard();
}

/**
 * 在文档元素上设置基础色的 data 属性。
 * @param {string} color - 小写的颜色名（如 "zinc"、"slate"）。
 */
export function applyBaseColor(color) {
  desired = desired ?? { isDark: document.documentElement.classList.contains('dark'), baseColor: color, primaryColor: null, radius: null };
  desired.baseColor = color;
  writeToDom();
  startGuard();
}

/**
 * 在文档元素上设置主色的 data 属性。
 * @param {string} color - 小写的主色名（如 "blue"、"default"）。
 */
export function applyPrimaryColor(color) {
  desired = desired ?? { isDark: document.documentElement.classList.contains('dark'), baseColor: null, primaryColor: color, radius: null };
  desired.primaryColor = color;
  writeToDom();
  startGuard();
}

/**
 * 在文档元素上设置 --radius CSS 自定义属性。
 * @param {number} radius - 圆角半径（rem）。
 */
export function applyRadius(radius) {
  desired = desired ?? { isDark: document.documentElement.classList.contains('dark'), baseColor: null, primaryColor: null, radius };
  desired.radius = radius;
  writeToDom();
  startGuard();
}

/**
 * 探测用户操作系统是否偏好暗色模式。
 * @returns {boolean}
 */
export function getPrefersDark() {
  return window.matchMedia('(prefers-color-scheme: dark)').matches;
}

/**
 * 从 localStorage 读取已保存的主题偏好。
 * @returns {{ isDarkMode: boolean, baseColor: string, primaryColor: string, radius: number } | null}
 */
export function loadTheme() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

/**
 * 一次调用完成启动主题的解析与应用。
 *
 * 这里曾是 C# 侧的两到四次独立调用——loadTheme、然后也许 clearTheme、也许 getPrefersDark、
 * 最后 applyTheme——而在 Blazor Server 上每一次都是一趟电路往返，页面加载时在任何交互
 * 发生之前就要付出。其间的决策没有一个需要服务器参与：不过是读 localStorage 与一条媒体查询。
 *
 * 颜色名在这里对照 C# 提供的清单校验，于是 localStorage 中损坏或过时的条目会像 C# 的
 * 枚举解析那样直接回落到配置默认值，而不是先应用、再晚一个往返被纠正。
 *
 * @param {Object} config
 * @param {boolean} config.persist - 是否应当采用已保存的偏好。
 * @param {boolean} config.detectSystemPreference - 是否回落到操作系统的暗色模式设置。
 * @param {Object} config.defaults - isDarkMode、baseColor、primaryColor、radius。
 * @param {string[]} config.validBaseColors - 接受的基础色名，小写。
 * @param {string[]} config.validPrimaryColors - 接受的主色名，小写。
 * @returns {{ isDarkMode: boolean, baseColor: string, primaryColor: string, radius: number }}
 *   实际被应用的状态。
 */
export function initialize(config) {
  const defaults = config.defaults;

  // 只有在启用持久化时才读取 localStorage。无论如何都读，意味着关闭持久化之后，
  // 早前运行保存的主题仍会持续覆盖配置的默认值，除非用户手动清除站点数据（#481）。
  // 清除过期条目把同样的坑堵在已有存档的人身上。
  const saved = config.persist ? loadTheme() : null;
  if (!config.persist) {
    clearTheme();
  }

  const pick = (value, allowed, fallback) => {
    const name = typeof value === 'string' ? value.toLowerCase() : null;
    return name && allowed.includes(name) ? name : fallback;
  };

  const state = saved
    ? {
        isDarkMode: !!saved.isDarkMode,
        baseColor: pick(saved.baseColor, config.validBaseColors, defaults.baseColor),
        primaryColor: pick(saved.primaryColor, config.validPrimaryColors, defaults.primaryColor),
        radius: Number.isFinite(saved.radius) && saved.radius >= 0 && saved.radius <= 4 ? saved.radius : defaults.radius,
      }
    : {
        isDarkMode: config.detectSystemPreference ? getPrefersDark() : defaults.isDarkMode,
        baseColor: defaults.baseColor,
        primaryColor: defaults.primaryColor,
        radius: defaults.radius,
      };

  state.design = validDesign(saved?.design, validDesign(defaults.design));
  applyTheme(state.isDarkMode, state.baseColor, state.primaryColor, state.radius, state.design);

  return state;
}

/**
 * 把主题偏好保存到 localStorage。
 * @param {boolean} isDarkMode
 * @param {string} baseColor
 * @param {string} primaryColor
 * @param {number} radius
 * @param {Object} design
 */
export function saveTheme(isDarkMode, baseColor, primaryColor, radius, design) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ isDarkMode, baseColor, primaryColor, radius, design: validDesign(design) }));
  } catch {
    // localStorage 不可用——静默忽略
  }
}

/**
 * 从 localStorage 移除所有已保存的主题偏好。
 * 用于持久化被关闭时：防止早前运行写入的过期条目继续覆盖配置的默认值。
 */
export function clearTheme() {
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    // localStorage 不可用——静默忽略
  }
}
