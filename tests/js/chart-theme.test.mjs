// chart-theme.js 的纯导出面测试（Node 环境，无浏览器）。
//
// 本模块顶层不触碰 document/window，只有函数体内才按需取用 getComputedStyle、
// document、MutationObserver、requestAnimationFrame，因此可以在 Node 里直接 import，
// 再按本仓既有先例（见 cascader-module.test.mjs、calendar-focus.test.mjs）逐测打桩——
// afterEach 一律删除本轮装上的全局件，绝不让桩泄漏给同进程的后续用例。
//
// 覆盖意图分三层：
 1) resolveThemeColors 的护栏（空入参原样返回）、深克隆与不改写原对象；
 2) var 求值链——命中自定义属性、fallback、无 fallback 保留原文、一处多 var；
 3) oklch 换算的浏览器无关数学（oklchToRgb 未导出，只能经此间接驱动）与
 color-mix 的 DOM 兜底；watchThemeChanges 的观察选项、延迟一拍派发与清理。
//
// 说明：echarts-renderer.js 的兄弟件 world-map.js 属 Map 家族，
// 与 MapSeriesTests 一同 ，本波不在 Node 侧测它。

import assert from 'node:assert/strict';
import { afterEach, test } from 'node:test';

const themeUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/chart-theme.js', import.meta.url);

const installed = [];

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
});

/** 元素桩：只作 getComputedStyle 的身份参数，本身不参与求值。 */
function fakeElement() {
    return { id: 'chart-1', isConnected: true };
}

/**
 * getComputedStyle 桩。
 * vars 供自定义属性取值；color 供 color-mix 的临时元素回读；
 * calls 记录每次回读的标识，用以核对解析次数。
 */
function stubStyles(vars, color = '') {
    const reads = [];
    stub('getComputedStyle', (target) => {
        reads.push(target);
        return {
            getPropertyValue: (name) => vars[name] ?? '',
            color
        };
    });
    return reads;
}

/** document 桩：只为 color-mix 兜底提供临时元素的挂载/摘除记录。 */
function stubDocument() {
    const appended = [];
    const removed = [];
    stub('document', {
        createElement: () => ({ style: {} }),
        documentElement: {
            appendChild: (node) => appended.push(node),
            removeChild: (node) => removed.push(node)
        }
    });
    return { appended, removed };
}

test('导出面只有 resolveThemeColors 与 watchThemeChanges', async () => {
    const mod = await import(themeUrl.href);

    // 换算器与兜底器都刻意留作内部件：C# 侧只经 option 字符串间接触达。
    assert.deepEqual(Object.keys(mod).sort(), ['resolveThemeColors', 'watchThemeChanges']);
    assert.equal(typeof mod.resolveThemeColors, 'function');
    assert.equal(typeof mod.watchThemeChanges, 'function');
});

test('option 或元素缺失时原样返回同一引用', async () => {
    const { resolveThemeColors } = await import(themeUrl.href);
    const element = fakeElement();
    const option = { color: 'var(--chart-1)' };

    assert.equal(resolveThemeColors(null, element), null);
    assert.equal(resolveThemeColors(undefined, element), undefined);
    assert.equal(resolveThemeColors(false, element), false);
    assert.equal(resolveThemeColors(option, null), option);
    assert.equal(resolveThemeColors(option, undefined), option);
    // 早退于克隆之前：给回的就是原对象。
    assert.equal(resolveThemeColors(option, 0), option);
});

test('无 var() 引用时值照旧而对象另起一份', async () => {
    const { resolveThemeColors } = await import(themeUrl.href);
    const option = {
        animation: true,
        title: { text: 'Revenue', left: 'center' },
        series: [{ type: 'bar', data: [1, 2, 3], barGap: '-100%' }],
        grid: null,
        extra: undefined
    };

    const resolved = resolveThemeColors(option, fakeElement());

    assert.notEqual(resolved, option);
    assert.deepEqual(resolved, option);
    // 布尔与数字未被字符串化；嵌套数组仍是数组。
    assert.equal(resolved.animation, true);
    assert.equal(resolved.series[0].data[0], 1);
    assert.equal(resolved.series[0].barGap, '-100%');
    assert.ok('extra' in resolved);
    assert.equal(resolved.extra, undefined);
});

