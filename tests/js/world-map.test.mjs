// -c：world-map.js 地图注册/着色/纵横比测试（同名件 1:1，仅模块路径改名；mapName 'atom-world' 与库侧实物同形）。
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { readFile } from 'node:fs/promises';

const moduleUrl = new URL('../../src/AtomBlazor.Components/wwwroot/js/world-map.js', import.meta.url);
const source = (await readFile(moduleUrl, 'utf8')).replaceAll('import.meta.url', JSON.stringify(moduleUrl.href));
const geoJson = JSON.parse(await readFile(new URL('../maps/world.json', moduleUrl), 'utf8'));
let moduleId = 0;

async function fixture(t) {
  const api = await import(`data:text/javascript;base64,${Buffer.from(source + `\n// ${moduleId++}`).toString('base64')}`);
  const original = globalThis.fetch;
  t.after(() => { globalThis.fetch = original; });
  let requests = 0;
  globalThis.fetch = async () => { requests++; return { ok: true, json: async () => geoJson }; };
  const maps = new Map();
  let registrations = 0;
  const echarts = {
    getMap: name => maps.get(name),
    registerMap: (name, data) => { registrations++; maps.set(name, data); }
  };
  return { ...api, echarts, requests: () => requests, registrations: () => registrations };
}

const option = data => ({ series: [{ type: 'map', map: 'atom-world', data, itemStyle: { areaColor: 'gray' } }] });

test('only maps load geometry, concurrent maps share one request and registration', async t => {
  const f = await fixture(t);
  await f.ensureWorldMap(f.echarts, { series: [{ type: 'bar' }] });
  assert.equal(f.requests(), 0);
  await Promise.all([f.ensureWorldMap(f.echarts, option([])), f.ensureWorldMap(f.echarts, option([]))]);
  assert.equal(f.requests(), 1);
  assert.equal(f.registrations(), 1);
});

test('country aliases, duplicates and invalid codes preserve source indices and scale only plotted data', async t => {
  const f = await fixture(t);
  const raw = option([
    { name: 'unknown', value: 1000000, atomSourceIndex: 0 },
    { name: ' us ', value: 12, atomSourceIndex: 1 },
    { name: 'USA', value: 99, atomSourceIndex: 2 },
    { name: 'Germany', value: 0, atomSourceIndex: 3 },
    { name: 'SGP', value: 6, atomSourceIndex: 4 },
    { name: 'FR', value: -5, atomSourceIndex: 5 },
    { name: 'NO', value: null, atomSourceIndex: 6 }
  ]);
  await f.ensureWorldMap(f.echarts, raw);
  const prepared = f.prepareWorldMap(raw);
  const data = prepared.series[0].data;
  assert.equal(data.length, 241);
  assert.equal(data.find(d => d.name === 'United States of America').atomSourceIndex, 1);
  assert.equal(data.find(d => d.name === 'Singapore').value, 6);
  assert.equal(data.find(d => d.name === 'Germany').value, 0);
  assert.equal(data.find(d => d.name === 'Norway').itemStyle.areaColor, 'gray');
  assert.equal(data.find(d => d.name === 'Canada').atomSourceIndex, -1);
  assert.equal(prepared.visualMap.min, -5);
  assert.equal(prepared.visualMap.max, 12);
  assert.equal(raw.series[0].data.length, 7);
  assert.equal(raw.visualMap, undefined);
  const negative = f.prepareWorldMap(option([{ name: 'US', value: -5 }]));
  assert.equal(negative.visualMap.min, -5);
  assert.equal(negative.visualMap.max, 0);
});

test('explicit visual maps survive, and empty or all-zero maps have a usable default range', async t => {
  const f = await fixture(t);
  await f.ensureWorldMap(f.echarts, option([]));
  for (const data of [[], [{ name: 'US', value: 0 }], [{ name: 'US', value: null }]]) {
    const prepared = f.prepareWorldMap(option(data));
    assert.equal(prepared.visualMap.min, 0);
    assert.equal(prepared.visualMap.max, 1);
  }
  const raw = option([{ name: 'US', value: 12 }]);
  raw.visualMap = { min: 0, max: 100, show: false, inRange: { color: ['white', 'blue'] } };
  const customized = f.prepareWorldMap(raw).visualMap;
  assert.equal(customized.max, 100);
  assert.equal(customized.show, false);
  assert.deepEqual(customized.inRange, raw.visualMap.inRange);
});

test('failed asset loads can be retried', async t => {
  const f = await fixture(t);
  const success = globalThis.fetch;
  globalThis.fetch = async () => ({ ok: false, status: 503 });
  await assert.rejects(f.ensureWorldMap(f.echarts, option([])), /503/);
  globalThis.fetch = success;
  await f.ensureWorldMap(f.echarts, option([]));
  assert.equal(f.registrations(), 1);
});

test('map layout fits both dimensions without stretching, and resizing preserves interaction state', async t => {
  const f = await fixture(t);
  const raw = option([{ name: 'US', value: 12 }]);
  await f.ensureWorldMap(f.echarts, raw);
  const desktop = f.prepareWorldMap(raw, { clientWidth: 1000, clientHeight: 420 }).series[0];
  const mobile = f.prepareWorldMap(raw, { clientWidth: 292, clientHeight: 420 }).series[0];
  assert.ok(desktop.layoutSize > 600 && desktop.layoutSize < 1000);
  assert.equal(mobile.layoutSize, 292);
  let update;
  f.resizeWorldMap({ setOption: value => { update = value; } }, raw, { clientWidth: 292, clientHeight: 420 });
  assert.deepEqual(Object.keys(update.series[0]).sort(), ['layoutCenter', 'layoutSize']);
  assert.equal(update.series[0].layoutSize, 292);
});

test('bundled regions have unambiguous aliases and closed polygon rings', () => {
  const aliases = new Map();
  for (const feature of geoJson.features) {
    const { name, aliases: names } = feature.properties;
    for (const alias of [name, ...names]) {
      const key = alias.toLowerCase();
      assert.ok(!aliases.has(key) || aliases.get(key) === name, `Ambiguous alias: ${alias}`);
      aliases.set(key, name);
    }
    const polygons = feature.geometry.type === 'Polygon' ? [feature.geometry.coordinates] : feature.geometry.coordinates;
    for (const polygon of polygons) for (const ring of polygon) {
      assert.ok(ring.length >= 4);
      assert.deepEqual(ring[0], ring.at(-1));
    }
  }
});
