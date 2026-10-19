// 用指针拖拽来重排标签页。
//
// 监听器挂在容器上而不是每个标签上，因为 Blazor 在列表重渲染时会修补并替换
// 标签按钮——按标签挂的监听器会在第一次重排时就丢失，
// 而那恰恰是最需要它们的时候。
//
// Ctrl 加方向键在 C# 里处理，书写方向也在那里——但它要回到这里通过 locate() 取位置，
// 因为顺序唯一正确的地方就是 DOM。

const states = new Map();

/**
 * 为一个标签列表启动拖拽重排。
 * @param {HTMLElement} root - 持有 tablist 的外层元素。
 * @param {object} dotNetRef - Blazor 组件引用。
 * @param {string} id - 本列表的唯一标识。
 */
export function initialize(root, dotNetRef, id) {
  if (!root || !dotNetRef || states.has(id)) return;

  const state = { root, dotNetRef, dragValue: null, indicator: null };
  states.set(id, state);

  const tabs = () => Array.from(root.querySelectorAll('[role="tab"][data-tab-value]'));

  const indexOf = (value) => tabs().findIndex((t) => t.getAttribute('data-tab-value') === value);

  // 一根摆在两个标签之间的 2px 竖条。相对容器绝对定位，而容器只有在拖拽进行期间
  // 才由这里置为 position:relative——写在这里而不是样式表里，好让一个从未被
  // 拖拽过的列表保持它原有的布局。
  const showIndicator = (tab, after) => {
    if (!state.indicator) {
      const bar = document.createElement('div');
      bar.style.cssText =
        'position:absolute;width:2px;top:4px;bottom:4px;pointer-events:none;' +
        'background:currentColor;opacity:0.7;border-radius:1px;';
      root.style.position = 'relative';
      root.appendChild(bar);
      state.indicator = bar;
    }

    const tabRect = tab.getBoundingClientRect();
    const rootRect = root.getBoundingClientRect();
    const edge = after ? tabRect.right : tabRect.left;

    state.indicator.style.left = `${edge - rootRect.left}px`;
    state.indicator.style.display = 'block';
  };

  const hideIndicator = () => {
    if (state.indicator) state.indicator.style.display = 'none';
  };

  state.onDragStart = (e) => {
    const tab = e.target.closest('[role="tab"][data-tab-value]');
    if (!tab || !root.contains(tab)) return;

    state.dragValue = tab.getAttribute('data-tab-value');

    if (e.dataTransfer) {
      e.dataTransfer.effectAllowed = 'move';
      // Firefox 在传输数据为空时根本不会开始拖拽。
      e.dataTransfer.setData('text/plain', state.dragValue);
    }
  };

  state.onDragOver = (e) => {
    if (state.dragValue === null) return;

    const tab = e.target.closest('[role="tab"][data-tab-value]');
    if (!tab) return;

    e.preventDefault();
    if (e.dataTransfer) e.dataTransfer.dropEffect = 'move';

    // 越过中线意味着落点在这个标签之后。以指针为参照来度量，而不是以被拖动的元素为参照，
    // 因为后备的拖拽图像并不在光标所在的位置。
    const rect = tab.getBoundingClientRect();
    const after = e.clientX > rect.left + rect.width / 2;
    showIndicator(tab, after);
  };

  state.onDrop = (e) => {
    if (state.dragValue === null) return;

    const tab = e.target.closest('[role="tab"][data-tab-value]');
    if (!tab) return;

    e.preventDefault();

    const value = state.dragValue;
    const from = indexOf(value);
    const overIndex = tabs().indexOf(tab);
    const rect = tab.getBoundingClientRect();
    const after = e.clientX > rect.left + rect.width / 2;

    // 先算插入点，再算最终下标。把标签摘出来再重新插入会让之后每个位置都下移一格，
    // 而这一行存在的意义正是避开这个 off-by-one。
    let target = after ? overIndex + 1 : overIndex;
    if (from < target) target -= 1;

    finish();

    if (from >= 0 && target >= 0 && target !== from) {
      dotNetRef.invokeMethodAsync('OnTabDropped', value, from, target);
    }
  };

  const finish = () => {
    state.dragValue = null;
    hideIndicator();
  };

  state.onDragEnd = finish;
  // 一个彻底离开列表的拖拽也该收起指示条，否则它会被留在原地，
  // 指着阅读者已经放弃的那个位置。
  state.onDragLeave = (e) => {
    if (!root.contains(e.relatedTarget)) hideIndicator();
  };

  root.addEventListener('dragstart', state.onDragStart);
  root.addEventListener('dragover', state.onDragOver);
  root.addEventListener('drop', state.onDrop);
  root.addEventListener('dragend', state.onDragEnd);
  root.addEventListener('dragleave', state.onDragLeave);
}

/**
 * 报告某个标签当前所在的位置以及标签总数。
 *
 * DOM 是这件事唯一诚实的来源。Blazor 在重排时会移动键控组件而不改变任何参数，
 * 于是没有任何子组件生命周期被运行，而 C# 里从注册时起保有的顺序
 * 从第一次移动开始就是旧的。
 *
 * @param {string} id - 传给 initialize 的标识。
 * @param {string} value - 标签的值。
 * @returns {{index: number, count: number}} - 找不到该标签时下标为 -1。
 */
export function locate(id, value) {
  const state = states.get(id);
  if (!state) return { index: -1, count: 0 };

  const tabs = Array.from(state.root.querySelectorAll('[role="tab"][data-tab-value]'));
  return {
    index: tabs.findIndex((t) => t.getAttribute('data-tab-value') === value),
    count: tabs.length,
  };
}

/**
 * 选中一个文本框的全部内容，就地重命名打开时使用。
 * @param {HTMLInputElement} input - 重命名输入框。
 */
export function selectAll(input) {
  if (input && typeof input.select === 'function') {
    input.select();
  }
}

/**
 * 停止拖拽重排并摘除全部监听器。
 * @param {string} id - 传给 initialize 的标识。
 */
export function dispose(id) {
  const state = states.get(id);
  if (!state) return;

  const { root } = state;
  root.removeEventListener('dragstart', state.onDragStart);
  root.removeEventListener('dragover', state.onDragOver);
  root.removeEventListener('drop', state.onDrop);
  root.removeEventListener('dragend', state.onDragEnd);
  root.removeEventListener('dragleave', state.onDragLeave);

  if (state.indicator && state.indicator.parentNode) {
    state.indicator.remove();
  }

  states.delete(id);
}
