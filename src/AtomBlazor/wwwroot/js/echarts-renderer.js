// echarts-renderer.js
// 管理 ECharts 实例生命周期：创建、更新、调整尺寸、释放

import { resolveThemeColors, watchThemeChanges } from './chart-theme.js';
import { ensureWorldMap, prepareWorldMap, resizeWorldMap } from './world-map.js';

/** @type {Map<string, ChartState>} */
const instances = new Map();
const pendingInitializations = new Map();

/** @type {Promise|null} */
let echartsLoadPromise = null;

/** @type {any} */
let echartsLib = null;

/**
 * 惰性加载 ECharts 的 ESM 库。
 * 采用单飞（single-flight）模式防止重复加载。
 * @returns {Promise<any>}
 */
async function loadECharts() {
  if (echartsLib) return echartsLib;

  // 先检查是否已全局加载 ECharts（例如经由 <script> 标签）
  if (window.echarts) {
    echartsLib = window.echarts;
    return echartsLib;
  }

  if (!echartsLoadPromise) {
    echartsLoadPromise = (async () => {
      // 相对本模块自身的 URL 解析
      const libPath = new URL('../lib/echarts/echarts.min.js', import.meta.url).href;
      const mod = await import(libPath);
      return mod;
    })();
  }

  echartsLib = await echartsLoadPromise;
  return echartsLib;
}

/**
 * 把 ECharts 的点击参数摊平成 ChartClickEventArgs 期望的形状。
 * * `value` 是该系列所放之物：柱或线为数字、散点或 K 线为数组、
 * 偶尔为对象。把它拆成一个标量与一个数组，两种常见情形便
 * 在 C# 侧以强类型抵达，而非一团要使用者自行拆解的 JsonElement，
 * 其余情形则以双 null 抵达，name 与 dataIndex 仍可定位该点。
 * * @param {object} params - ECharts 事件参数
 * @returns {object} 与 ChartClickEventArgs 匹配的载荷
 */
function toClickArgs(params) {
  const raw = params.value;
  let value = null;
  let values = null;

  if (typeof raw === 'number' && Number.isFinite(raw)) {
    value = raw;
  } else if (Array.isArray(raw)) {
    values = raw.map((v) => (typeof v === 'number' ? v : Number(v))).map((v) => (Number.isFinite(v) ? v : 0));
  }

  return {
    seriesName: params.seriesName ?? null,
    seriesIndex: typeof params.seriesIndex === 'number' ? params.seriesIndex : -1,
    dataIndex: params.seriesType === 'map'
      ? (params.data?.atomSourceIndex ?? -1)
      : (typeof params.dataIndex === 'number' ? params.dataIndex : -1),
    name: params.name ?? null,
    componentType: params.componentType ?? null,
    value,
    values
  };
}

/**
 * 在 DOM 元素上初始化一个 ECharts 实例。
 * @param {string} chartId - 图表唯一标识（与元素 id 一致）
 * @param {object} option - ECharts option 对象（来自 C# 的 JSON）
 * @param {object} [dotNetRef] - 可选的 .NET 引用，供点击回调使用（#480）。仅当组件
 * 确实挂有处理器时才传入，没有处理器的图表完全不做 interop。
 */
export async function initialize(chartId, option, dotNetRef) {
  const element = document.getElementById(chartId);
  if (!element) return;

  // 若是重新初始化，先释放既有实例
  if (instances.has(chartId)) {
    dispose(chartId);
  }

  const initialization = {};
  pendingInitializations.set(chartId, initialization);
  const echarts = await loadECharts();
  await ensureWorldMap(echarts, option);
  if (pendingInitializations.get(chartId) !== initialization || !element.isConnected) return;
  pendingInitializations.delete(chartId);

  const chart = echarts.init(element, null, { renderer: 'svg' });

  // 解析 CSS 变量并设置选项
  const resolvedOption = resolveThemeColors(prepareWorldMap(option, element), element);
  applyRadarTooltipFormatter(resolvedOption);
  chart.setOption(resolvedOption);

  // 用 ResizeObserver 支撑响应式图表
  const resizeObserver = new ResizeObserver(() => {
    if (!chart.isDisposed()) {
      chart.resize();
      const state = instances.get(chartId);
      if (state) resizeWorldMap(chart, state.lastOption, element);
    }
  });
  resizeObserver.observe(element);

  // 主题变更监视器——主题变化时重新解析 CSS 变量
  const themeUnwatch = watchThemeChanges(() => {
    const state = instances.get(chartId);
    if (state && state.lastOption && !state.chart.isDisposed()) {
      const reresolved = resolveThemeColors(prepareWorldMap(state.lastOption, state.element), state.element);
      applyRadarTooltipFormatter(reresolved);
      state.chart.setOption(reresolved, { notMerge: true });
    }
  });

  instances.set(chartId, {
    chart,
    element,
    resizeObserver,
    themeUnwatch,
    dotNetRef: null,
    lastOption: option
  });
  setClickHandler(chartId, dotNetRef);
}

