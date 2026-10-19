/**
 * 签名画板基元的墨迹引擎。
 *
 * 用直线连接指针采样点画出的签名看起来像鼠标涂鸦。让它读起来像墨迹的是两件事：
 * 路径是穿过采样点的光滑贝塞尔曲线而非折线，且线条随笔速加快而变细。两件事都在这里、
 * 在浏览器里完成——C# 只在一笔结束时听到一次调用，绘制过程中从不被打扰。
 *
 * 这里存储的唯一内容是几何。颜色在渲染时从元素上解析，所以同一份签名会以当前主题重绘，
 * 导出的 SVG 也能用 `currentColor`，而不是烙死一条在白纸上会消失的白色描边。
 */

const padStates = new Map();

/** 用圆点填充曲线时，每像素曲线长度的子步数。低于此值会出现条带。 */
const CURVE_STEPS_PER_PIXEL = 2;

/** 近似曲线弧长所用的采样数。够用来据以确定步数即可。 */
const LENGTH_SAMPLES = 10;

// ---------------------------------------------------------------------------
// 几何
// ---------------------------------------------------------------------------

const distance = (a, b) => Math.hypot(b.x - a.x, b.y - a.y);

/** 两个采样点之间的速度，以像素每毫秒计；时间戳相同时为零。 */
function velocityBetween(a, b) {
    const elapsed = b.t - a.t;
    return elapsed > 0 ? distance(a, b) / elapsed : 0;
}

/**
 * 三个采样点中点段所需的两个贝塞尔控制点。
 *
 * 每对采样贡献其中点；中点经过平移让曲线穿过中间那个采样而非抄近角，并按段长加权，
 * 使长段接短段时不会过冲。
 */
function controlPoints(s1, s2, s3) {
    const m1 = { x: (s1.x + s2.x) / 2, y: (s1.y + s2.y) / 2 };
    const m2 = { x: (s2.x + s3.x) / 2, y: (s2.y + s3.y) / 2 };

    const l1 = distance(s1, s2);
    const l2 = distance(s2, s3);
    const k = l1 + l2 > 0 ? l2 / (l1 + l2) : 0;

    const centre = { x: m2.x + (m1.x - m2.x) * k, y: m2.y + (m1.y - m2.y) * k };
    const shiftX = s2.x - centre.x;
    const shiftY = s2.y - centre.y;

    return [
        { x: m1.x + shiftX, y: m1.y + shiftY },
        { x: m2.x + shiftX, y: m2.y + shiftY }
    ];
}

const bezierAt = (t, p0, p1, p2, p3) => {
    const u = 1 - t;
    return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
};

const pointOnCurve = (curve, t) => ({
    x: bezierAt(t, curve.start.x, curve.c1.x, curve.c2.x, curve.end.x),
    y: bezierAt(t, curve.start.y, curve.c1.y, curve.c2.y, curve.end.y)
});

function curveLength(curve) {
    let length = 0;
    let previous = curve.start;
    for (let i = 1; i <= LENGTH_SAMPLES; i++) {
        const current = pointOnCurve(curve, i / LENGTH_SAMPLES);
        length += distance(previous, current);
        previous = current;
    }
    return length;
}

// ---------------------------------------------------------------------------
// 笔画构建
// ---------------------------------------------------------------------------

/**
 * 重置一笔把采样变成曲线所需的增量状态。
 *
 * 正在绘制的笔与在缩放、撤销或导出时重放的笔走的是同一段例程，所以重绘出的签名与
 * 当初画下的完全一致。
 */
function beginStroke(stroke, pen) {
    stroke.buffer = [];
    stroke.lastVelocity = 0;
    stroke.lastWidth = (pen.minWidth + pen.maxWidth) / 2;
}

/** 两个采样之间线段的半宽：移动越快线条越细。 */
function curveWidths(stroke, pen, from, to) {
    const velocity = pen.velocityWeight * velocityBetween(from, to)
        + (1 - pen.velocityWeight) * stroke.lastVelocity;
    const width = Math.max(pen.maxWidth / (velocity + 1), pen.minWidth);

    const widths = { start: stroke.lastWidth, end: width };
    stroke.lastVelocity = velocity;
    stroke.lastWidth = width;
    return widths;
}