test('解析改写的是克隆件而非入参对象', async () => {
    const { resolveThemeColors } = await import(themeUrl.href);
    stubStyles({ '--chart-1': '#11aa22' });

    const option = { color: ['var(--chart-1)'], series: [{ itemStyle: { color: 'var(--chart-1)' } }] };
    const resolved = resolveThemeColors(option, fakeElement());

    assert.deepEqual(resolved.color, ['#11aa22']);
    assert.equal(resolved.series[0].itemStyle.color, '#11aa22');
    assert.deepEqual(option.color, ['var(--chart-1)']);
    assert.equal(option.series[0].itemStyle.color, 'var(--chart-1)');
});

test('hex 与 rgb 与 hsl 计算值走快路径原样交付', async () => {
    const { resolveThemeColors } = await import(themeUrl.href);
    stubStyles({
        '--hex-short': '#abc',
        '--hex-long': '#aabbcc',
        '--hex-alpha': '#aabbccdd',
        '--rgb': 'rgb(1, 2, 3)',
        '--rgba': 'rgba(1, 2, 3, 0.5)',
        '--hsl': 'hsl(120, 50%, 50%)'
    });

    const resolved = resolveThemeColors({
        a: 'var(--hex-short)',
        b: 'var(--hex-long)',
        c: 'var(--hex-alpha)',
        d: 'var(--rgb)',
        e: 'var(--rgba)',
        f: 'var(--hsl)'
    }, fakeElement());

    assert.deepEqual(resolved, {
        a: '#abc',
        b: '#aabbcc',
        c: '#aabbccdd',
        d: 'rgb(1, 2, 3)',
        e: 'rgba(1, 2, 3, 0.5)',
        f: 'hsl(120, 50%, 50%)'
    });
});

test('oklch 自定义属性按纯数学换算为 rgb', async () => {
    const { resolveThemeColors } = await import(themeUrl.href);
    stubStyles({
        '--black': 'oklch(0 0 0)',
        '--white': 'oklch(1 0 0)',
        '--mid-gray': 'oklch(0.5 0 0)'
    });

    const resolved = resolveThemeColors(
        { a: 'var(--black)', b: 'var(--white)', c: 'var(--mid-gray)' },
        fakeElement()
    );

    // Oklab 参考矩阵的三行系数各自和为 1，故 C=0 时线性值即 L^3：
    // L=0.5 → 0.125 → gamma 后 0.3886 → 99。
    assert.deepEqual(resolved, { a: 'rgb(0, 0, 0)', b: 'rgb(255, 255, 255)', c: 'rgb(99, 99, 99)' });
});

test('oklch 的写法差异皆归一', async () => {
    const { resolveThemeColors } = await import(themeUrl.href);
    stubStyles({
        '--decimal': 'oklch(0.5 0 0)',
        '--percent': 'oklch(50% 0 0)',
        '--comma': 'oklch(0.5, 0, 0)',
        '--bare': '0.5 0 0',
        '--alpha': 'oklch(0.5 0 0 / 0.35)'
    });

    const resolved = resolveThemeColors({
        decimal: 'var(--decimal)',
        percent: 'var(--percent)',
        comma: 'var(--comma)',
        bare: 'var(--bare)',
        alpha: 'var(--alpha)'
    }, fakeElement());

    // L 的百分比形、逗号分隔、无包裹的裸令牌与带 alpha 后缀都折到同一 rgb。
    assert.deepEqual(Object.values(resolved), Array(5).fill('rgb(99, 99, 99)'));
});

