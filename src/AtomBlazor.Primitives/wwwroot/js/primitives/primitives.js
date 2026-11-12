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
 * 各独立文件仍可单独导入，内容不变。但**同一个 URL 才是同一个模块实例**：
 * `./signature-pad.js` 与 `./signature-pad.js?assets=1` 会被浏览器当成两个模块分别求值，
 * 各自持有独立的实例 Map。本 barrel 对 signature-pad 用了 `?assets=1`（它需要随包分发的
 * 静态资源），所以任何绕过 barrel 直接导入它的调用点必须带上完全相同的查询串，
 * 否则两边操作的是不同的状态。其余模块一律裸路径导入，请保持不变。
 *
 * 命名空间永远是文件名（去连字符）的 camelCase 形式。请保持这条规则——它正是调用点写法
 * 可以预测的前提。
 *
 * 当前打包 20 个模块：click-outside、element-utils、escape-keydown、focus-trap、
 * keyboard-nav、keyboard-shortcuts、match-trigger-width、menu-keyboard、native-dialog、
 * overlay、portal、positioning、scroll-area、select、slider、signature-pad、swipe-area、
 * table-row-nav、tree-keyboard、sortable。
 * overlay.js 自身又把 click-outside、escape-keydown、menu-keyboard、select、element-utils
 * 纳入静态依赖图，因此它们不会随浮层二次搬运。
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