/**
 * 向一笔喂入一个采样，并返回它补全的曲线（若有）。
 *
 * 一条三次曲线需要四个采样：曲线在中间两个之间行进，两端的控制点取自两侧的采样。
 * 前三个用首采样补齐，于是笔画从第三个采样就开始绘制而非第四个。
 *
 * @returns {object|null} 补全的曲线；缓冲区仍在填充时为 null。
 */
function feedStroke(stroke, pen, sample) {
    const buffer = stroke.buffer;
    buffer.push(sample);

    if (buffer.length <= 2) return null;
    if (buffer.length === 3) buffer.unshift(buffer[0]);

    const widths = curveWidths(stroke, pen, buffer[1], buffer[2]);
    const curve = {
        start: buffer[1],
        c1: controlPoints(buffer[0], buffer[1], buffer[2])[1],
        c2: controlPoints(buffer[1], buffer[2], buffer[3])[0],
        end: buffer[2],
        startWidth: widths.start,
        endWidth: widths.end
    };

    buffer.shift();
    return curve;
}

/** 重放一笔，把每条曲线交给访问者。 */
function replayStroke(stroke, pen, visit) {
    const replay = { buffer: [], lastVelocity: 0, lastWidth: 0 };
    beginStroke(replay, pen);
    for (const sample of stroke.points) {
        const curve = feedStroke(replay, pen, sample);
        if (curve) visit(curve);
    }
}

const isDot = stroke => stroke.points.length < 3;

// ---------------------------------------------------------------------------
// 画布渲染
// ---------------------------------------------------------------------------

/**
 * 用半径插值的、彼此重叠的圆点填满一条曲线。
 *
 * 变宽的线无法在一次调用里描出，所以曲线被逐段步行、每一步落一个圆。宽度沿 t 的三次方
 * 而非 t 变化，让宽度变化聚在线段末端——那正是下一段接手的部位。
 */
function drawCurve(ctx, curve, colour) {
    const widthDelta = curve.endWidth - curve.startWidth;
    const steps = Math.max(Math.ceil(curveLength(curve)) * CURVE_STEPS_PER_PIXEL, 1);

    ctx.beginPath();
    ctx.fillStyle = colour;
    for (let i = 0; i <= steps; i++) {
        const t = i / steps;
        const { x, y } = pointOnCurve(curve, t);
        const radius = curve.startWidth + t * t * t * widthDelta;
        ctx.moveTo(x + radius, y);
        ctx.arc(x, y, radius, 0, Math.PI * 2);
    }
    ctx.fill();
}

function drawDot(ctx, stroke, pen, colour) {
    const { x, y } = stroke.points[0];
    ctx.beginPath();
    ctx.fillStyle = colour;
    ctx.arc(x, y, (pen.minWidth + pen.maxWidth) / 2, 0, Math.PI * 2);
    ctx.fill();
}

function drawStroke(ctx, stroke, pen, colour) {
    if (isDot(stroke)) {
        drawDot(ctx, stroke, pen, colour);
        return;
    }
    replayStroke(stroke, pen, curve => drawCurve(ctx, curve, colour));
}

function redraw(state) {
    const { ctx, canvas } = state;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    const colour = penColour(state);
    for (const stroke of state.strokes) {
        drawStroke(ctx, stroke, pen(state), colour);
    }
}

// ---------------------------------------------------------------------------
// 由元素驱动的设定
// ---------------------------------------------------------------------------

function numberAttribute(element, name, fallback) {
    const raw = element.getAttribute(name);
    if (raw === null) return fallback;
    const value = Number(raw);
    return Number.isNaN(value) ? fallback : value;
}

const pen = state => ({
    minWidth: numberAttribute(state.canvas, 'data-min-width', 0.5),
    maxWidth: numberAttribute(state.canvas, 'data-max-width', 2.5),
    velocityWeight: numberAttribute(state.canvas, 'data-velocity-weight', 0.7)
});

