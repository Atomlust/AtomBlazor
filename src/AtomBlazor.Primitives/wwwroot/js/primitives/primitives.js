/**
 * AtomBlazor 全部基元 JavaScript 模块的唯一入口。
 *
 * 为什么存在这个文件
 * ------------------
 * 在 Blazor Server 上，从 C# 发出的 `import(...)` 是一条发往浏览器、随后由服务端等待的指令，
 * 所以每个懒加载模块都要付出一次完整的电路往返——按模块、按页面加载各计一次，
 * 无论文件是否已在 HTTP 缓存里。打开一个浮层过去会串起三次这样的往返
 * （positioning、Floating UI、click-outside），开销随用户延迟增长，而与文件大小无关。
 *
 * 改为导入这个 barrel，整个基元面一次往返即可解析：浏览器自行并行遍历静态导入图，
 * 不再重复询问服务器。
 *
 * 每个模块都以自己的命名空间再导出，因为其中好几个导出同名函数（`initialize`、`dispose`、
 * `focusElement`）。从 C# 调用时使用点分标识符：
 *
 *     await module.InvokeVoidAsync("clickOutside.onClickOutsideByIds", ...);
 *
 * 各独立文件仍可单独导入，内容不变；模块每个文档只求值一次，无论先经哪条路径到达，
 * 所以混用两种方式也是安全的。
 *
 * 命名空间永远是文件名（去连字符）的 camelCase 形式。请保持这条规则——它正是调用点写法
 * 可以预测的前提。
 *
 * 当前打包 （Portal/Direction）、（Positioning/Floating）、（焦点捕获/
 * 原生 dialog/全局快捷键/滑动区域）、（slider）、（tree-keyboard）、
 * （signature-pad）、（keyboard-nav，Tabs/ListBox 键盘导航）、
 * （match-trigger-width，DropdownMenu 触发器等宽）与
 * （scroll-area，ScrollArea 自定义滚动条）与 （table-row-nav，Table 行键盘导航）
 * 已交付的模块。
 * 提前整体迁入了 overlay.js 的静态依赖图（click-outside、escape-keydown、
 * menu-keyboard、select、element-utils），避免同一模块随 二次搬运。
 * 后续的模块清单与到货批次：
 * （2026-10-14）已交付 sortable.js（及其配套 sortable-keyboard/overlay/transfer.js
 *           与 vendored lib/sortable/sortable.min.js）。下方已补齐导入、新鲜性校验与导出，
 * 遗留 sortable.js TODO。
 */

import * as clickOutside from './click-outside.js';
import * as elementUtils from './element-utils.js';
import * as escapeKeydown from './escape-keydown.js';
import * as focusTrap from './focus-trap.js';
import * as keyboardNav from './keyboard-nav.js';
import * as keyboardShortcuts from './keyboard-shortcuts.js';
import * as matchTriggerWidth from './match-trigger-width.js';
import * as menuKeyboard from './menu-keyboard.js';
import * as nativeDialog from './native-dialog.js';
import * as overlay from './overlay.js';
import * as portal from './portal.js';
import * as positioning from './positioning.js';
import * as scrollArea from './scroll-area.js';
import * as select from './select.js';
import * as slider from './slider.js';
import * as signaturePad from './signature-pad.js?assets=1';
import * as swipeArea from './swipe-area.js';
import * as tableRowNav from './table-row-nav.js';
import * as treeKeyboard from './tree-keyboard.js';
import * as sortable from './sortable.js';

/**
 * 当 bundle 内某个模块比 bundle 本身还旧时，大声失败。
 *
 * C# 导入本文件时带库版本作查询串，所以新版本就是新 URL，入口文件不可能被陈旧缓存应答。
 * 而它导入的文件按相对路径取回、不带查询串，浏览器或 CDN 若仍握着旧副本，会毫无异议地
 * 交出来。曾有用户踩中：CDN 浏览器缓存 TTL 很长，新 bundle 导入了几小时前的旧模块，
 * 某个 `x.initialize` 不是函数，第一个调用点就把整条电路打死——且不给任何原因。
 *
 * 每个模块都按当前 bundle 依赖的某个导出做检查。过期文件在这里、在加载时就失败，
 * 点名文件本身，而不是晚些时候、在别处抛一条关于某函数的错误。
 */
function assertFresh(fileName, module, exportName) {
    if (typeof module[exportName] !== 'function') {
        throw new Error(
            `Atom: ${fileName} 已过期——它没有 '${exportName}'，而当前版本的 ` +
            `primitives.js 需要它。浏览器或其前面的缓存交出了旧副本。` +
            `请强制刷新验证，并确保 _content/AtomBlazor.*/js/ 的缓存时长不超过一次部署周期。`);
    }
}

assertFresh('click-outside.js', clickOutside, 'onClickOutsideByIds');
assertFresh('element-utils.js', elementUtils, 'observeNearBottom');
assertFresh('escape-keydown.js', escapeKeydown, 'initialize');
assertFresh('focus-trap.js', focusTrap, 'createFocusTrap');
assertFresh('keyboard-nav.js', keyboardNav, 'setupKeyboardNav');
assertFresh('keyboard-shortcuts.js', keyboardShortcuts, 'registerShortcut');
assertFresh('match-trigger-width.js', matchTriggerWidth, 'matchTriggerWidth');
assertFresh('menu-keyboard.js', menuKeyboard, 'initialize');
assertFresh('native-dialog.js', nativeDialog, 'setupDialog');
assertFresh('overlay.js', overlay, 'open');
assertFresh('portal.js', portal, 'setupPortal');
assertFresh('positioning.js', positioning, 'hidePosition');
assertFresh('scroll-area.js', scrollArea, 'initialize');
assertFresh('select.js', select, 'scrollMarkedIntoView');
assertFresh('slider.js', slider, 'initialize');
assertFresh('signature-pad.js', signaturePad, 'initialize');
assertFresh('swipe-area.js', swipeArea, 'initialize');
assertFresh('table-row-nav.js', tableRowNav, 'delegateRowBehaviour');
assertFresh('tree-keyboard.js', treeKeyboard, 'initialize');
assertFresh('sortable.js', sortable, 'init');

export {
    clickOutside,
    elementUtils,
    escapeKeydown,
    focusTrap,
    keyboardNav,
    keyboardShortcuts,
    matchTriggerWidth,
    menuKeyboard,
    nativeDialog,
    overlay,
    portal,
    positioning,
    scrollArea,
    select,
    slider,
    signaturePad,
    swipeArea,
    tableRowNav,
    treeKeyboard,
    sortable
};
