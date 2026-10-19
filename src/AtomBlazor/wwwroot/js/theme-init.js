/**
 * 在首次绘制之前把已保存的主题应用到 <html>。
 *
 * 没有它，页面会先以默认主题渲染，保存的主题要等 Blazor 启动后才生效——所以选过暗色模式
 * 的用户每次加载都会先看到一闪的亮色。这就是 #477，且无法在 C# 侧修掉：在 prerender 或
 * 静态渲染的页面上，服务器无从知道存在 localStorage 里的偏好。
 *
 * 必须以经典、阻塞式脚本加载在 <head> 中：
 *
 *   <script src="_content/AtomBlazor/js/theme-init.js"></script>
 *
 * 不是 type="module"——模块会被推迟到文档解析完毕之后，也就是首次绘制之后，而这正是问题本身。
 * 也不是内联脚本——这样严格的 Content-Security-Policy 就不需要 'unsafe-inline'。
 *
 * 用同一个 script 标签上的 data 属性配置它：
 *
 *   data-default-dark="true"   没有任何已保存主题时使用暗色，而不是跟随系统偏好
 *   data-default-dark="false"  没有任何已保存主题时使用亮色
 *   data-storage="false"       完全不读 localStorage；须与
 *                              PersistToLocalStorage = false 配对使用，
 *                              防止早先会话留下的过期主题死灰复燃（#481）
 *
 * 这里刻意重复了 theme.js 中触碰 <html> 的那一小部分。导入虽能共享代码，却会把 defer
 * 重新引进来，而复制四次 DOM 写入是更便宜的取舍。若此处的属性名与 theme.js 哪天出现分岔，
 * 主题就会重新闪动。
 */
(function () {
    'use strict';

    var STORAGE_KEY = 'atom-theme';

    // document.currentScript 正是当前执行的标签——data-* 配置就这样读取，无需内联脚本。
    var script = document.currentScript;
    var useStorage = !script || script.getAttribute('data-storage') !== 'false';
    var defaultDark = script ? script.getAttribute('data-default-dark') : null;

    var saved = null;
    if (useStorage) {
        try {
            var raw = localStorage.getItem(STORAGE_KEY);
            if (raw) {
                saved = JSON.parse(raw);
            }
        } catch (e) {
            // localStorage 被禁用，或存值不是 JSON。回落到默认值；
            // 错一帧的主题好过一条打断文档解析的异常。
            saved = null;
        }
    }

    var root = document.documentElement;

    var isDark;
    if (saved && typeof saved.isDarkMode === 'boolean') {
        isDark = saved.isDarkMode;
    } else if (defaultDark === 'true') {
        isDark = true;
    } else if (defaultDark === 'false') {
        isDark = false;
    } else {
        try {
            isDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
        } catch (e) {
            isDark = false;
        }
    }

    if (isDark) {
        root.classList.add('dark');
    } else {
        root.classList.remove('dark');
    }

    if (saved) {
        if (saved.baseColor) {
            root.setAttribute('data-base-color', saved.baseColor);
        }

        if (saved.primaryColor) {
            if (saved.primaryColor === 'default') {
                root.removeAttribute('data-primary-color');
            } else {
                root.setAttribute('data-primary-color', saved.primaryColor);
            }
        }

        if (Number.isFinite(saved.radius) && saved.radius >= 0 && saved.radius <= 4) {
            root.style.setProperty('--radius', saved.radius + 'rem');
        }
        var designOptions = {
            density: ['standard', 'compact', 'dense', 'spacious'],
            font: ['system', 'inter', 'geist', 'roboto', 'nunitosans', 'serif', 'mono'],
            surface: ['standard', 'flat', 'elevated', 'glass'],
            menuColor: ['default', 'muted', 'primary', 'inverse'],
            menuAccent: ['subtle', 'primary']
        };
        Object.keys(designOptions).forEach(function (key) {
            var value = saved.design && saved.design[key];
            if (typeof value !== 'string') return;
            value = value.toLowerCase();
            if (designOptions[key].indexOf(value) < 0) return;
            var attribute = 'data-atom-' + key.replace(/[A-Z]/g, function (letter) { return '-' + letter.toLowerCase(); });
            root.setAttribute(attribute, value);
        });
    }
})();
