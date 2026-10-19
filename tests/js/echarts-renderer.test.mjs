// echarts-renderer.js 的生命周期与点击互通测试（Node 环境，无浏览器）。
//
// 本模块是 C# 侧 ChartBase 的对端：initialize/update/setClickHandler/dispose 四个入口
// 与 ChartClickEventArgs 的载荷形状都在此定稿，故值得在无宿主进程下钉住。
// 桩件策略照本仓既有先例（见 chart-theme.test.mjs、cascader-module.test.mjs）：
// loadECharts 先看 window.echarts 是否已由 <script> 备好，因此只要把假库挂在 window 上，
// 就完全不走 `import('../lib/echarts/echarts.min.js')`——真实的压缩包不必在 Node 里求值。
// document/ResizeObserver/MutationObserver/requestAnimationFrame/getComputedStyle 逐一桩掉，
// afterEach 一律删除，桩件绝不泄漏给同进程的后续用例。
//
// 说明：world-map.js 与地图系列（atom-world 的 GeoJSON 抓取、布局与缩放）属 Map 家族，
// 与 MapSeriesTests 同挂；本波 option 皆非地图系列，只验证
// ensureWorldMap/prepareWorldMap/resizeWorldMap 在非地图 option 上的早退不影响主流程。

import assert from 'node:assert/strict';
import { afterEach, test } from 'node:test';

const rendererUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/echarts-renderer.js', import.meta.url);

const installed = [];
const mutationObservers = [];
const resizeObservers = [];
const html = () => globalThis.document.documentElement;

/** 装一个全局桩并在 afterEach 里删除。 */
function stub(name, value) {
    installed.push(name);
    globalThis[name] = value;
    return value;
}

afterEach(() => {
    for (const name of installed) {
        delete globalThis[name];
    }
    installed.length = 0;
    mutationObservers.length = 0;
    resizeObservers.length = 0;
});

/** 图表元素桩：isConnected 可置真以演练脱离文档的竞态。 */
function fakeElement(id) {
    return { id, isConnected: true, clientWidth: 600, clientHeight: 400 };
}

/** ECharts 实例桩：记下 setOption/on/off/resize/dispose 的一切调用。 */
function fakeChart(element) {
    const chart = {
        element,
        options: [],
        listeners: new Map(),
        zrListeners: new Map(),
        offs: [],
        resizes: 0,
        disposed: false,
        setOption(option, opts) {
            chart.options.push({ option, opts });
        },
        on(type, handler) {
            chart.listeners.set(type, handler);
        },
        off(type, handler) {
            chart.offs.push({ scope: 'chart', type, handler });
            if (chart.listeners.get(type) === handler) {
                chart.listeners.delete(type);
            }
        },
        getZr() {
            return {
                on: (type, handler) => chart.zrListeners.set(type, handler),
                off: (type, handler) => {
                    chart.offs.push({ scope: 'zr', type, handler });
                    if (chart.zrListeners.get(type) === handler) {
                        chart.zrListeners.delete(type);
                    }
                }
            };
        },
        resize() {
            chart.resizes++;
        },
        isDisposed() {
            return chart.disposed;
        },
        dispose() {
            chart.disposed = true;
        },
        fire(type, params) {
            chart.listeners.get(type)?.(params);
        },
        fireZr(type, event) {
            chart.zrListeners.get(type)?.(event);
        }
    };
    return chart;
}

/**
 * 把假 ECharts 挂到 window 上，令 loadECharts 走快路而不动态 import 压缩包。
 * @returns {Array<{el: any, theme: any, opts: any, chart: any}>} 建实例台账
 */
function stubEcharts() {
    const created = [];
    stub('window', {
        echarts: {
            init(el, theme, opts) {
                const chart = fakeChart(el);
                created.push({ el, theme, opts, chart });
                return chart;
            },
            getMap: () => {
                throw new Error('非地图 option 不应回读地图');
            },
            registerMap: () => {
                throw new Error('非地图 option 不应注册地图');
            }
        }
    });
    return created;
}

/** .NET 侧 DotNetObjectReference 的等价桩：只记 invokeMethodAsync。 */
function fakeRef() {
    const calls = [];
    return {
        calls,
        identifiers: () => calls.map((call) => call.identifier),
        invokeMethodAsync(identifier, ...args) {
            calls.push({ identifier, args });
            return Promise.resolve();
        }
    };
}

