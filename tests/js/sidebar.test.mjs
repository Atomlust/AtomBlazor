// sidebar.js 的真实文件导入与行为测试。
// 模块顶层零 DOM 触碰（仅常量与模块级实例登记簿），document/window/ResizeObserver
// 均在函数体内引用，可在无 DOM 的 Node 环境以 file-URL 导入；模块无相对依赖。
// C# 侧调用点为 barrel 点分标识符 sidebar.initialize / sidebar.cleanup /
// sidebar.setToggleShortcutEnabled / sidebar.getSidebarState / sidebar.saveSidebarState
// （SidebarProvider.razor.cs，经 ComponentModules.GetCoreAsync 装载 components-core.js）。
// 怪癖锁存照源面：initializeSidebar 与 initialize 双导出——initialize 是
// "一次调用同读已存 cookie 并登记实例"的合并面（省 Blazor Server 电路往返），
// initializeSidebar 保留为纯登记面，两枚都在导出面上，本测逐一核对。
// 持久化通道照源面为 document.cookie（简报"localStorage 往返"措辞与源不符，
// 以源为准落 cookie 往返断言，偏差在此登记）。
// DOM 桩沿用 menu-triggers/responsive-nav 先例最小口径：可编程 window.innerWidth、
// document.body 哨兵 + 监听计数 + cookie 罐（setter 解析 name=value 首对并入罐，
// getter 以 "; " 重排还原，照浏览器语义）与记录回调/断联的 ResizeObserver 桩。
// 不可测项登记：真实键盘事件与浏览器 cookie 过期语义在纯桩环境下只核对调用面
// （手动重放观察器/监听回调），不伪造浏览器级断言；模块级 instances/nextInstanceId/
// keyboardHandler/resizeObserver 为共享状态，各测试按序 initialize→cleanup 复位。

import assert from 'node:assert/strict';
import { test } from 'node:test';

const moduleUrl = new URL(
    '../../src/AtomBlazor.Components/wwwroot/js/sidebar.js', import.meta.url);

// 环境桩：document（监听表 + body 哨兵 + cookie 罐）。
const bodies = [];
function makeDocument() {
    const handlers = new Map();
    const jar = new Map();
    return {
        handlers,
        body: { tag: 'body' },
        get cookie() {
            return [...jar.entries()].map(([k, v]) => `${k}=${v}`).join('; ');
        },
        set cookie(text) {
            // 浏览器语义：赋值串的首对 name=value 入罐（expires/path/SameSite 属性段忽略）。
            const [pair] = text.split(';');
            const eq = pair.indexOf('=');
            const name = pair.slice(0, eq).trim();
            const value = pair.slice(eq + 1).trim();
            if (value === '') jar.delete(name); else jar.set(name, value);
        },
        clearCookies() { jar.clear(); },
        addEventListener(type, fn) {
            if (!handlers.has(type)) handlers.set(type, []);
            handlers.get(type).push(fn);
        },
        removeEventListener(type, fn) {
            const list = handlers.get(type) ?? [];
            const index = list.indexOf(fn);
            if (index !== -1) list.splice(index, 1);
        },
        dispatch(type, event = {}) {
            for (const fn of [...(handlers.get(type) ?? [])]) fn(event);
        },
        count(type) { return (handlers.get(type) ?? []).length; },
    };
}

globalThis.document = makeDocument();
globalThis.window = { innerWidth: 1024 };

// ResizeObserver 桩：记录回调、观察目标与断联标记。
const observers = [];
globalThis.ResizeObserver = class {
    constructor(cb) {
        this.cb = cb;
        this.targets = [];
        this.disconnected = false;
        observers.push(this);
    }
    observe(target) { this.targets.push(target); }
    disconnect() { this.disconnected = true; }
    fire() { this.cb(); }
};

// DotNet 引用桩：记录 invokeMethodAsync 的 (方法名, 载荷序列)。
function makeRef() {
    const calls = [];
    return {
        calls,
        invokeMethodAsync(name, ...args) {
            calls.push([name, ...args]);
            return Promise.resolve();
        },
    };
}

// Ctrl/Cmd+B 事件桩。
function ctrlB(override = {}) {
    const event = {
        key: 'b', ctrlKey: true, metaKey: false,
        preventDefaultCalls: 0,
        preventDefault() { this.preventDefaultCalls += 1; },
        ...override,
    };
    document.dispatch('keydown', event);
    return event;
}

const sb = await import(moduleUrl.href);

// ------------------------------------------------------------------ 导出面