/**
 * 墨迹颜色：显式给出的优先，否则取元素自身的 CSS `color`。
 *
 * 回落到 `color` 正是签名无需消费者接线即可跟随主题的原因，也与导出的 SVG 用
 * `currentColor` 顶替的是同一个值。
 */
function penColour(state) {
    const explicit = state.canvas.getAttribute('data-colour');
    return explicit || getComputedStyle(state.canvas).color || '#000';
}

/**
 * 按设备像素比配置后备存储的大小并重绘。
 *
 * 采样以 CSS 像素存储，所以缩放或在显示器之间移动只需重设上下文变换再重放它们。
 * 调整画布尺寸也会清空画布——这正是重绘不可省略的原因。
 */
function resize(state) {
    const { canvas } = state;
    const rect = canvas.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return;

    const ratio = window.devicePixelRatio || 1;
    canvas.width = Math.round(rect.width * ratio);
    canvas.height = Math.round(rect.height * ratio);
    state.ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
    state.cssWidth = rect.width;
    state.cssHeight = rect.height;
    redraw(state);
}

// ---------------------------------------------------------------------------
// 公开面
// ---------------------------------------------------------------------------

/**
 * 开始在一块画布上捕获签名。
 *
 * @param {HTMLCanvasElement} canvas - 绘制面。
 * @param {DotNetObject} dotNetRef - 对 SignaturePad 组件的引用。
 * @param {string} padId - 本画板的唯一标识。
 */
export function initialize(canvas, dotNetRef, padId) {
    if (!canvas || !dotNetRef) return;
    dispose(padId);

    const state = {
        canvas,
        ctx: canvas.getContext('2d'),
        dotNetRef,
        strokes: [],
        current: null,
        pointerId: null,
        cssWidth: 0,
        cssHeight: 0,
        disposed: false
    };

    const disabled = () => canvas.getAttribute('data-disabled') === 'true';
    const notify = () => {
        if (state.disposed) return;
        dotNetRef.invokeMethodAsync('JsSignatureChanged', state.strokes.length === 0)
            .catch(() => { }); // 断开的电路不得打断画板。
    };

    const sampleFrom = event => {
        const rect = canvas.getBoundingClientRect();
        return { x: event.clientX - rect.left, y: event.clientY - rect.top, t: event.timeStamp };
    };

    const down = event => {
        if (state.pointerId != null || disabled() || !event.isPrimary || event.button > 0) return;
        event.preventDefault();
        state.pointerId = event.pointerId;
        canvas.setPointerCapture(event.pointerId);

        state.current = { points: [] };
        beginStroke(state.current, pen(state));
        addSample(sampleFrom(event));
    };

    const addSample = sample => {
        const stroke = state.current;
        stroke.points.push(sample);
        const curve = feedStroke(stroke, pen(state), sample);
        if (curve) drawCurve(state.ctx, curve, penColour(state));
    };

    const move = event => {
        if (event.pointerId !== state.pointerId) return;
        event.preventDefault();
        // 手写笔或高回报率鼠标在一帧之间会报告多个位置。全部用上在这里不费什么，
        // 却是光滑曲线与多面曲线的分水岭。
        const events = typeof event.getCoalescedEvents === 'function'
            ? event.getCoalescedEvents()
            : [event];
        for (const point of events.length > 0 ? events : [event]) {
            addSample(sampleFrom(point));
        }
    };

    const up = event => {
        if (event.pointerId !== state.pointerId) return;
        if (canvas.hasPointerCapture(event.pointerId)) canvas.releasePointerCapture(event.pointerId);
        state.pointerId = null;

        const stroke = state.current;
        state.current = null;
        if (!stroke) return;

        // 轻点留下一个圆点。两个采样凑不出一条三次曲线，所以按圆点绘制。
        if (isDot(stroke)) drawDot(state.ctx, stroke, pen(state), penColour(state));
        state.strokes.push({ points: stroke.points });
        notify();
    };

    const listeners = {
        pointerdown: down,
        pointermove: move,
        pointerup: up,
        pointercancel: up,
        lostpointercapture: up
    };
    Object.entries(listeners).forEach(([event, handler]) => canvas.addEventListener(event, handler));

    const resizeObserver = new ResizeObserver(() => resize(state));
    resizeObserver.observe(canvas);

    // 墨色取自 CSS，所以主题切换必须把已画的内容重绘一遍。
    const themeObserver = new MutationObserver(() => redraw(state));
    themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ['class', 'data-theme'] });

    resize(state);

    padStates.set(padId, {
        state,
        dispose: () => {
            state.disposed = true;
            resizeObserver.disconnect();
            themeObserver.disconnect();
            if (state.pointerId != null && canvas.hasPointerCapture(state.pointerId)) {
                canvas.releasePointerCapture(state.pointerId);
            }
            Object.entries(listeners).forEach(([event, handler]) => canvas.removeEventListener(event, handler));
        }
    });
}