/** 事件处理器可增删可派发的根元素桩。 */
function fakeRoot() {
    const listeners = new Map();
    return {
        listeners,
        addEventListener(type, handler) {
            if (!listeners.has(type)) {
                listeners.set(type, []);
            }
            listeners.get(type).push(handler);
        },
        removeEventListener(type, handler) {
            const list = listeners.get(type) ?? [];
            const index = list.indexOf(handler);
            if (index >= 0) {
                list.splice(index, 1);
            }
        },
        dispatch(type) {
            for (const handler of [...(listeners.get(type) ?? [])]) {
                handler({ type });
            }
        }
    };
}

/**
 * DOM 侧总桩：元素查表、根元素、两类观察器与下一拍。
 * @param {Map<string, any>} elements 图表标识到元素的映射
 */
function stubDom(elements) {
    stub('document', {
        getElementById: (id) => elements.get(id) ?? null,
        documentElement: fakeRoot()
    });

    class FakeMutationObserver {
        constructor(callback) {
            this.callback = callback;
            this.disconnected = 0;
            mutationObservers.push(this);
        }

        observe() {
            this.observing = true;
        }

        disconnect() {
            this.disconnected++;
        }

        emit(mutations) {
            if (this.disconnected > 0) {
                return;
            }
            this.callback(mutations, this);
        }
    }
    stub('MutationObserver', FakeMutationObserver);

    class FakeResizeObserver {
        constructor(callback) {
            this.callback = callback;
            this.observations = [];
            this.disconnected = 0;
            resizeObservers.push(this);
        }

        observe(target) {
            this.observations.push(target);
        }

        disconnect() {
            this.disconnected++;
        }

        emit() {
            if (this.disconnected > 0) {
                return;
            }
            this.callback([]);
        }
    }
    stub('ResizeObserver', FakeResizeObserver);

    const frames = {
        queue: [],
        flush() {
            for (const job of this.queue.splice(0, this.queue.length)) {
                job();
            }
        }
    };
    stub('requestAnimationFrame', (job) => frames.queue.push(job));

    return { frames };
}

* 自定义属性取值桩（option 里含 var 时才需要）。 
function stubStyles(vars) {
    stub('getComputedStyle', () => ({
        getPropertyValue: (name) => vars[name] ?? '',
        color: ''
    }));
}

/**
 * 每用例取一份全新的模块实例：loadECharts 的单飞缓存与 instances 表都是模块级的，
 * 若共享实例，后一个用例会拿前一个用例的假库建图表。加查询串令 URL 各异即可隔离。
 */
let moduleSeq = 0;
async function freshModule() {
    moduleSeq += 1;
    return import(`${rendererUrl.href}?v=${moduleSeq}`);
}

test('导出面正是 ChartBase 调用的四个入口', async () => {
    const mod = await freshModule();

    assert.deepEqual(Object.keys(mod).sort(), ['dispose', 'initialize', 'setClickHandler', 'update']);
});

test('元素查无此人则不建实例不建观察器', async () => {
    const created = stubEcharts();
    stubDom(new Map());
    const { initialize } = await freshModule();

    await initialize('missing', { series: [] });

    assert.equal(created.length, 0);
    assert.equal(mutationObservers.length, 0);
    assert.equal(resizeObservers.length, 0);
});

test('初始化以 svg 渲染器建实例并即刻套用 option', async () => {
    const created = stubEcharts();
    const element = fakeElement('c-init');
    stubDom(new Map([['c-init', element]]));
    const { initialize } = await freshModule();

    await initialize('c-init', { series: [{ type: 'bar', data: [1, 2] }] });

    assert.equal(created.length, 1);
    assert.equal(created[0].el, element);
    assert.equal(created[0].theme, null);
    assert.deepEqual(created[0].opts, { renderer: 'svg' });

    const chart = created[0].chart;
    assert.equal(chart.options.length, 1);
    assert.deepEqual(chart.options[0].option.series[0].data, [1, 2]);
    // 非地图 option：prepareWorldMap 原样交回，既不补 visualMap 也不注册地图。
    assert.equal(chart.options[0].option.visualMap, undefined);

    assert.equal(resizeObservers.length, 1);
    assert.deepEqual(resizeObservers[0].observations, [element]);
    assert.equal(mutationObservers.length, 1, '主题观察器随图表一并建立');
    assert.equal(chart.listeners.size, 0, '未给托管引用就不建点击通路');
    assert.equal(html().listeners.get('atom-theme-changed').length, 1);
});

