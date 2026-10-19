// Node 解析钩子：把 Components 层 range-slider.js 的跨包 drag-updates 说明符
// 映射到仓库真实路径。
//
// 该说明符 '../../AtomBlazor.Primitives/js/drag-updates.js?assets=1' 按发布后的
// _content/ 服务布局写成（与 .Primitives 的同构写法一致）：
// 运行时相对 _content/AtomBlazor.Components/js/ 解析为
// _content/AtomBlazor.Primitives/js/drag-updates.js，在浏览器里成立；
// 但在仓库布局下该相对路径不存在，直接 file-URL 导入 range-slider.js（或把它
// 静态依赖进来的 components-core.js barrel）会以 ERR_MODULE_NOT_FOUND 失败。
// 经 node:module register 挂上本钩子后，仓库布局与 _content 布局两不相碍。
// tests/js/range-slider-module.test.mjs 用的是"data: URI 再导入前改写说明符"的
// 同类手段，这里是 barrel 整图导入（无法逐文件改写）所需的进程级版本。

export async function initialize() {}

export function resolve(specifier, context, nextResolve) {
    if (specifier === '../../AtomBlazor.Primitives/js/drag-updates.js?assets=1'
        && context.parentURL?.includes('/wwwroot/js/')) {
        // 从 Components 的 wwwroot/js/ 上溯三级到 src/，再进 Primitives 包。
        const mapped = new URL(
            '../../../AtomBlazor.Primitives/wwwroot/js/drag-updates.js', context.parentURL);
        return nextResolve(mapped.href, context);
    }
    return nextResolve(specifier, context);
}