/** 擦除全部笔画。 */
export function clear(padId) {
    const entry = padStates.get(padId);
    if (!entry) return;
    entry.state.strokes = [];
    entry.state.current = null;
    redraw(entry.state);
}

/** 移除最近的一笔。确有笔画被移除时返回 true。 */
export function undo(padId) {
    const entry = padStates.get(padId);
    if (!entry || entry.state.strokes.length === 0) return false;
    entry.state.strokes.pop();
    redraw(entry.state);
    return true;
}

/** 是否尚未画下任何东西。 */
export function isEmpty(padId) {
    const entry = padStates.get(padId);
    return !entry || entry.state.strokes.length === 0;
}

/**
 * 签名的 SVG，未绘制任何东西时为 null。
 *
 * 每条曲线成为一条按自身宽度描画的 path，而不是画布画出的成千上万个圆点——文件因此
 * 保持小巧，锥形也得以保留。描边为 `currentColor`，所以同一份标记在纸面上呈深色、
 * 在深色页面上呈浅色。
 */
export function toSvg(padId) {
    const entry = padStates.get(padId);
    if (!entry || entry.state.strokes.length === 0) return null;

    const state = entry.state;
    const settings = pen(state);
    // 一位小数比任何合理缩放下的设备像素都细，而每一个数字每个坐标都要付一次——
    // 一份细节丰富的签名有成千上万个坐标。
    const round = value => Math.round(value * 10) / 10;
    const parts = [];

    for (const stroke of state.strokes) {
        if (isDot(stroke)) {
            const { x, y } = stroke.points[0];
            const r = round((settings.minWidth + settings.maxWidth) / 2);
            // 圆点是这里唯一的填充形状，所以要覆盖分组的 fill。
            parts.push(`<circle cx="${round(x)}" cy="${round(y)}" r="${r}" fill="currentColor"/>`);
            continue;
        }
        replayStroke(stroke, settings, curve => {
            const d = `M${round(curve.start.x)} ${round(curve.start.y)}`
                + `C${round(curve.c1.x)} ${round(curve.c1.y)} `
                + `${round(curve.c2.x)} ${round(curve.c2.y)} `
                + `${round(curve.end.x)} ${round(curve.end.y)}`;
            // 画布宽度是半径；SVG 的 stroke 按整宽计，所以取两倍均值。
            parts.push(`<path d="${d}" stroke-width="${round(curve.startWidth + curve.endWidth)}"/>`);
        });
    }

    const width = round(state.cssWidth);
    const height = round(state.cssHeight);
    // width 与 height 赋予它固有尺寸，这正是 <img> 标签与 PDF 需要的。内联样式则照顾
    // 另一种场合：直接落进页面时，1500px 画板画出的签名否则会撑破容器被裁掉，而不是
    // 收缩适配。
    //
    // 着墨属性被提到 <g> 上而不是在每条 path 上重复。它们在每条上完全一致，而一份
    // 细节丰富的签名有数千条 path，重复开价不止让文件翻倍。
    return `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" `
        + `viewBox="0 0 ${width} ${height}" style="max-width:100%;height:auto">`
        + `<g fill="none" stroke="currentColor" stroke-linecap="round">`
        + `${parts.join('')}</g></svg>`;
}