test('元素在等待库加载那拍脱离文档则止步不建实例', async () => {
    const created = stubEcharts();
    const element = fakeElement('c-away');
    stubDom(new Map([['c-away', element]]));
    const { initialize } = await freshModule();

    element.isConnected = false;
    await initialize('c-away', { series: [] });

    assert.equal(created.length, 0);
    assert.equal(resizeObservers.length, 0);
    assert.equal(mutationObservers.length, 0);
});

test('重复初始化先释放既有实例并断开其观察器', async () => {
    const created = stubEcharts();
    const element = fakeElement('c-reinit');
    stubDom(new Map([['c-reinit', element]]));
    const { initialize } = await freshModule();

    await initialize('c-reinit', { series: [] });
    await initialize('c-reinit', { series: [{ type: 'line' }] });

    assert.equal(created.length, 2);
    assert.equal(created[0].chart.disposed, true);
    assert.equal(created[1].chart.disposed, false);
    assert.equal(resizeObservers[0].disconnected, 1);
    assert.equal(mutationObservers[0].disconnected, 1);
    // 旧主题监听已摘除，只剩新图表那一份。
    assert.equal(html().listeners.get('atom-theme-changed').length, 1);
});

test('主题变更后按 notMerge 重解 CSS 变量并重设 option', async () => {
    const created = stubEcharts();
    stubStyles({ '--chart-1': '#112233' });
    const element = fakeElement('c-theme');
    const dom = stubDom(new Map([['c-theme', element]]));
    const { initialize } = await freshModule();

    await initialize('c-theme', { color: ['var(--chart-1)'], series: [] });
    const chart = created[0].chart;
    assert.deepEqual(chart.options[0].option.color, ['#112233']);

    mutationObservers[0].emit([{ type: 'attributes', attributeName: 'class' }]);
    assert.equal(chart.options.length, 1, '变更要等下一拍才落地');
    dom.frames.flush();

    assert.equal(chart.options.length, 2);
    assert.deepEqual(chart.options[1].opts, { notMerge: true });
    assert.deepEqual(chart.options[1].option.color, ['#112233']);
});

test('尺寸观察器驱动图表重算且断开后不再重算', async () => {
    const created = stubEcharts();
    const element = fakeElement('c-resize');
    stubDom(new Map([['c-resize', element]]));
    const { initialize } = await freshModule();

    await initialize('c-resize', { series: [] });
    const chart = created[0].chart;

    resizeObservers[0].emit();
    assert.equal(chart.resizes, 1);

    // 图表已释放时观察器仍可能被投递一次，须自行守住。
    chart.disposed = true;
    resizeObservers[0].emit();
    assert.equal(chart.resizes, 1);
});

test('交接托管引用即挂数据点与空白两处监听', async () => {
    const created = stubEcharts();
    stubDom(new Map([['c-click', fakeElement('c-click')]]));
    const { initialize, setClickHandler } = await freshModule();
    const ref = fakeRef();

    await initialize('c-click', { series: [] });
    const chart = created[0].chart;
    setClickHandler('c-click', ref);

    assert.equal(typeof chart.listeners.get('click'), 'function');
    assert.equal(typeof chart.zrListeners.get('click'), 'function');
    assert.equal(ref.calls.length, 0, '挂监听本身不派发');

    chart.fire('click', { seriesName: 'Rev', seriesIndex: 0, dataIndex: 2, name: '一月', value: 12.5 });
    assert.deepEqual(ref.calls[0], {
        identifier: 'HandleDataPointClick',
        args: [{
            seriesName: 'Rev',
            seriesIndex: 0,
            dataIndex: 2,
            name: '一月',
            componentType: null,
            value: 12.5,
            values: null
        }]
    });

    chart.fireZr('click', { target: {} });
    assert.equal(ref.calls.length, 1, '落在形状上的点击不算空白');

    chart.fireZr('click', { target: null });
    assert.deepEqual(ref.calls[1], { identifier: 'HandleChartClick', args: [] });
});

