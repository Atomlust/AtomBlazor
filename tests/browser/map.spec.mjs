import { test, expect } from '@playwright/test';

const selector = '[data-slot="map-chart"]';
const engine = '/_content/AtomBlazor.Components/lib/echarts/echarts.min.js';
// 偏差（授权）：goto 统一等 networkidle——WebAssembly 宿主下 load 事件远早于运行时
// 装配完成，首屏 8s 断言会抢跑（ net10 的运行时装配更快）。

async function readMap(page, index = 0) {
    return page.evaluate(async ({ index, selector, engine }) => {
        const echarts = await import(engine);
        const chart = echarts.getInstanceByDom(document.querySelectorAll(selector)[index]);
        if (!chart) return null;
        const option = chart.getOption();
        const data = chart.getModel().getSeriesByIndex(0).getData();
        const region = name => {
            const i = data.indexOfName(name);
            // MapDraw applies areaColor to the rendered region, after visualMap processing.
            let fill;
            data.getItemGraphicEl(i)?.traverse(child => { fill ??= child.style?.fill; });
            return { value: data.get('value', i), fill };
        };
        return {
            count: data.count(), usa: region('United States of America'), mexico: region('Mexico'),
            max: option.visualMap[0].max, noDataColor: option.series[0].itemStyle.areaColor,
            zoom: option.series[0].zoom, palette: option.visualMap[0].inRange.color,
            aspect: (() => {
                const origin = chart.convertToPixel({ seriesIndex: 0 }, [0, 0]);
                const diagonal = chart.convertToPixel({ seriesIndex: 0 }, [10, 10]);
                return Math.abs((diagonal[0] - origin[0]) / (diagonal[1] - origin[1]));
            })()
        };
    }, { index, selector, engine });
}

async function mapPoint(page, coordinates, index = 0) {
    return page.evaluate(async ({ selector, engine, coordinates, index }) => {
        const echarts = await import(engine);
        const element = document.querySelectorAll(selector)[index];
        const [x, y] = echarts.getInstanceByDom(element).convertToPixel({ seriesIndex: 0 }, coordinates);
        const rect = element.getBoundingClientRect();
        return { x: rect.left + x, y: rect.top + y, relativeX: x, width: rect.width };
    }, { selector, engine, coordinates, index });
}

async function clickCountry(page, coordinates) {
    const point = await mapPoint(page, coordinates);
    await page.mouse.move(point.x, point.y);
    // Let ECharts paint the emphasis label before pressing: otherwise WebKit can
    // hit the region on pointer down and the new label on pointer up.
    await page.evaluate(() => new Promise(resolve =>
        requestAnimationFrame(() => requestAnimationFrame(resolve))));
    await page.mouse.click(point.x, point.y);
}

test('world maps share geometry, update data and preserve country click indices', async ({ page }) => {
    const errors = [];
    let mapRequests = 0;
    page.on('pageerror', error => errors.push(error.message));
    page.on('request', request => { if (request.url().endsWith('/maps/world.json')) mapRequests++; });
    await page.goto('/charts/map', { waitUntil: 'networkidle' });
    await expect(page.locator(`${selector} svg`)).toHaveCount(2);
    await expect.poll(async () => (await readMap(page))?.usa.value).toBe(12400);
    const initial = await readMap(page);
    expect(initial.count).toBe(241);
    expect(initial.max).toBe(12400);
    expect(initial.mexico.fill).toBe(initial.noDataColor);
    expect(initial.usa.fill).not.toBe(initial.mexico.fill);
    expect(mapRequests).toBe(1);

    await clickCountry(page, [-100, 40]);
    await expect(page.getByRole('status')).toContainText('12,400 visitors (dataset row 1)');
    await clickCountry(page, [-102, 23]);
    await expect(page.getByRole('status')).toHaveText('Mexico: no visitor data.');
    await page.getByRole('button', { name: 'Show last 30 days' }).click();
    await expect.poll(async () => (await readMap(page))?.usa.value).toBe(49600);
    expect((await readMap(page)).max).toBe(49600);
    expect((await readMap(page, 1)).max).toBe(50000);
    expect((await readMap(page, 1)).palette).toEqual(['#dbeafe', '#60a5fa', '#1d4ed8']);
    expect(errors).toEqual([]);
    await page.screenshot({ path: 'test-results/map-desktop.png', fullPage: true });
});

test('map colors follow the theme and the map resizes for mobile', async ({ page }) => {
    await page.goto('/charts/map', { waitUntil: 'networkidle' });
    await expect(page.locator(`${selector} svg`)).toHaveCount(2);
    const before = await readMap(page);
    await page.evaluate(() => {
        document.documentElement.classList.toggle('dark');
        document.documentElement.style.setProperty('--muted', '#123456');
    });
    await expect.poll(async () => (await readMap(page))?.mexico.fill).not.toBe(before.mexico.fill);
    const themed = await readMap(page);
    expect(themed.mexico.fill).toBe(themed.noDataColor);
    await page.setViewportSize({ width: 390, height: 844 });
    await expect.poll(async () => +(await page.locator(`${selector} svg`).first().getAttribute('width'))).toBeLessThanOrEqual(390);
    const bounds = await page.locator(selector).first().boundingBox();
    expect(bounds.width).toBeGreaterThan(0);
    expect(bounds.width).toBeLessThanOrEqual(390);
    const west = await mapPoint(page, [-170, 0]);
    const east = await mapPoint(page, [170, 0]);
    expect(west.relativeX).toBeGreaterThanOrEqual(0);
    expect(east.relativeX).toBeLessThanOrEqual(east.width);
    expect((await readMap(page)).aspect).toBeCloseTo(before.aspect);
    expect((await readMap(page)).usa.value).toBe(12400);
    await page.screenshot({ path: 'test-results/map-mobile.png', fullPage: true });
});

test('pan and zoom are opt-in', async ({ page }) => {
    await page.goto('/charts/map', { waitUntil: 'networkidle' });
    await expect(page.locator(`${selector} svg`)).toHaveCount(2);
    const fixed = await readMap(page);
    const fixedPoint = await mapPoint(page, [0, 20]);
    await page.mouse.move(fixedPoint.x, fixedPoint.y);
    await page.mouse.wheel(0, -250);
    expect((await readMap(page)).zoom).toBe(fixed.zoom);
    await page.locator(selector).nth(1).scrollIntoViewIfNeeded();
    const before = await readMap(page, 1);
    const point = await mapPoint(page, [0, 20], 1);
    await page.mouse.move(point.x, point.y);
    await page.mouse.wheel(0, -250);
    await expect.poll(async () => (await readMap(page, 1)).zoom).toBeGreaterThan(before.zoom);
    const zoomed = (await readMap(page, 1)).zoom;
    await page.setViewportSize({ width: 1000, height: 800 });
    await expect.poll(async () => (await readMap(page, 1)).zoom).toBe(zoomed);
});

test('a chart without maps does not request world geometry', async ({ page }) => {
    let mapRequests = 0;
    page.on('request', request => { if (request.url().endsWith('/maps/world.json')) mapRequests++; });
    await page.goto('/charts/line', { waitUntil: 'networkidle' });
    await expect(page.locator('[data-slot="line-chart"] svg').first()).toBeVisible();
    expect(mapRequests).toBe(0);
});