/**
 * 签名的 PNG data URL，未绘制任何东西时为 null。
 *
 * 笔画按指定颜色重绘，而不是从活动画布上拷贝。拷贝会把主题的墨色烙进去，而深色主题下
 * 画出的签名近乎白色：把它合成到 PDF 想要的白底上，结果就是一张白页。SVG 靠
 * `currentColor` 躲过了这一劫；PNG 没有地方把这个决定推迟出去，所以颜色总是显式的。
 *
 * @param {string|null} background - 垫在墨迹之后的 CSS 颜色，透明背景为 null。
 * @param {string} ink - 笔画的 CSS 颜色。
 */
function renderForExport(padId, background, ink) {
    const entry = padStates.get(padId);
    if (!entry || entry.state.strokes.length === 0) return null;
    const state = entry.state;

    const copy = document.createElement('canvas');
    copy.width = state.canvas.width;
    copy.height = state.canvas.height;

    const ctx = copy.getContext('2d');
    const ratio = state.canvas.width / (state.cssWidth || state.canvas.width);
    if (background) {
        ctx.fillStyle = background;
        ctx.fillRect(0, 0, copy.width, copy.height);
    }

    ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
    const settings = pen(state);
    const colour = ink || penColour(state);
    for (const stroke of state.strokes) {
        drawStroke(ctx, stroke, settings, colour);
    }

    return copy;
}

/** 原始采样，用于存储一份可回放、可事后核查的签名。 */
export function getStrokes(padId) {
    const entry = padStates.get(padId);
    if (!entry) return [];
    return entry.state.strokes.map(stroke => ({
        points: stroke.points.map(p => ({ x: p.x, y: p.y, t: p.t }))
    }));
}

/** 用先前捕获的采样替换当前绘制内容。 */
export function setStrokes(padId, strokes) {
    const entry = padStates.get(padId);
    if (!entry) return;
    entry.state.strokes = (strokes ?? [])
        .map(stroke => ({ points: (stroke.points ?? []).map(p => ({ x: p.x, y: p.y, t: p.t })) }))
        .filter(stroke => stroke.points.length > 0);
    redraw(entry.state);
}

// ---------------------------------------------------------------------------
// 流式导出
// ---------------------------------------------------------------------------

/**
 * 本模块送往 C# 的一切都以流的形式走，而不是作为返回值。
 *
 * Blazor Server 电路默认把单条入站 hub 消息封顶在 32KB，而一份细节丰富的签名径直撞穿它：
 * 400 个采样约等于 400 条曲线，SVG、PNG 乃至原始采样三者都会越过封顶。故障是静默且彻底的
 * ——电路在调用中途被撕毁，组件永远等不到答复，页面就这么停止响应。流由框架分块传输，
 * 不受该封顶约束。
 *
 * 每个函数把字节以普通类型化数组返回。调用侧索要 IJSStreamReference 时由 Blazor 亲自构建
 * 流；在这里用 createJSStreamReference 包一层会被套壳两次，并以
 * "Supplied value is not a typed array or blob." 失败。
 */

const utf8 = new TextEncoder();

/** SVG 标记的 UTF-8 字节，未绘制任何东西时为 null。 */
export function toSvgStream(padId) {
    const svg = toSvg(padId);
    return svg === null ? null : utf8.encode(svg);
}

/** 原始采样的 UTF-8 JSON 字节。 */
export function toStrokesStream(padId) {
    return utf8.encode(JSON.stringify(getStrokes(padId)));
}

/**
 * PNG 字节的流，未绘制任何东西时为 null。
 *
 * 经 toBlob 构建而非 toDataURL，字节在这一侧绝不以 base64 现身：编码白胖三分之一，
 * 而 C# 反正要把这层解码掉。
 */
export async function toPngStream(padId, background, ink) {
    const canvas = renderForExport(padId, background, ink);
    if (!canvas) return null;

    const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
    if (!blob) return null;
    return new Uint8Array(await blob.arrayBuffer());
}

/** 摘除某画板的全部监听器与观察器。 */
export function dispose(padId) {
    padStates.get(padId)?.dispose();
    padStates.delete(padId);
}