test('导出面：initializeSidebar 与 initialize 双导出照源面，另有四枚工具函数', () => {
    assert.equal(typeof sb.initializeSidebar, 'function');
    assert.equal(typeof sb.initialize, 'function'); // 合并面（读 cookie + 登记）双导出
    assert.equal(typeof sb.cleanup, 'function');
    assert.equal(typeof sb.setToggleShortcutEnabled, 'function');
    assert.equal(typeof sb.getSidebarState, 'function');
    assert.equal(typeof sb.saveSidebarState, 'function');
});

// ------------------------------------------------------------------ initialize/cleanup 挂摘

test('首次 initializeSidebar 挂共享监听并观察 body，随即推送当前移动端状态', () => {
    const ref = makeRef();
    globalThis.window.innerWidth = 1024;
    const id = sb.initializeSidebar(ref, true);
    assert.equal(typeof id, 'number');
    assert.equal(document.count('keydown'), 1, 'keydown 应挂上');
    assert.equal(observers.length, 1, 'ResizeObserver 应新建');
    assert.deepEqual(observers[0].targets, [document.body], '观察对象应为 body');
    assert.deepEqual(ref.calls, [['OnMobileChange', false]], '桌面宽度推送 false');

    const event = ctrlB();
    assert.equal(event.preventDefaultCalls, 1);
    assert.deepEqual(ref.calls.at(-1), ['OnToggleShortcut']);
    sb.cleanup(id);
});

test('实例 id 递增不复用；未知 id cleanup 与空表 cleanup 皆安全空操作', () => {
    const a = sb.initializeSidebar(makeRef(), true);
    const b = sb.initializeSidebar(makeRef(), true);
    assert.equal(b, a + 1, 'nextInstanceId 递增');
    sb.cleanup(a);
    sb.cleanup(a); // 已摘除再 cleanup：静默
    sb.cleanup(999999); // 从未存在：静默
    sb.cleanup(b);
    assert.equal(document.count('keydown'), 0, '最后实例离开后 keydown 应摘净');
    assert.equal(observers.at(-1).disconnected, true, '最后实例离开后观察器应断联');
});

test('再注册一轮重新挂共享监听（cleanup 摘净后可复用挂摘面）', () => {
    const ref = makeRef();
    const before = observers.length;
    const id = sb.initializeSidebar(ref, true);
    assert.equal(document.count('keydown'), 1, 'keyboardHandler 置 null 后应重挂');
    assert.equal(observers.length, before + 1, 'resizeObserver 置 null 后应新建下一枚');
    sb.cleanup(id);
    assert.equal(document.count('keydown'), 0);
});

test('多实例共享单套监听：keydown 只挂一枚且分发覆盖全部实例', () => {
    const refA = makeRef();
    const refB = makeRef();
    const idA = sb.initializeSidebar(refA, true);
    const idB = sb.initializeSidebar(refB, true);
    assert.equal(document.count('keydown'), 1, '第二实例不得再挂一套');
    const event = ctrlB();
    assert.equal(event.preventDefaultCalls, 1);
    assert.deepEqual(refA.calls.at(-1), ['OnToggleShortcut']);
    assert.deepEqual(refB.calls.at(-1), ['OnToggleShortcut']);
    // cleanup 单枚不清共享面
    sb.cleanup(idA);
    assert.equal(document.count('keydown'), 1, '还剩一实例，监听不应摘');
    assert.equal(observers.at(-1).disconnected, false);
    sb.cleanup(idB);
});

test('窗口重放观察器回调向各实例重播移动端状态', () => {
    const ref = makeRef();
    const id = sb.initializeSidebar(ref, true);
    globalThis.window.innerWidth = 500;
    observers.at(-1).fire();
    assert.deepEqual(ref.calls.at(-1), ['OnMobileChange', true], '窄视口应重播 true');
    globalThis.window.innerWidth = 1024;
    sb.cleanup(id);
});

// ------------------------------------------------------------------ 快捷键开关沿

test('非 Ctrl/Cmd 修饰的 b 与其它键一律沉默不吞键', () => {
    const ref = makeRef();
    const id = sb.initializeSidebar(ref, true);
    const plain = ctrlB({ ctrlKey: false });
    assert.equal(plain.preventDefaultCalls, 0);
    const otherKey = { key: 'x', ctrlKey: true, preventDefaultCalls: 0, preventDefault() { this.preventDefaultCalls += 1; } };
    document.dispatch('keydown', otherKey);
    assert.equal(otherKey.preventDefaultCalls, 0);
    assert.equal(ref.calls.filter(c => c[0] === 'OnToggleShortcut').length, 0, '两键均不应触发切换');
    const meta = ctrlB({ ctrlKey: false, metaKey: true });
    assert.equal(meta.preventDefaultCalls, 1, 'Cmd+B 应与 Ctrl+B 同沿');
    sb.cleanup(id);
});