test('点击载荷把各类 value 归一为标量或数组', async () => {
    const created = stubEcharts();
    stubDom(new Map([['c-shape', fakeElement('c-shape')]]));
    const { initialize, setClickHandler } = await freshModule();
    const ref = fakeRef();

    await initialize('c-shape', { series: [] });
    const chart = created[0].chart;
    setClickHandler('c-shape', ref);

    chart.fire('click', { value: Number.NaN });
    chart.fire('click', { value: '12' });
    chart.fire('click', { value: ['1', 2, 'x'] });
    chart.fire('click', { value: { amount: 5 } });
    chart.fire('click', { seriesType: 'map', data: { atomSourceIndex: 7 }, dataIndex: 3 });
    chart.fire('click', { seriesType: 'map', data: null });

    const payloads = ref.calls.map((call) => call.args[0]);
    assert.deepEqual(payloads.map((p) => [p.value, p.values]), [
        // 非有限数字不当标量。
        [null, null],
        // 裸字符串标量既不数化也不成组，交由 name/dataIndex 定位。
        [null, null],
        // 数组逐位数化，数不动的记为 0。
        [null, [1, 2, 0]],
        [null, null],
        // 地图系列的载荷也没有额外的值形。
        [null, null],
        [null, null]
    ]);
    assert.equal(payloads[4].dataIndex, 7, '地图系列的 dataIndex 取原始下标而非排序位');
    assert.equal(payloads[5].dataIndex, -1);

    chart.fire('click', { dataIndex: '2', seriesIndex: '1' });
    const loose = ref.calls.at(-1).args[0];
    assert.equal(loose.dataIndex, -1);
    assert.equal(loose.seriesIndex, -1);
    assert.equal(loose.name, null);
    assert.equal(loose.seriesName, null);
});

test('换监听者时旧回调被摘而图表 option 不受牵连', async () => {
    const created = stubEcharts();
    stubDom(new Map([['c-swap', fakeElement('c-swap')]]));
    const { initialize, setClickHandler } = await freshModule();
    const first = fakeRef();
    const second = fakeRef();

    await initialize('c-swap', { series: [{ type: 'bar' }] });
    const chart = created[0].chart;
    setClickHandler('c-swap', first);
    setClickHandler('c-swap', second);

    assert.deepEqual(chart.offs.map((o) => `${o.scope}:${o.type}`), ['chart:click', 'zr:click']);
    chart.fire('click', { value: 1 });
    assert.deepEqual(first.identifiers(), []);
    assert.deepEqual(second.identifiers(), ['HandleDataPointClick']);
    // 订阅关系与 option 各不相干：换监听绝不重设 option。
    assert.equal(chart.options.length, 1);
});

test('撤去引用即摘两路监听且不再派发', async () => {
    const created = stubEcharts();
    stubDom(new Map([['c-off', fakeElement('c-off')]]));
    const { initialize, setClickHandler } = await freshModule();
    const ref = fakeRef();

    await initialize('c-off', { series: [] }, ref);
    const chart = created[0].chart;
    chart.fire('click', { value: 3 });
    assert.deepEqual(ref.identifiers(), ['HandleDataPointClick']);

    setClickHandler('c-off', null);

    assert.equal(chart.listeners.size, 0);
    assert.equal(chart.zrListeners.size, 0);
    assert.equal(chart.offs.length, 2);
    chart.fire('click', { value: 4 });
    chart.fireZr('click', { target: null });
    assert.deepEqual(ref.identifiers(), ['HandleDataPointClick'], '摘除后不再有回音');
});

test('未初始化或已释放的图表对 setClickHandler 无感', async () => {
    const created = stubEcharts();
    stubDom(new Map([['c-dead', fakeElement('c-dead')]]));
    const { initialize, setClickHandler } = await freshModule();

    setClickHandler('c-dead', fakeRef());
    assert.equal(mutationObservers.length, 0, '未知标识不建任何东西');

    const ref = fakeRef();
    await initialize('c-dead', { series: [] });
    const chart = created[0].chart;
    chart.disposed = true;

    setClickHandler('c-dead', ref);
    assert.equal(chart.listeners.size, 0);
    assert.deepEqual(ref.identifiers(), []);
});

