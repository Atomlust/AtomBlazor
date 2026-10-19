/**
 * Tag Input JavaScript interop 模块。
 * 负责触发键的 preventDefault、粘贴事件读取与容器点击聚焦。
 *
 * 输入法组合期间，触发键与组件的 @oninput 都会被暂缓。
 * 缘由见 composition-guard.js。
 */

import { createCompositionGuard } from './composition-guard.js?assets=1';

const instances = new Map();

/**
 * 为标签输入组件初始化事件处理。
 * @param {HTMLElement} containerEl - 外层容器 div。
 * @param {HTMLInputElement} inputEl - 内联文本输入元素。
 * @param {DotNetObject} dotNetRef - Blazor 组件的引用。
 * @param {string} instanceId - 本实例的唯一 ID。
 * @param {object} config - 配置对象。
 * @param {number} config.triggers - TagInputTrigger 标志位掩码。
 */
export function initialize(containerEl, inputEl, dotNetRef, instanceId, config) {
  if (!containerEl || !inputEl || !dotNetRef) {
    return;
  }

  const handleKeyDown = (e) => {
    // 输入法组合期间，下面每个键都归属于输入法：Enter 与各分隔符负责上屏，
    // 方向键驱动候选列表，Escape 取消组合。对其中任何一个作出反应都会把
    // 写了一半的组合文本提交成标签，或与候选窗口抢夺按键。
    if (guard.isComposing || e.isComposing === true || e.keyCode === 229) {
      return;
    }

    const key = e.key;

    // 判断按键是否为已配置的触发键
    const isTrigger =
      (key === 'Enter' && (config.triggers & 1)) ||
      (key === ',' && (config.triggers & 2)) ||
      (key === ' ' && (config.triggers & 4)) ||
      (key === 'Tab' && (config.triggers & 8)) ||
      (key === ';' && (config.triggers & 16));

    if (isTrigger) {
      e.preventDefault();
      dotNetRef.invokeMethodAsync('JsTriggerAdd').catch(() => {});
      return;
    }

    // 空输入框上按 Backspace 移除最后一个标签
    if (key === 'Backspace' && inputEl.value === '') {
      dotNetRef.invokeMethodAsync('JsBackspace').catch(() => {});
      return;
    }

    // 方向键在建议列表中导航
    if (key === 'ArrowDown') {
      e.preventDefault();
      dotNetRef.invokeMethodAsync('JsSuggestionNext').catch(() => {});
      return;
    }

    if (key === 'ArrowUp') {
      e.preventDefault();
      dotNetRef.invokeMethodAsync('JsSuggestionPrev').catch(() => {});
      return;
    }

    // Escape 关闭建议列表
    if (key === 'Escape') {
      dotNetRef.invokeMethodAsync('JsSuggestionClose').catch(() => {});
    }
  };

  const handlePaste = (e) => {
    const text = e.clipboardData ? e.clipboardData.getData('text') : '';
    if (!text) {
      return;
    }

    // 判断粘贴文本是否含有与已配置触发方式匹配的分隔符
    const hasDelimiter =
      ((config.triggers & 2) && text.includes(',')) ||
      ((config.triggers & 16) && text.includes(';')) ||
      ((config.triggers & 4) && text.includes(' '));

    if (hasDelimiter) {
      e.preventDefault();
      dotNetRef.invokeMethodAsync('JsPasteText', text).catch(() => {});
    }
  };

  const handleContainerClick = (e) => {
    // 不抢走标签移除按钮的焦点
    if (e.target.closest('button')) {
      return;
    }
    inputEl.focus();
  };

  const guard = createCompositionGuard(inputEl, { suppress: ['input'] });

  // keydown 用捕获阶段监听，让 preventDefault 抢在浏览器默认行为之前
  inputEl.addEventListener('keydown', handleKeyDown, true);
  inputEl.addEventListener('paste', handlePaste);
  containerEl.addEventListener('click', handleContainerClick);

  instances.set(instanceId, {
    handleKeyDown,
    handlePaste,
    handleContainerClick,
    guard,
    inputEl,
    containerEl
  });
}

/**
 * 以编程方式聚焦输入元素。
 * @param {string} instanceId - 要聚焦的实例。
 */
export function focusInput(instanceId) {
  const stored = instances.get(instanceId);
  if (stored) {
    stored.inputEl.focus();
  }
}

/**
 * 摘除事件处理器并清理状态。
 * @param {string} instanceId - 要释放的实例。
 */
export function dispose(instanceId) {
  const stored = instances.get(instanceId);
  if (!stored) {
    return;
  }

  stored.inputEl.removeEventListener('keydown', stored.handleKeyDown, true);
  stored.inputEl.removeEventListener('paste', stored.handlePaste);
  stored.containerEl.removeEventListener('click', stored.handleContainerClick);
  stored.guard.dispose();
  instances.delete(instanceId);
}
