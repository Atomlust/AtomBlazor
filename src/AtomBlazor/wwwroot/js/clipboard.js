/**
 * CopyText 组件的剪贴板 interop。
 *
 * 整个文件系于一条浏览器规则：剪贴板写入需要**瞬态用户激活**（transient user activation），
 * 而 await 会把这份激活耗掉。先解析文本、再发起写入，写入就会被拒绝——Safari 最为严格。
 * 所以异步路径从不在写入之前 await；它把*promise*交给 ClipboardItem，让浏览器跨着它
 * 把激活保持下去。
 */

/** 结果码，与 C# 侧的 CopyTextFailure 相互镜像。 */
const OK = 'ok';
const REFUSED = 'refused';
const NO_VALUE = 'noValue';
const UNSUPPORTED = 'unsupported';

/**
 * 当前浏览器能否接受放进 ClipboardItem 的 promise。
 *
 * 特性检测在这里只能帮到这一步：浏览器可能同时暴露 ClipboardItem 与 clipboard.write，
 * 却仍然拒绝 promise 值。那种情况会在写入时以拒绝的形式浮出水面，而无法被预判——
 * 这正是调用方要被告知失败的原因。
 */
function supportsPromiseWrite() {
  return typeof ClipboardItem !== 'undefined'
    && typeof navigator?.clipboard?.write === 'function';
}

/**
 * 复制已经在手的文本。
 * @param {string} text
 * @returns {Promise<string>} 上述结果码之一。
 */
export async function copyToClipboard(text) {
  if (typeof text !== 'string' || text.length === 0) {
    return NO_VALUE;
  }

  try {
    await navigator.clipboard.writeText(text);
    return OK;
  } catch {
    // 只针对以 http 提供的页面——那里 Clipboard API 根本不存在。
    // execCommand 救不回已过期的激活，所以把它当作所有失败的兜底 catch-all，
    // 恰恰是这类 bug 曾经隐形的原因。
    if (window.isSecureContext) {
      return REFUSED;
    }

    return copyViaExecCommand(text) ? OK : REFUSED;
  }
}

/**
 * 复制只有 .NET 回调才能产出的文本，且不把用户激活耗掉。
 *
 * `resolve()` 是在写入*之前*、在 ClipboardItem **内部**被调用的，所以浏览器会把激活
 * 保持到回调跑完为止。
 *
 * @param {object} dotNetRef - 指向 CopyText 实例的引用。
 * @param {string} methodName - 返回文本的 JSInvokable 方法名。
 * @returns {Promise<string>} 上述结果码之一。
 */
export async function copyFromAsyncSource(dotNetRef, methodName) {
  if (!dotNetRef || !methodName) {
    return NO_VALUE;
  }

  if (!supportsPromiseWrite()) {
    // 这里没有 promise 感知的路径。先解析会耗掉激活，所以仍可能被拒——
    // 这正是结果要被上报而不是被吞掉的原因。
    let text;
    try {
      text = await dotNetRef.invokeMethodAsync(methodName);
    } catch {
      return NO_VALUE;
    }
    return copyToClipboard(text);
  }

  // 不 await：promise 本身就是要交出去的值，这在接受的浏览器上正是全部要点。
  // 保留这次调用，是为了让下面的回退路径能复用它的结果而不是向 .NET 要两次
  // ——对做往返的消费方来说，那意味着两次往返。
  const valuePromise = dotNetRef.invokeMethodAsync(methodName);

  const blobPromise = valuePromise.then((text) => {
    if (typeof text !== 'string' || text.length === 0) {
      // 拒绝会中止写入，而不是往剪贴板上放一个空字符串。
      throw new Error('CopyText: no value to copy');
    }
    return new Blob([text], { type: 'text/plain' });
  });

  try {
    await navigator.clipboard.write([new ClipboardItem({ 'text/plain': blobPromise })]);
    return OK;
  } catch {
    // Safari 暴露了 ClipboardItem 与 clipboard.write，却在这里拒绝 promise 值——
    // 是实测结果，不是臆测。回退到「先解析、再写入」是它能接受的写法，回调够快
    // 就能成功。真正慢的回调在激活耗尽后仍可能被拒，这会被上报而不是被藏起来。
    let text;
    try {
      text = await valuePromise;
    } catch {
      return NO_VALUE;
    }

    if (typeof text !== 'string' || text.length === 0) {
      return NO_VALUE;
    }

    try {
      await navigator.clipboard.writeText(text);
      return OK;
    } catch {
      return REFUSED;
    }
  }
}

