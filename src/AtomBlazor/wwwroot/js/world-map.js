// 国家（country）边界只在用到地图系列时抓取，每页一次。
const mapName = 'atom-world';
let loadPromise;
let countries;
let aspectRatio;

export async function ensureWorldMap(echarts, option) {
  if (!option.series?.some(series => series.type === 'map' && series.map === mapName)) return;

  if (!loadPromise) {
    loadPromise = (async () => {
      const response = await fetch(new URL('../maps/world.json', import.meta.url));
      if (!response.ok) throw new Error(`Unable to load world map: ${response.status}`);
      const geoJson = await response.json();
      countries = new Map();
      const bounds = [Infinity, Infinity, -Infinity, -Infinity];
      const extendBounds = coordinates => {
        if (typeof coordinates[0] === 'number') {
          bounds[0] = Math.min(bounds[0], coordinates[0]);
          bounds[1] = Math.min(bounds[1], coordinates[1]);
          bounds[2] = Math.max(bounds[2], coordinates[0]);
          bounds[3] = Math.max(bounds[3], coordinates[1]);
        } else {
          coordinates.forEach(extendBounds);
        }
      };
      for (const feature of geoJson.features) {
        extendBounds(feature.geometry.coordinates);
        const { name, aliases } = feature.properties;
        for (const alias of [name, ...aliases]) {
          countries.set(alias.trim().toLowerCase(), name);
        }
      }
      // ECharts 默认的地理纵横比缩放为 0.75。
      aspectRatio = (bounds[2] - bounds[0]) / (bounds[3] - bounds[1]) * 0.75;
      return geoJson;
    })().catch(error => {
      loadPromise = null; // 失败的请求不得毒化后续的地图实例。
      throw error;
    });
  }

  const geoJson = await loadPromise;
  if (!echarts.getMap(mapName)) echarts.registerMap(mapName, geoJson);
}

// 在主题解析之前运行，把原始 option 原样留给后续更新。
export function prepareWorldMap(option, element) {
  const mapSeries = option.series?.filter(series => series.type === 'map' && series.map === mapName);
  if (!mapSeries?.length || !countries) return option;

  const prepared = structuredClone(option);
  const values = [];
  const seriesIndices = [];
  prepared.series.forEach((series, index) => {
    if (series.type !== 'map' || series.map !== mapName) return;
    seriesIndices.push(index);
    const seen = new Set();
    series.data = (series.data ?? []).flatMap(item => {
      const name = countries.get(item.name?.trim().toLowerCase());
      if (!name || seen.has(name)) return [];
      seen.add(name);
      if (Number.isFinite(item.value)) values.push(item.value);
      return [{ ...item, name }];
    });
    // 显式置入缺失的区域，免得 visualMap 把它们错当成零。
    const present = new Set(series.data.filter(item => Number.isFinite(item.value)).map(item => item.name));
    for (const name of new Set(countries.values())) {
      if (present.has(name)) continue;
      const existing = series.data.find(item => item.name === name);
      const item = existing ?? { name, value: null, atomSourceIndex: -1 };
      item.itemStyle = { areaColor: series.itemStyle?.areaColor };
      if (!existing) series.data.push(item);
    }
    series.scaleLimit = { min: 1, max: 20 };
    if (element) Object.assign(series, mapLayout(element));
  });

  if (!prepared.visualMap) {
    const min = values.length ? Math.min(0, ...values) : 0;
    const max = values.length ? Math.max(0, ...values) : 1;
    prepared.visualMap = {
      type: 'continuous', min, max: max > min ? max : min + 1,
      seriesIndex: seriesIndices, orient: 'horizontal', left: 'center', bottom: 0,
      inRange: { color: ['var(--background)', 'var(--chart-1)'] },
      textStyle: { color: 'var(--muted-foreground)' }
    };
  }
  prepared.visualMap.text ??= [String(prepared.visualMap.max), String(prepared.visualMap.min)];
  prepared.visualMap.textStyle ??= { color: 'var(--muted-foreground)' };
  return prepared;
}

function mapLayout(element) {
  const top = 30;
  const bottom = 55;
  const height = Math.max(1, element.clientHeight - top - bottom);
  return {
    layoutCenter: ['50%', top + height / 2],
    layoutSize: Math.max(1, Math.min(element.clientWidth, height * aspectRatio))
  };
}

// resize 时只更新布局，保留当前的平移/缩放与颜色范围选择。
export function resizeWorldMap(chart, option, element) {
  if (!aspectRatio || !option.series?.some(series => series.map === mapName)) return;
  chart.setOption({
    series: option.series.map(series => series.map === mapName ? mapLayout(element) : {})
  });
}
