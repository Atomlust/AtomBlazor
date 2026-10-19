// MultiSelect 键盘导航处理器
// 用 JavaScript 直接操作 DOM 完成导航，换取即时响应
// 与 combobox 的关键差异：
// - Space 切换复选框但不关闭下拉
// - Enter 切换复选框并关闭下拉
// - 条目上的选中状态保持不变
//
// 导航键与搜索框的 @bind 在输入法组合期间都会被按住。原因见 composition-guard.js。

import { createCompositionGuard } from './composition-guard.js?assets=1';

let multiSelectStates = new Map();

/**
 * 为一个多选输入框装配键盘事件拦截
 * @param {HTMLElement} inputElement - 要挂载处理器的输入元素
 * @param {DotNetObject} dotNetRef - 指向 Blazor 组件的引用
 * @param {string} inputId - 输入元素的唯一标识
 * @param {string} contentId - 列表内容元素的 ID
 */
export function setupMultiSelectInput(inputElement, dotNetRef, inputId, contentId) {
    if (!inputElement || !(inputElement instanceof HTMLElement) || !dotNetRef) {
        console.error('setupMultiSelectInput: missing required parameters or inputElement is not a DOM element');
        return;
    }

    // 本多选实例的状态
    const state = {
        inputElement,
        dotNetRef,
        contentId,
        focusedIndex: -1
    };

    // 取可见且可用的 option 元素
    const getVisibleOptions = () => {
        const content = document.getElementById(contentId);
        if (!content) return [];

        // 取全部可见且未被禁用的 option 元素
        return Array.from(content.querySelectorAll('[role="option"]')).filter(el => {
            // 元素必须可见且未禁用
            return el.offsetParent !== null && el.getAttribute('aria-disabled') !== 'true';
        });
    };

    // 在 DOM 中更新焦点态（与 combobox 不同，这里保留选中状态）
    const updateFocusedItem = (newIndex) => {
        const options = getVisibleOptions();

        // 从全部条目上摘掉焦点态（但保留选中状态）
        options.forEach(el => {
            el.setAttribute('data-focused', 'false');
            // 不改 aria-selected——在多选里它反映的是复选框状态
        });

        // 把焦点态加到新条目上
        if (newIndex >= 0 && newIndex < options.length) {
            const focusedEl = options[newIndex];
            focusedEl.setAttribute('data-focused', 'true');

            // 需要时滚动到可视区
            focusedEl.scrollIntoView({ block: 'nearest', behavior: 'smooth' });

            state.focusedIndex = newIndex;
        } else {
            state.focusedIndex = -1;
        }
    };

    // 键盘导航处理
    const keyHandler = (e) => {
        // 输入法组合期间，下面每一个键都归它管：Space 是日文转换键、Enter 负责上屏、
        // 方向键在候选列表里行走、Escape 取消组合。本处理器跑在捕获阶段且无条件
        // preventDefault，没有这道守卫就会直接封死输入法，而不只是与它同时触发。
        if (guard.isComposing || e.isComposing === true || e.keyCode === 229) {
            return;
        }

        const options = getVisibleOptions();

        // 仅在存在可见条目时才处理按键
        if (options.length === 0 && !['Enter', 'Escape', ' '].includes(e.key)) {
            return;
        }

        switch (e.key) {
            case 'ArrowDown':
                e.preventDefault();
                {
                    const newIndex = state.focusedIndex < options.length - 1
                        ? state.focusedIndex + 1
                        : 0; // 回绕到首项
                    updateFocusedItem(newIndex);
                }
                break;

            case 'ArrowUp':
                e.preventDefault();
                {
                    const newIndex = state.focusedIndex > 0
                        ? state.focusedIndex - 1
                        : options.length - 1; // 回绕到末项
                    updateFocusedItem(newIndex);
                }
                break;

            case 'Home':
                e.preventDefault();
                updateFocusedItem(0);
                break;

            case 'End':
                e.preventDefault();
                updateFocusedItem(options.length - 1);
                break;

            case ' ': // Space——切换复选框、不关闭下拉（仅在有条目聚焦时）
                if (state.focusedIndex < 0 || state.focusedIndex >= options.length) {
                    // 没有条目聚焦——把空格字符放行给搜索框
                    return;
                }
                e.preventDefault();
                if (state.focusedIndex >= 0 && state.focusedIndex < options.length) {
                    const focusedEl = options[state.focusedIndex];
                    if (focusedEl.getAttribute('aria-disabled') !== 'true') {
                        // 点击聚焦条目以触发切换
                        focusedEl.click();
                        // 回调 Blazor 处理切换
                        dotNetRef.invokeMethodAsync('HandleSpace').catch(err => {
                            console.error('Error invoking HandleSpace:', err);
                        });
                    }
                }
                break;

            case 'Enter':
                e.preventDefault();
                if (state.focusedIndex >= 0 && state.focusedIndex < options.length) {
                    const focusedEl = options[state.focusedIndex];
                    if (focusedEl.getAttribute('aria-disabled') !== 'true') {
                        // 点击聚焦条目以触发切换
                        focusedEl.click();
                    }
                }
                // 回调 Blazor 处理回车（切换 + 关闭）
                dotNetRef.invokeMethodAsync('HandleEnter').catch(err => {
                    console.error('Error invoking HandleEnter:', err);
                });
                break;

            case 'Escape':
                e.preventDefault();
                e.stopPropagation();
                // 经 Blazor 关闭下拉
                dotNetRef.invokeMethodAsync('HandleEscape').catch(err => {
                    console.error('Error invoking HandleEscape:', err);
                });
                break;
        }
    };

    // 用捕获阶段（true）赶在浏览器默认行为之前运行
    inputElement.addEventListener('keydown', keyHandler, true);

    // 搜索词变化时复位聚焦索引
    const inputHandler = () => {
        // 用户键入即复位焦点
        state.focusedIndex = -1;
        setTimeout(() => {
            // 稍等一拍再清空全部聚焦态，让 Blazor 先完成重渲染
            const options = getVisibleOptions();
            options.forEach(el => {
                el.setAttribute('data-focused', 'false');
            });
        }, 50);
    };
    inputElement.addEventListener('input', inputHandler);

    const guard = createCompositionGuard(inputElement, { suppress: ['input'] });

    // 存下状态与处理器，供清理时使用
    multiSelectStates.set(inputId, {
        state,
        keyHandler,
        inputHandler,
        guard,
        inputElement
    });

    // 注：焦点由 Blazor 的 OnContentReady 事件接管，不在这里处理
}

/**
 * 摘除某个多选输入框的键盘事件处理器
 * @param {string} inputId - 输入元素的唯一标识
 */
export function removeMultiSelectInput(inputId) {
    const stored = multiSelectStates.get(inputId);
    if (stored) {
        stored.inputElement.removeEventListener('keydown', stored.keyHandler, true);
        stored.inputElement.removeEventListener('input', stored.inputHandler);
        stored.guard.dispose();
        multiSelectStates.delete(inputId);
    }
}

/**
 * 清理全部多选处理器（调试时有用）
 */
export function disposeAll() {
    multiSelectStates.forEach((stored, inputId) => {
        stored.inputElement.removeEventListener('keydown', stored.keyHandler, true);
        stored.inputElement.removeEventListener('input', stored.inputHandler);
        stored.guard.dispose();
    });
    multiSelectStates.clear();
}

/**
 * 以 preventScroll 选项聚焦元素，避免页面跳动
 * @param {HTMLElement} element - 要聚焦的元素
 */
export function focusElementWithPreventScroll(element) {
    if (element) {
        setTimeout(() => {
            element.focus({ preventScroll: true });
        }, 10);
    }
}