test('oklch 越出 sRGB 色域时钳进 0 到 255', async () => {
    const { resolveThemeColors } = await import(themeUrl.href);
    stubStyles({ '--hot': 'oklch(1 0.4 120)', '--cold': 'oklch(0 0.4 120)' });

    const resolved = resolveThemeColors({ hot: 'var(--hot)', cold: 'var(--cold)' }, fakeElement());

    const bytes = (s) => s.match(/\d+/g).map(Number);
    for (const value of Object.values(resolved)) {
        assert.match(value, /^rgb\(\d{1,3}, \d{1,3}, \d{1,3}\)$/);
        for (const byte of bytes(value)) {
            assert.ok(byte >= 0 && byte <= 255, `钳制越界：${value}`);
        }
    }

    // 满彩度绿在 sRGB 之外：L=1 时绿被钳到顶、蓝被钳到底，红留在色域内；
    // L=0 时红蓝皆落到 0 的下界。
    const hot = bytes(resolved.hot);
    const cold = bytes(resolved.cold);
    assert.equal(hot[1], 255);
    assert.equal(hot[2], 0);
    assert.ok(hot[0] > 0 && hot[0] < 255, `红通道不该被钳制：${resolved.hot}`);
    assert.equal(cold[0], 0);
    assert.equal(cold[2], 0);
});

test('属性无值时取 fallback 并去掉两端空白', async () => {
    const { resolveThemeColors } = await import(themeUrl.href);
    stubStyles({ '--set': '#000000' });

    const resolved = resolveThemeColors({
        withSpace: 'var(--missing,   #ff0000   )',
        emptyComputed: 'var(--blank, rgba(0, 0, 0, 0.25))',
        mixed: 'var(--set, #eeeeee)'
    }, fakeElement());

    assert.deepEqual(resolved, {
        withSpace: '#ff0000',
        emptyComputed: 'rgba(0, 0, 0, 0.25)',
        // 属性有值时 fallback 绝不被采用。
        mixed: '#000000'
    });
});

test('既无值也无 fallback 时保留 var() 原文', async () => {
    const { resolveThemeColors } = await import(themeUrl.href);
    stubStyles({});

    const resolved = resolveThemeColors({
        bare: 'var(--nope)',
        spaced: 'var( --nope2 )',
        wrapped: '1px solid var(--nope3)'
    }, fakeElement());

    assert.deepEqual(resolved, {
        bare: 'var(--nope)',
        spaced: 'var( --nope2 )',
        wrapped: '1px solid var(--nope3)'
    });
});

test('非法变量名不匹配故整体原样返回', async () => {
    const { resolveThemeColors } = await import(themeUrl.href);
    stubStyles({ '--Chart-1': '#000000' });

    // 下划线与点号不在 --[a-zA-Z0-9-]+ 之内，正则即不命中。
    const resolved = resolveThemeColors({
        underscore: 'var(--chart_1)',
        dotted: 'var(--chart.1)',
        nonAscii: 'var(--图表)',
        declared: 'var(--Chart-1)'
    }, fakeElement());

    assert.deepEqual(resolved, {
        underscore: 'var(--chart_1)',
        dotted: 'var(--chart.1)',
        nonAscii: 'var(--图表)',
        declared: '#000000'
    });
});

test('同串多处 var() 皆被替换且大小写敏感', async () => {
    const { resolveThemeColors } = await import(themeUrl.href);
    stubStyles({ '--a': '#111111', '--b': 'oklch(0 0 0)' });

     test 会把 lastIndex 推到串尾；源码显式归零，故第二处 var 同样被换。
    const resolved = resolveThemeColors({
        gradient: 'linear-gradient(var(--a), var(--b))',
        upper: 'VAR(--a)'
    }, fakeElement());

    assert.equal(resolved.gradient, 'linear-gradient(#111111, rgb(0, 0, 0))');
     正则无 i 标志：大写 VAR 原样不动。
    assert.equal(resolved.upper, 'VAR(--a)');
});

