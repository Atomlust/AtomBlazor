/**
 * 几乎每个页面都会加载的 JavaScript 模块，集中在一个入口里。
 *
 * 为什么只打包这些
 * ----------------
 * 从 C# 发出的每个 `import(...)` 在 Blazor Server 上都是一次电路往返。`JsModules` 已把它
 * 从"每个组件实例一次"降到"每个模块一次"；本文件再把常见情形降到"每个页面一次"。
 *
 * 实测（冷缓存、八个示例页面）：`theme.js` 在每个页面上都会加载；带表单控件的页面必然
 * 加载 `text-input.js` 与 `composition-guard.js`；使用侧边栏的页面加载 `sidebar.js` 与
 * `sidebar-inset.js`。也就是说任何人点击之前就要付出三到五次往返。
 *
 * Components 层其余模块刻意保持懒加载。全部打包是 56 KB（gzip 后），一个只渲染一个输入框的
 * 应用会为此下载数据网格、dock、markdown 编辑器与 ECharts 适配层。这些文件只占几 KB，
 * 而任何渲染过页面的应用本就已为它们付过费。
 *
 * 命名空间永远是文件名（去连字符）的 camelCase 形式，与 primitives bundle 一致，
 * 于是调用点写作 `theme.initialize`，无需有人打开这个文件。
 *
 * 当前打包 （theme）、（textInput/compositionGuard）、（numericInput）、
 * （maskedInput/inputOtp）、（rangeSlider）、（tagInput）、
 * （cascader）、（multiselect）、（colorPicker）、（fileUpload）、
 * （segmentedInput）、（virtualizationScroll）、（clipboard/exitPrompt）、
 * （responsiveTabs/tabsReorder）、（stepper）、（scrollToTop）、
 * （navigationMenu）、（responsiveNav）、（sidebar/sidebarInset）、
 * （resizable）与 （dock）已交付的模块。
 */

// 这些依赖 URL 的版本查询串要与 ComponentModules.CoreUrl 保持同拍。
// 入口模块上的查询串不会被浏览器缓存继承给相对导入。
import * as cascader from './cascader.js?assets=1';
import * as clipboard from './clipboard.js?assets=1';
import * as colorPicker from './color-picker.js?assets=1';
import * as compositionGuard from './composition-guard.js?assets=1';
import * as dock from './dock.js?assets=1';
import * as exitPrompt from './exit-prompt.js?assets=1';
import * as fileUpload from './file-upload.js?assets=1';
import * as inputOtp from './input-otp.js?assets=1';
import * as maskedInput from './masked-input.js?assets=1';
import * as multiselect from './multiselect.js?assets=1';
import * as navigationMenu from './navigation-menu.js?assets=1';
import * as numericInput from './numeric-input.js?assets=1';
import * as rangeSlider from './range-slider.js?assets=1';
import * as resizable from './resizable.js?assets=1';
import * as responsiveNav from './responsive-nav.js?assets=1';
import * as responsiveTabs from './responsive-tabs.js?assets=1';
import * as scrollToTop from './scroll-to-top.js?assets=1';
import * as segmentedInput from './segmented-input.js?assets=1';
import * as sidebar from './sidebar.js?assets=1';
import * as sidebarInset from './sidebar-inset.js?assets=1';
import * as stepper from './stepper.js?assets=1';
import * as tagInput from './tag-input.js?assets=1';
import * as tabsReorder from './tabs-reorder.js?assets=1';
import * as textInput from './text-input.js?assets=1';
import * as theme from './theme.js?assets=1';
import * as virtualizationScroll from './virtualization-scroll.js?assets=1';

// 当 bundle 内某个模块比 bundle 本身还旧时，大声失败。
// 参见 primitives.js 注释里那次事故：CDN 后面躺着过期的模块，而每条电路都死了。
function assertFresh(fileName, module, exportName) {
    if (typeof module[exportName] !== 'function') {
        throw new Error(
            `Atom: ${fileName} 已过期——它没有 '${exportName}'，而当前版本的 ` +
            `components-core.js 需要它。浏览器或其前面的缓存交出了旧副本。` +
            `请强制刷新验证，并确保 _content/AtomBlazor.*/js/ 的缓存时长不超过一次部署周期。`);
    }
}

assertFresh('cascader.js', cascader, 'initialize');
assertFresh('clipboard.js', clipboard, 'copyToClipboard');
assertFresh('color-picker.js', colorPicker, 'initializeArea');
assertFresh('composition-guard.js', compositionGuard, 'attach');
assertFresh('dock.js', dock, 'initializeDock');
assertFresh('exit-prompt.js', exitPrompt, 'arm');
assertFresh('file-upload.js', fileUpload, 'initializeDropZone');
assertFresh('input-otp.js', inputOtp, 'initialize');
assertFresh('masked-input.js', maskedInput, 'setInputValue');
assertFresh('multiselect.js', multiselect, 'setupMultiSelectInput');
assertFresh('navigation-menu.js', navigationMenu, 'navigateNext');
assertFresh('numeric-input.js', numericInput, 'initialize');
assertFresh('range-slider.js', rangeSlider, 'initializeRangeSlider');
assertFresh('resizable.js', resizable, 'initializeResizable');
assertFresh('responsive-nav.js', responsiveNav, 'initialize');
assertFresh('responsive-tabs.js', responsiveTabs, 'initialize');
assertFresh('scroll-to-top.js', scrollToTop, 'observe');
assertFresh('segmented-input.js', segmentedInput, 'initialize');
assertFresh('sidebar.js', sidebar, 'initialize');
assertFresh('sidebar-inset.js', sidebarInset, 'scrollToTop');
assertFresh('stepper.js', stepper, 'initialize');
assertFresh('tag-input.js', tagInput, 'initialize');
assertFresh('tabs-reorder.js', tabsReorder, 'initialize');
assertFresh('text-input.js', textInput, 'initialize');
assertFresh('theme.js', theme, 'initialize');
assertFresh('virtualization-scroll.js', virtualizationScroll, 'scrollToVirtualizedIndex');

export {
    cascader,
    clipboard,
    colorPicker,
    compositionGuard,
    dock,
    exitPrompt,
    fileUpload,
    inputOtp,
    maskedInput,
    multiselect,
    navigationMenu,
    numericInput,
    rangeSlider,
    resizable,
    responsiveNav,
    responsiveTabs,
    scrollToTop,
    segmentedInput,
    sidebar,
    sidebarInset,
    stepper,
    tagInput,
    tabsReorder,
    textInput,
    theme,
    virtualizationScroll
};
