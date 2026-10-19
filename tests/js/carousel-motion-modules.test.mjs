// carousel.js 与 motion.js 的真实文件导入测试。
// 两个模块都刻意保持顶层零 DOM 触碰（WeakMap/纯函数/惰性箭头），document/matchMedia
// 只在函数体内部访问，因此可以在无 DOM 的 Node 环境以 file-URL 单模块导入验证——
// 与 theme.js 同一先例。components-core.js barrel 不受影响：这两个模块与源形态一致
// 走独立懒加载 URL（JsModules 按电路缓存），故此处验证的是裸导出面本身，
// 与 C# 侧 InvokeVoidAsync("configure"/"motion"/…) 的标识符互为镜像。

import assert from 'node:assert/strict';
import { test } from 'node:test';

const carouselUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/carousel.js', import.meta.url);
const motionUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/motion.js', import.meta.url);

test('carousel.js 可导入且导出面与 Carousel 组件的裸标识符一一对应', async () => {
    const mod = await import(carouselUrl.href);

    // C# 侧调用点：module.InvokeVoidAsync("configure"|"dispose", …)。
    for (const name of ['configure', 'dispose', 'maximumStart']) {
        assert.equal(typeof mod[name], 'function', `carousel.js 缺少导出 ${name}`);
    }
});

test('carousel.js maximumStart 纯函数表驱动', async () => {
    const { maximumStart } = await import(carouselUrl.href);

    // 最大距离 <= 1：没有可滑动空间，恒为 0。
    assert.equal(maximumStart([0, 300, 600], 1), 0);
    assert.equal(maximumStart([0, 300, 600], 0), 0);
    // 首个偏移量达到"最大距离-1"的索引即为末张可对齐幻灯片。
    assert.equal(maximumStart([0, 300, 600], 599), 2);
    assert.equal(maximumStart([0, 300, 600], 301), 1);
    assert.equal(maximumStart([0, 300, 600], 100), 1);
    // 全部偏移量都不足时落在最后一张。
    assert.equal(maximumStart([0, 100], 500), 1);
});

test('motion.js 可导入且导出面与 MotionInterop 的裸标识符一一对应', async () => {
    const mod = await import(motionUrl.href);

    // C# 侧调用点：InvokeVoidAsync("motion"|"height"|"indicator"|"play"|"dispose", …)。
    for (const name of ['motion', 'height', 'indicator', 'play', 'dispose', 'presetFrames']) {
        assert.equal(typeof mod[name], 'function', `motion.js 缺少导出 ${name}`);
    }
});

test('motion.js presetFrames 预设形状表驱动', async () => {
    const { presetFrames } = await import(motionUrl.href);

    // Fade（含未知预设回退）：两帧透明度。
    assert.deepEqual(presetFrames('Fade'), [{ opacity: 0 }, { opacity: 1 }]);
    assert.deepEqual(presetFrames('Nonsense'), [{ opacity: 0 }, { opacity: 1 }]);

    // 滑动预设：带距离的 translate 起点、零位移终点。
    assert.deepEqual(presetFrames('SlideUp', 16)[0], { opacity: 0, transform: 'translate(0px, 16px)' });
    assert.deepEqual(presetFrames('SlideDown', 16)[0], { opacity: 0, transform: 'translate(0px, -16px)' });
    assert.deepEqual(presetFrames('SlideLeft', 16)[0], { opacity: 0, transform: 'translate(16px, 0px)' });
    assert.deepEqual(presetFrames('SlideRight', 16)[0], { opacity: 0, transform: 'translate(-16px, 0px)' });
    assert.deepEqual(presetFrames('SlideUp', 16)[1], { opacity: 1, transform: 'translate(0, 0)' });

    // 缩放与回弹：终点 scale(1)。
    assert.deepEqual(presetFrames('Scale')[0], { opacity: 0, transform: 'scale(.92)' });
    assert.equal(presetFrames('Spring').at(-1).transform, 'scale(1)');
    assert.equal(presetFrames('Spring').length, 3);

    // 抖动与脉冲：端点回到中性值。
    assert.equal(presetFrames('ShakeX').at(0).transform, 'translateX(0px)');
    assert.equal(presetFrames('ShakeX').at(-1).transform, 'translateX(0px)');
    assert.equal(presetFrames('ShakeX').length, 6);
    assert.equal(presetFrames('ShakeY').length, 6);
    assert.deepEqual(presetFrames('Pulse'), [{ transform: 'scale(1)' }, { transform: 'scale(1.05)' }, { transform: 'scale(1)' }]);
    assert.equal(presetFrames('Bounce').length, 5);
    assert.equal(presetFrames('Bounce').at(-1).transform, 'translateY(0px)');
});

test('motion.js dispose 对未知元素静默返回（幂等清理契约）', async () => {
    const { dispose } = await import(motionUrl.href);

    // C# 释放路径会在从未配置过的元素上调用 dispose——必须不抛异常。
    dispose({});
});