/**
 * 针对非安全上下文的最后手段——那里 navigator.clipboard 根本不存在。
 * @param {string} text
 * @returns {boolean}
 */
function copyViaExecCommand(text) {
  const textarea = document.createElement('textarea');
  textarea.value = text;
  textarea.style.position = 'fixed';
  textarea.style.opacity = '0';
  document.body.appendChild(textarea);
  textarea.select();

  try {
    return document.execCommand('copy');
  } catch {
    return false;
  } finally {
    document.body.removeChild(textarea);
  }
}

/**
 * 为一个 CopyText 元素主导复制手势。
 *
 * 它的存在是因为 Safari 会拒绝一切不在用户手势内部完成的剪贴板写入，而 Blazor 的点击
 * 不是：它要走 C# -> SignalR -> JS，等 JS 跑起来时手势窗口已经关了。短平快的写入还能
 * 钻这个空子，所以字面 Value 在那里复制没问题；但要花点真时间的值就不行——这是实测过
 * 两遍才定下来的方案。
 *
 * 在元素上挂监听把写送回了手势之内。值由 ClipboardItem 内部回调 .NET 产出，
 * 所以回调想花多久就花多久。
 *
 * @param {HTMLElement} element - CopyText 的根元素。
 * @param {object} dotNetRef - 指向组件的引用。
 * @returns {Object} 带 dispose 方法的清理对象。
 */
export function initializeCopy(element, dotNetRef) {
  if (!element || !dotNetRef) {
    return { dispose: () => {} };
  }

  const report = (outcome) => {
    dotNetRef.invokeMethodAsync('HandleCopyOutcome', outcome).catch(() => {});
  };

  const copy = () => {
    // 刻意不是 async，且 clipboard.write 在任何 await 之前就被调用。
    // 先 await 就是整个 bug 本身。
    if (!supportsPromiseWrite()) {
      dotNetRef.invokeMethodAsync('ResolveCopyValue')
        .then((text) => copyToClipboard(text))
        .then(report)
        .catch(() => report(REFUSED));
      return;
    }

    const valuePromise = dotNetRef.invokeMethodAsync('ResolveCopyValue');

    const blobPromise = valuePromise.then((text) => {
      if (typeof text !== 'string' || text.length === 0) {
        throw new Error('CopyText: no value to copy');
      }
      return new Blob([text], { type: 'text/plain' });
    });

    navigator.clipboard
      .write([new ClipboardItem({ 'text/plain': blobPromise })])
      .then(() => report(OK))
      .catch(() => {
        // 区分「无可复制」与真正的拒绝，且不向 .NET 要两次。
        valuePromise
          .then((text) => report(typeof text === 'string' && text.length > 0 ? REFUSED : NO_VALUE))
          .catch(() => report(NO_VALUE));
      });
  };

  const handleClick = () => copy();

  const handleKeyDown = (e) => {
    // role="button" 的 span 不会因这些按键得到原生 click，所以显式接线。
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      copy();
    }
  };

  element.addEventListener('click', handleClick);
  element.addEventListener('keydown', handleKeyDown);

  return {
    dispose: () => {
      element.removeEventListener('click', handleClick);
      element.removeEventListener('keydown', handleKeyDown);
    }
  };
}
