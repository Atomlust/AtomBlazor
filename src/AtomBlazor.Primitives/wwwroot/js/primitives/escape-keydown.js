// 基于栈的 Escape 按键检测，服务于所有必须在 document 层监听 Escape 的浮层。
//
// 一个 document 级监听器、一个栈，只有最顶层的浮层处理这个键。
// 任何自己再挂 document 监听器的东西都游离于这个排序之外，届时 Escape 会把打开的浮层
// 一次性全关掉，而不是逐层剥开——开在 Dialog 里的 Popover 过去就正是这样。
//
// 内容持有焦点的浮层（Select、菜单）不属于这里。它们在自己的容器上处理 Escape
// 并阻止其继续冒泡，从而既绕开这个栈，又仍然优先于压在下面的任何东西。

const stack = [];
let listening = false;

function handleKeyDown(e) {
  if (e.key === 'Escape' && stack.length > 0) {
    const top = stack[stack.length - 1];
    top.dotNetRef.invokeMethodAsync(top.methodName).catch(() => {});
  }
}

export function initialize(dotNetRef, instanceId, methodName = 'JsOnEscapeKey') {
  if (!dotNetRef) {
    return;
  }

  // 重新打开时不能留下旧条目，否则栈会越堆越长，栈顶就不再是真正在屏幕上的那个浮层。
  dispose(instanceId);

  stack.push({ dotNetRef, instanceId, methodName });

  if (!listening) {
    document.addEventListener('keydown', handleKeyDown);
    listening = true;
  }
}

export function dispose(instanceId) {
  const index = stack.findIndex(s => s.instanceId === instanceId);
  if (index !== -1) {
    stack.splice(index, 1);
  }

  if (stack.length === 0 && listening) {
    document.removeEventListener('keydown', handleKeyDown);
    listening = false;
  }
}