test('深层对象与数组与混合层级一并解析', async () => {
    const { resolveThemeColors } = await import(themeUrl.href);
    stubStyles({ '--c1': '#111111', '--c2': '#222222' });

    const resolved = resolveThemeColors({
        color: ['var(--c1)', 'var(--c2)', ['#111111']],
        series: [
            { itemStyle: { color: { type: 'linear', colorStops: [{ color: 'var(--c1)' }, { color: 'var(--c2)' }] } } },
            { lineStyle: { color: 'var(--c2)' } }
        ],
        counts: [0, 1]
    }, fakeElement());

    assert.deepEqual(resolved.color, ['#111111', '#222222', ['#111111']]);
    assert.equal(resolved.series[0].itemStyle.color.colorStops[0].color, '#111111');
    assert.equal(resolved.series[0].itemStyle.color.colorStops[1].color, '#222222');
    assert.equal(resolved.series[1].lineStyle.color, '#222222');
    assert.deepEqual(resolved.counts, [0, 1]);
});

test('color-mix 经临时元素兜底并成对挂载摘除', async () => {
    const { resolveThemeColors } = await import(themeUrl.href);
    stubStyles({ '--mix': 'color-mix(in oklab, #ff0000 50%, white)' }, 'rgb(255, 128, 128)');
    const { appended, removed } = stubDocument();

    const resolved = resolveThemeColors({ fill: 'var(--mix)' }, fakeElement());

    assert.equal(resolved.fill, 'rgb(255, 128, 128)');
    assert.equal(appended.length, 1);
    assert.deepEqual(removed, appended);
});

test('兜底回读为空时保留 color-mix 原文', async () => {
    const { resolveThemeColors } = await import(themeUrl.href);
    stubStyles({ '--mix': 'color-mix(in oklab, #ff0000 50%, white)' }, '');
    const { appended } = stubDocument();

    const resolved = resolveThemeColors({ fill: 'var(--mix)' }, fakeElement());

    // 无果时不造假色：文本原样留着，交回 ECharts 自行取舍。
    assert.equal(resolved.fill, 'color-mix(in oklab, #ff0000 50%, white)');
    // 首轮 convertToUsableColor 回原文后，resolveValue 察觉仍是 color-mix 再试一轮。
    assert.equal(appended.length, 2);
});

test('兜底回读透明色视为解析失败', async () => {
    const { resolveThemeColors } = await import(themeUrl.href);
    stubStyles({ '--weird': 'color-mix(in srgb, unknown)' }, 'rgba(0, 0, 0, 0)');
    stubDocument();

    const resolved = resolveThemeColors({ fill: 'var(--weird)' }, fakeElement());

    // rgba(0,0,0,0) 是浏览器对无法识别颜色表达式的默认回读，不能当色值交付；
    // 于是仍回原文，经 resolveValue 的 color-mix 复查后原样留存。
    assert.equal(resolved.fill, 'color-mix(in srgb, unknown)');
});

test('兜底回读仍是 oklch 时二次换算为 rgb', async () => {
    const { resolveThemeColors } = await import(themeUrl.href);
    // Chrome 111+ 对 oklch 输入不再归一化，DOM 回读依旧给 oklch。
    stubStyles({ '--mixed': 'color-mix(in oklab, oklch(0.5 0 0) 100%, white)' }, 'oklch(0.5 0 0)');
    stubDocument();

    const resolved = resolveThemeColors({ fill: 'var(--mixed)' }, fakeElement());

    assert.equal(resolved.fill, 'rgb(99, 99, 99)');
});

test('无法解析的 oklch 文本原样保留', async () => {
    const { resolveThemeColors } = await import(themeUrl.href);
    stubStyles({ '--bad': 'oklch(not a color)' });
    stubDocument();

    const resolved = resolveThemeColors({ fill: 'var(--bad)' }, fakeElement());

    // 数学换算器认不出就退回 DOM，DOM 也无果则不改动文本。
    assert.equal(resolved.fill, 'oklch(not a color)');
});

test('watchThemeChanges 以属性过滤器观察根元素并回传清理函数', async () => {
    const { watchThemeChanges } = await import(themeUrl.href);
    const html = fakeNode();
    stub('document', { documentElement: html });
    const observers = stubObserverHub();
    stubFrames();

    const cleanup = watchThemeChanges(() => { });

    assert.equal(typeof cleanup, 'function');
    assert.equal(observers.length, 1);
    assert.equal(observers[0].observed.target, html);
    assert.deepEqual(observers[0].observed.options, {
        attributes: true,
        attributeFilter: ['class', 'data-theme']
    });
    assert.equal(html.listeners.get('atom-theme-changed').length, 1);
});

