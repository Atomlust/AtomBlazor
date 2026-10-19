// chart-theme.js
// 面向 ECharts 的 CSS 变量解析与主题变更检测

/**
 * 递归遍历 option 对象，把其中含 CSS var() 引用的字符串值
 * 解析为计算后的颜色值。
 * @param {object} option - ECharts option 对象
 * @param {HTMLElement} contextElement - 供 getComputedStyle 使用的元素
 * @returns {object} - 颜色已解析的新 option 对象
 */
export function resolveThemeColors(option, contextElement) {
  if (!option || !contextElement) return option;
  return deepResolve(structuredClone(option), contextElement);
}

/**
 * @param {any} obj
 * @param {HTMLElement} el
 * @returns {any}
 */
function deepResolve(obj, el) {
  if (obj === null || obj === undefined) return obj;

  if (typeof obj === 'string') {
    return resolveValue(obj, el);
  }

  if (Array.isArray(obj)) {
    return obj.map(item => deepResolve(item, el));
  }

  if (typeof obj === 'object') {
    for (const key of Object.keys(obj)) {
      obj[key] = deepResolve(obj[key], el);
    }
  }

  return obj;
}

/**
 * 解析单个字符串值。含 var(--x) 时对其求值。
 * OKLCH 值通过换算为十六进制来处理。
 * @param {string} value
 * @param {HTMLElement} el
 * @returns {string}
 */
function resolveValue(value, el) {
  if (typeof value !== 'string') return value;

  const varRegex = /var\(\s*(--[a-zA-Z0-9-]+)\s*(?:,\s*([^)]+))?\s*\)/g;
  if (!varRegex.test(value)) return value;

  // test 之后重置正则的 lastIndex
  varRegex.lastIndex = 0;

  const resolved = value.replace(varRegex, (_match, varName, fallback) => {
    const computed = getComputedStyle(el).getPropertyValue(varName).trim();
    if (computed) {
      return convertToUsableColor(computed);
    }
    return fallback ? fallback.trim() : _match;
  });

  // 若结果仍是复杂的 CSS 表达式（例如 color-mix()），
  // 就经由 canvas 上下文将其解析为十六进制颜色
  if (resolved !== value && /color-mix\s*\(/i.test(resolved)) {
    return convertToUsableColor(resolved);
  }

  return resolved;
}

/**
 * 把任何 CSS 颜色表达式转换成 ECharts 能解析的 rgb()/rgba() 字符串。
 *
 * ECharts 内部的 zrender 颜色解析器认识 hex、rgb/rgba、hsl/hsla 与
 * 具名颜色，但不认识 oklch() 这类 CSS Level 4 格式。
 *
 * ## 为什么使用纯数学——以及为什么它在所有浏览器里都成立
 *
 * 在每个浏览器（Chrome、Firefox、Safari、Edge、Brave、Opera……）里，
 * `getComputedStyle(el).getPropertyValue('--name')` 都原样返回
 * CSS 自定义属性（例如 `--chart-1: oklch(81% .1 252)`）的取值。浏览器从不
 * 归一化或解析自定义属性的令牌值——它只是存取
 * 原始文本。因此无论浏览器为何，我们收到的字符串始终是
 * 最初的 oklch() 表达式，必须由我们自己换算。
 *
 * 下面的 `oklchToRgb` 函数是自足的 JavaScript 数学（Oklab
 * 参考矩阵）。它不借助任何浏览器 API，因而在 Chrome、Firefox、Safari
 * 与所有 Chromium 分支中产生完全相同的结果。
 *
 * 另注：当 oklch 作为*已解析*的非自定义值出现时（例如回读
 * `element.style.color`），Chrome 111+ 也已停止对其归一化。
 * Firefox 与 Safari 则仍会把这类值归一化为 rgb()。
 * 这两种行为都不影响我们，因为我们只从自定义属性读取。
 *
 * 不使用 oklch 的其他 CSS Level 4 表达式（例如 color-mix()）
 * 会先经 DOM 兜底尝试解析；若兜底结果仍是 oklch（Chrome 的行为），
 * 就把该结果交给数学换算器。
 *
 * @param {string} colorValue - 任意 CSS 颜色字符串
 * @returns {string} - 与 ECharts 兼容的颜色字符串
 */