test('更新按 notMerge 取舍并记下原始 option', async () => {
    const created = stubEcharts();
    stubStyles({ '--chart-1': '#112233' });
    const dom = stubDom(new Map([['c-update', fakeElement('c-update')]]));
    const { initialize, update } = await freshModule();

    await initialize('c-update', { color: ['var(--chart-1)'], series: [] });
    const chart = created[0].chart;

    await update('c-update', { color: ['#eeeeee'], series: [{ type: 'bar', data: [4] }] });
    assert.equal(chart.options.length, 2);
    assert.deepEqual(chart.options[1].opts, { notMerge: true });
    assert.deepEqual(chart.options[1].option.series[0].data, [4]);

    await update('c-update', { color: ['var(--chart-1)'] }, false);
    assert.deepEqual(chart.options[2].opts, { notMerge: false });
    assert.deepEqual(chart.options[2].option.color, ['#112233'], '更新同样先解 CSS 变量');

     主题重解读用的是最近一次的原始 option（仍是 var 文本而非已解色）。
    mutationObservers[0].emit([{ type: 'attributes', attributeName: 'data-theme' }]);
    assert.equal(chart.options.length, 3, '变更要等下一拍才落地');
    dom.frames.flush();

    assert.equal(chart.options.length, 4);
    assert.deepEqual(chart.options[3].opts, { notMerge: true });
    assert.deepEqual(chart.options[3].option.color, ['#112233']);
});

test('更新对未知标识与已释放实例皆止步', async () => {
    const created = stubEcharts();
    stubDom(new Map([['c-frozen', fakeElement('c-frozen')]]));
    const { initialize, update, dispose } = await freshModule();

    await update('c-unknown', { series: [] });
    assert.equal(created.length, 0);

    await initialize('c-frozen', { series: [] });
    const chart = created[0].chart;
    dispose('c-frozen');

    await update('c-frozen', { series: [{ type: 'line' }] });
    assert.equal(chart.options.length, 1, '释放后不再收更新');
});

test('释放一次性清场且重复释放无害', async () => {
    const created = stubEcharts();
    stubDom(new Map([['c-gone', fakeElement('c-gone')]]));
    const { initialize, dispose } = await freshModule();
    const ref = fakeRef();

    await initialize('c-gone', { series: [] }, ref);
    const chart = created[0].chart;

    dispose('c-gone');

    assert.equal(chart.disposed, true);
    assert.equal(resizeObservers[0].disconnected, 1);
    assert.equal(mutationObservers[0].disconnected, 1);
    assert.equal(html().listeners.get('atom-theme-changed').length, 0, '主题监听随图表一并摘除');

    dispose('c-gone');
    dispose('c-never');
    assert.equal(chart.resizes, 0);
});

test('富文本指标名的雷达图装上转义过的 tooltip formatter', async () => {
    const created = stubEcharts();
    stubDom(new Map([['c-radar', fakeElement('c-radar')]]));
    const { initialize } = await freshModule();
    const option = {
        tooltip: { show: true },
        radar: {
            indicator: [
                { name: '{a|186/80}\n{b|January}', max: 80 },
                { name: '{a|80/120}\n{b|February}', max: 120 }
            ]
        },
        series: [{ type: 'radar', data: [{ value: [5, 6] }] }]
    };

    await initialize('c-radar', option);
    const applied = created[0].chart.options[0].option;
    assert.equal(typeof applied.tooltip.formatter, 'function');

    const htmlText = applied.tooltip.formatter({
        marker: '●',
        seriesName: '<b>&"\'',
        value: [5, 6, 7]
    });

    // 取最后一个富文本记号作干净名，超出指标数的第三位不再展开；来源数据皆转义。
    assert.equal(
        htmlText,
        '● <strong>&lt;b&gt;&amp;&quot;&#39;</strong><br/>January: 5<br/>February: 6'
    );
    assert.equal(option.tooltip.formatter, undefined, '装配落在克隆件上，原始 option 不动');
});

test('无 tooltip 或无富文本记号时装不上 formatter', async () => {
    const created = stubEcharts();
    const elements = new Map([
        ['c-plain', fakeElement('c-plain')],
        ['c-plain2', fakeElement('c-plain2')]
    ]);
    stubDom(elements);
    const { initialize } = await freshModule();

    // 有富文本指标名却无 tooltip：门条件不满足。
    await initialize('c-plain', { radar: { indicator: [{ name: '{a|1/2}\n{b|March}' }] }, series: [] });
    assert.equal(created[0].chart.options[0].option.tooltip, undefined);

    // 有 tooltip 却全是干净指标名：不去抢用户自己的 formatter。
    await initialize('c-plain2', { tooltip: {}, radar: { indicator: [{ name: 'March' }] }, series: [] });
    assert.equal(created[1].chart.options[0].option.tooltip.formatter, undefined);
});