test('class 或 data-theme 变更延后一拍只派发一次', async () => {
    const { watchThemeChanges } = await import(themeUrl.href);
    const html = fakeNode();
    stub('document', { documentElement: html });
    const observers = stubObserverHub();
    const frames = stubFrames();
    let calls = 0;

    watchThemeChanges(() => calls++);

    observers[0].emit([
        { type: 'attributes', attributeName: 'class' },
        { type: 'attributes', attributeName: 'data-theme' }
    ]);
    assert.equal(calls, 0, '回调要等 CSS 变量更新完的那一拍');
    assert.equal(frames.queue.length, 1, '同批变更只排一次回调');
    frames.flush();
    assert.equal(calls, 1);

    // 非属性变更与被过滤掉的属性都不排队。
    observers[0].emit([{ type: 'childList' }]);
    observers[0].emit([{ type: 'attributes', attributeName: 'style' }]);
    assert.equal(frames.queue.length, 0);
});

test('atom-theme-changed 自定义事件同样经下一拍派发', async () => {
    const { watchThemeChanges } = await import(themeUrl.href);
    const html = fakeNode();
    stub('document', { documentElement: html });
    stubObserverHub();
    const frames = stubFrames();
    let calls = 0;

    watchThemeChanges(() => calls++);
    html.dispatch('atom-theme-changed');

    assert.equal(calls, 0);
    assert.equal(frames.queue.length, 1);
    frames.flush();
    assert.equal(calls, 1);
});

test('清理函数断开观察器并摘去同一处理器', async () => {
    const { watchThemeChanges } = await import(themeUrl.href);
    const html = fakeNode();
    stub('document', { documentElement: html });
    const observers = stubObserverHub();
    const frames = stubFrames();
    let calls = 0;

    const cleanup = watchThemeChanges(() => calls++);
    const handler = html.listeners.get('atom-theme-changed')[0];

    cleanup();

    assert.equal(observers[0].disconnected, 1);
    assert.equal(html.listeners.get('atom-theme-changed').length, 0);
    // 摘去的是同一函数引用，不是复制体。
    assert.equal(html.removed[0].handler, handler);

    // 断开的观察器不再投递变更；注销后自定义事件亦无人接手。
    observers[0].emit([{ type: 'attributes', attributeName: 'class' }]);
    html.dispatch('atom-theme-changed');
    frames.flush();
    assert.equal(frames.queue.length, 0);
    assert.equal(calls, 0, '清理后派发不再抵达业务回调');
});

/** 事件处理器可增删可派发的节点桩。 */
function fakeNode() {
    const listeners = new Map();
    const removed = [];
    return {
        listeners,
        removed,
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
            removed.push({ type, handler });
        },
        dispatch(type) {
            for (const handler of [...(listeners.get(type) ?? [])]) {
                handler({ type });
            }
        }
    };
}

/** MutationObserver 桩：登记实例，暴露 emit 以手工触发变更批次。 */
function stubObserverHub() {
    const observers = [];
    class FakeMutationObserver {
        constructor(callback) {
            this.callback = callback;
            this.observed = null;
            this.disconnected = 0;
            observers.push(this);
        }

        observe(target, options) {
            this.observed = { target, options };
        }

        disconnect() {
            this.disconnected++;
        }

        emit(mutations) {
            // 真实浏览器里 disconnect 之后的变更不再投递，桩件同样止步于此。
            if (this.disconnected > 0) {
                return;
            }
            this.callback(mutations, this);
        }
    }
    stub('MutationObserver', FakeMutationObserver);
    return observers;
}

/** requestAnimationFrame 桩：只入队不执行，由用例自行 flush 那一拍。 */
function stubFrames() {
    const queue = [];
    const frames = {
        queue,
        pushed: 0,
        flush() {
            const pending = queue.splice(0, queue.length);
            for (const job of pending) {
                job();
            }
        }
    };
    stub('requestAnimationFrame', (job) => queue.push(job));
    return frames;
}