test('enableToggleShortcut=false 实例不收 OnToggleShortcut；全关时不吞原生键', () => {
    const offRef = makeRef();
    const offId = sb.initializeSidebar(offRef, false);
    const event = ctrlB();
    assert.equal(offRef.calls.filter(c => c[0] === 'OnToggleShortcut').length, 0);
    assert.equal(event.preventDefaultCalls, 0, '无一响应应放行原生 Ctrl+B');
    const onId = sb.initializeSidebar(makeRef(), true);
    assert.equal(ctrlB().preventDefaultCalls, 1, '混入一枚开启后应恢复吞键');
    sb.cleanup(offId);
    sb.cleanup(onId);
});

test('setToggleShortcutEnabled 初始化后双向换开关', () => {
    const ref = makeRef();
    const id = sb.initializeSidebar(ref, true);
    sb.setToggleShortcutEnabled(id, false);
    assert.equal(ctrlB().preventDefaultCalls, 0, '关后应沉默不吞');
    sb.setToggleShortcutEnabled(id, true);
    assert.equal(ctrlB().preventDefaultCalls, 1, '开后再收');
    sb.setToggleShortcutEnabled(999999, true); // 未知 id：静默不抛
    sb.cleanup(id);
});

// ------------------------------------------------------------------ cookie 持久化往返（照源面）

test('saveSidebarState/getSidebarState cookie 往返 true/false；无值 null', () => {
    document.clearCookies();
    sb.saveSidebarState('sidebar-open', true);
    assert.equal(sb.getSidebarState('sidebar-open'), true);
    sb.saveSidebarState('sidebar-open', false);
    assert.equal(sb.getSidebarState('sidebar-open'), false);
    assert.equal(sb.getSidebarState('never-saved'), null, '未落键应回 null');
    assert.equal(sb.getSidebarState('sidebar-open'), false, '他键缺失不串罐');
    document.clearCookies();
});

test('initialize 合并面：null 键不读 cookie 直返 savedOpen null；有键恢复已存态且登记实例', () => {
    document.clearCookies();
    globalThis.window.innerWidth = 1024;
    const refA = makeRef();
    const first = sb.initialize(refA, true, null);
    assert.equal(typeof first.instanceId, 'number');
    assert.equal(first.savedOpen, null, 'cookieKey null 即不读');

    sb.saveSidebarState('sb-key', false);
    const refB = makeRef();
    const second = sb.initialize(refB, true, 'sb-key');
    assert.equal(second.instanceId, first.instanceId + 1, 'initialize 内部同走 initializeSidebar 登记面');
    assert.equal(second.savedOpen, false, 'cookie "false" 恢复布尔 false');
    assert.deepEqual(refB.calls, [['OnMobileChange', false]], 'initialize 后仍推移动端状态');
    sb.cleanup(first.instanceId);
    sb.cleanup(second.instanceId);
    document.clearCookies();
});

// ------------------------------------------------------------------ 健壮性

test('invoke 异常吞掉：坏引用不拉崩共享分发环，好实例照常收键', () => {
    const broken = { invokeMethodAsync() { throw new Error('电路已断'); } };
    const good = makeRef();
    const badId = sb.initializeSidebar(broken, true); // notifyMobile 走 invoke 吞抛，不应崩
    const goodId = sb.initializeSidebar(good, true);
    const event = ctrlB();
    assert.equal(event.preventDefaultCalls, 1);
    assert.deepEqual(good.calls.at(-1), ['OnToggleShortcut']);
    sb.cleanup(badId);
    sb.cleanup(goodId);
});

// ------------------------------------------------------------------ components-core 桶接线

test('components-core.js barrel 已收口 sidebar 与 sidebarInset 两命名空间', async () => {
    const { readFile } = await import('node:fs/promises');
    const core = new URL('../../src/AtomBlazor.Components/wwwroot/js/components-core.js', import.meta.url);
    const text = await readFile(core, 'utf8');
    assert.match(text, /^import \* as sidebar from '\.\/sidebar\.js\?assets=1';$/m);
    assert.match(text, /^import \* as sidebarInset from '\.\/sidebar-inset\.js\?assets=1';$/m);
    assert.match(text, /^assertFresh\('sidebar\.js', sidebar, 'initialize'\);$/m);
    assert.match(text, /^assertFresh\('sidebar-inset\.js', sidebarInset, 'scrollToTop'\);$/m);
    assert.match(text, /^export \{[\s\S]*\bsidebar,[\s\S]*\bsidebarInset,[\s\S]*\};$/m);
});