function convertToUsableColor(colorValue) {
  // 快路径：已是 ECharts 能理解的格式
  if (/^#[0-9a-fA-F]{3,8}$/.test(colorValue)) return colorValue;
  if (/^rgba?\(/.test(colorValue)) return colorValue;
  if (/^hsla?\(/.test(colorValue)) return colorValue;

  // 处理无包裹函数的裸 OKLCH 令牌（例如 "0.81 0.10 252"）
  if (/^\d/.test(colorValue) && colorValue.includes(' ') && !colorValue.includes('(')) {
    colorValue = `oklch(${colorValue})`;
  }

  // 用自足的数学换算 oklch()——浏览器 API 在这里不可靠
  if (/^oklch\(/i.test(colorValue)) {
    const rgb = oklchToRgb(colorValue);
    if (rgb) return rgb;
  }

  // 兜底：对其余表达式（color-mix 等）走基于 DOM 的解析
  const tmp = document.createElement('div');
  tmp.style.color = colorValue;
  document.documentElement.appendChild(tmp);
  const resolved = getComputedStyle(tmp).color;
  document.documentElement.removeChild(tmp);

  // DOM 若返回了可解析的非 oklch 值，就直接采用
  if (resolved && resolved !== 'rgba(0, 0, 0, 0)' && !/^oklch\(/i.test(resolved)) {
    return resolved;
  }
  // DOM 若仍返回 oklch（例如以 oklch 为输入的 color-mix），就尝试换算它
  if (resolved && /^oklch\(/i.test(resolved)) {
    const rgb = oklchToRgb(resolved);
    if (rgb) return rgb;
  }

  return colorValue;
}

/**
 * 用自足的数学把 oklch() 颜色字符串换算为 rgb() 字符串。
 * 实现官方 Oklab/OKLCH → sRGB 换算矩阵。
 *
 * 本函数完全与浏览器无关——它只做纯算术、不调用
 * 任何浏览器 API，因而在 Chrome、Firefox、Safari、Edge、Brave
 * 及其他任何 JavaScript 环境中产生相同结果。
 *
 * 同时处理 "oklch(L C H)"（L 取 0–1）与 "oklch(L% C H)"（L 取 0–100%）。
 * 空格分隔与逗号分隔的分量语法皆可接受。
 * 结尾的 "/ alpha" 分量可选择性地忽略。
 *
 * @param {string} oklchStr - 例如 "oklch(0.42 0.18 266)" 或 "oklch(42% .18 266)"
 * @returns {string|null} - rgb() 字符串；输入无法解析时返回 null
 */
function oklchToRgb(oklchStr) {
  const m = oklchStr.match(
    /oklch\(\s*([\d.]+)(%?)\s*,?\s*([\d.]+)\s*,?\s*([\d.]+)\s*(?:\/\s*[\d.]+%?\s*)?\)/i
  );
  if (!m) return null;

  let L = parseFloat(m[1]);
  if (m[2] === '%') L /= 100;       // 百分比形式 → 0–1
  const C = parseFloat(m[3]);
  const H = parseFloat(m[4]);

  // 1. oklch → oklab（极坐标转直角坐标）
  const hRad = (H * Math.PI) / 180;
  const a = C * Math.cos(hRad);
  const b = C * Math.sin(hRad);

  // 2. oklab → 线性 sRGB（Oklab 参考矩阵）
  const l_ = L + 0.3963377774 * a + 0.2158037573 * b;
  const m_ = L - 0.1055613458 * a - 0.0638541728 * b;
  const s_ = L - 0.0894841775 * a - 1.2914855480 * b;

  const l3 = l_ * l_ * l_;
  const m3 = m_ * m_ * m_;
  const s3 = s_ * s_ * s_;

  const rLin = +4.0767416621 * l3 - 3.3077115913 * m3 + 0.2309699292 * s3;
  const gLin = -1.2684380046 * l3 + 2.6097574011 * m3 - 0.3413193965 * s3;
  const bLin = -0.0041960863 * l3 - 0.7034186147 * m3 + 1.7076147010 * s3;

  // 3. 线性 sRGB → sRGB gamma + 钳制到 [0, 255]
  const toSrgbByte = (linear) => {
    const v = Math.max(0, Math.min(1, linear));
    const gamma = v <= 0.0031308 ? 12.92 * v : 1.055 * Math.pow(v, 1 / 2.4) - 0.055;
    return Math.round(gamma * 255);
  };

  return `rgb(${toSrgbByte(rLin)}, ${toSrgbByte(gLin)}, ${toSrgbByte(bLin)})`;
}

/**
 * 经由 MutationObserver 与自定义 'atom-theme-changed' 事件
 * 监视主题变化（深色/浅色模式切换）。
 *
 * 观察 <html> 元素的 class 或 data-theme 属性变化。
 * 同时在 <html> 上监听 'atom-theme-changed' 自定义事件，让
 * 开发者在以编程方式改动 CSS 变量之后（例如动态换肤、主题构建器）
 * 也能触发图表颜色更新。
 *
 * 用法：
 *   document.documentElement.dispatchEvent(new CustomEvent('atom-theme-changed'));
 *
 * @param {Function} callback - 主题变化时被调用
 * @returns {Function} - 停止监视的清理函数
 */
export function watchThemeChanges(callback) {
  const target = document.documentElement;

  const observer = new MutationObserver((mutations) => {
    for (const mutation of mutations) {
      if (mutation.type === 'attributes' &&
        (mutation.attributeName === 'class' || mutation.attributeName === 'data-theme')) {
        // 延后一拍，等 CSS 变量完成更新
        requestAnimationFrame(() => callback());
        break;
      }
    }
  });

  observer.observe(target, {
    attributes: true,
    attributeFilter: ['class', 'data-theme']
  });

  // 监听自定义主题变更事件（例如来自动态主题编辑器）
  const onThemeChanged = () => requestAnimationFrame(() => callback());
  target.addEventListener('atom-theme-changed', onThemeChanged);

  return () => {
    observer.disconnect();
    target.removeEventListener('atom-theme-changed', onThemeChanged);
  };
}