// 订阅关系与图表 option 各不相干：回调可以在数据未变时更换。
export function setClickHandler(chartId, dotNetRef) {
  const state = instances.get(chartId);
  if (!state || state.chart.isDisposed()) return;
  if (state.dataClick) state.chart.off('click', state.dataClick);
  if (state.blankClick) state.chart.getZr().off('click', state.blankClick);
  state.dataClick = state.blankClick = null;
  state.dotNetRef = dotNetRef;
  if (!dotNetRef) return;
  // 数据点由 ECharts 在此上报；空白画布的点击则要 zrender 也看得见。
  state.dataClick = params => dotNetRef.invokeMethodAsync('HandleDataPointClick', toClickArgs(params));
  state.blankClick = event => {
    // target 即被绘制的形状。containPixel('grid') 会把柱间的缝隙也一并排除。
    if (!event.target) dotNetRef.invokeMethodAsync('HandleChartClick');
  };
  state.chart.on('click', state.dataClick);
  state.chart.getZr().on('click', state.blankClick);
}

/**
 * 更新图表 option。
 * @param {string} chartId - 图表标识
 * @param {object} option - 新的 ECharts option
 * @param {boolean} notMerge - 为 true 时整体替换（默认：true）
 */
export async function update(chartId, option, notMerge) {
  const state = instances.get(chartId);
  if (!state || state.chart.isDisposed()) return;

  state.lastOption = option;
  await ensureWorldMap(echartsLib, option);
  if (state.chart.isDisposed() || state.lastOption !== option) return;
  const resolved = resolveThemeColors(prepareWorldMap(option, state.element), state.element);
  applyRadarTooltipFormatter(resolved);
  state.chart.setOption(resolved, { notMerge: notMerge !== false });
}

function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>"']/g, char => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
  })[char]);
}

/**
 * 若图表是带富文本指标名的雷达图（例如 "{a|186/80}\n{b|January}"），
 * 就装上一个 tooltip formatter，剥除富文本记号并转义数据的文本。
 * @param {object} option - 已解析的 ECharts option
 */
function applyRadarTooltipFormatter(option) {
  if (!option.radar?.indicator || !option.tooltip) return;

  const hasRichText = option.radar.indicator.some(
    i => i.name && /\{[a-z]\|/.test(i.name)
  );
  if (!hasRichText) return;

  // 取最后一个富文本记号作为干净的显示名（例如 "{b|January}" → "January"）。
  // 无匹配时退回剥除全部记号。
  const cleanNames = option.radar.indicator.map(i => {
    if (!i.name) return '';
    const matches = [...i.name.matchAll(/\{[a-z]\|([^}]*)\}/g)];
    return matches.length > 0 ? matches[matches.length - 1][1] : i.name;
  });

  option.tooltip.formatter = (params) => {
    let html = `${params.marker} <strong>${escapeHtml(params.seriesName)}</strong>`;
    if (Array.isArray(params.value)) {
      params.value.forEach((val, idx) => {
        if (idx < cleanNames.length) {
          html += `<br/>${escapeHtml(cleanNames[idx])}: ${escapeHtml(val)}`;
        }
      });
    }
    return html;
  };
}

/**
 * 释放 ECharts 实例并清理全部资源。
 * @param {string} chartId - 图表标识
 */
export function dispose(chartId) {
  pendingInitializations.delete(chartId);
  const state = instances.get(chartId);
  if (!state) return;

  state.resizeObserver.disconnect();
  if (state.themeUnwatch) {
    state.themeUnwatch();
  }
  if (!state.chart.isDisposed()) {
    state.chart.dispose();
  }
  instances.delete(chartId);
}
