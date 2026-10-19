/**
 * 文件上传 JavaScript interop 模块。
 * 负责把拖放的文件转移给 Blazor 的 InputFile 组件。
 */

/**
 * 重置文件输入元素的 value。
 * @param {HTMLInputElement} inputFileElement - 要重置的 InputFile 元素。
 */
export function resetInput(inputFileElement) {
    if (inputFileElement) {
        inputFileElement.value = '';
    }
}

/**
 * 初始化文件上传的拖放区。
 * @param {HTMLElement} dropZoneElement - 拖放区容器元素。
 * @param {HTMLInputElement} inputFileElement - InputFile 元素。
 * @returns {Object} 带 dispose 方法的清理对象。
 */
export function initializeDropZone(dropZoneElement, inputFileElement) {
    if (!dropZoneElement || !inputFileElement) {
        console.warn('FileUpload: Missing dropZone or inputFile element');
        return { dispose: () => {} };
    }

    const handleDragOver = (e) => {
        e.preventDefault();
        e.stopPropagation();
    };

    const handleDrop = (e) => {
        e.preventDefault();
        e.stopPropagation();

        if (!inputFileElement.disabled && e.dataTransfer?.files?.length > 0) {
            // 把文件转移给 InputFile 元素
            inputFileElement.files = e.dataTransfer.files;

            // 派发 change 事件以触发 Blazor 的 InputFile 处理器
            const event = new Event('change', { bubbles: true });
            inputFileElement.dispatchEvent(event);
        }
    };

    // 同步锁住输入：同一个 InputFile 上的第二次选择会在服务器渲染下一个输入之前
    // 替换掉 Blazor 的浏览器文件映射。
    const lockSelection = () => { inputFileElement.disabled = true; };
    inputFileElement.addEventListener('change', lockSelection, { capture: true });

    // 挂载事件监听器
    dropZoneElement.addEventListener('dragover', handleDragOver);
    dropZoneElement.addEventListener('drop', handleDrop);

    // 返回清理对象
    return {
        dispose: () => {
            dropZoneElement.removeEventListener('dragover', handleDragOver);
            dropZoneElement.removeEventListener('drop', handleDrop);
            inputFileElement.removeEventListener('change', lockSelection, { capture: true });
        }
    };
}

/**
 * 让用户把文件粘贴进上传区，作为拖放的替代方式（#485）。
 *
 * 监听器挂在 `document` 上而不是拖放区上。`paste` 事件只有在获得焦点的元素可编辑时
 * 才会在那个元素上触发；文件输入框并不可编辑，所以挂在拖放区自身的监听器永远收不到
 * 粘贴。作用域靠"焦点位于区内"的检查来划定，于是页面其他位置的粘贴不受打扰，
 * 同一页上的两个上传区也不会同时认领同一次粘贴。
 *
 * 粘贴的文本会被忽略——纯文本的 `clipboardData.files` 为空，什么都不做，
 * 把这次粘贴留给页面上其他可能想要它的处理方。
 *
 * 文件的移交方式与 `initializeDropZone` 移交拖放文件完全一致：赋值给输入框并随后
 * 派发 `change` 事件，让校验、限额与回调全部走同一条既有路径，而不是另起一份副本。
 *
 * @param {HTMLElement} dropZoneElement - 拖放区容器元素。
 * @param {HTMLInputElement} inputFileElement - InputFile 元素。
 * @returns {Object} 带 dispose 方法的清理对象。
 */
export function initializePaste(dropZoneElement, inputFileElement) {
    if (!dropZoneElement || !inputFileElement) {
        console.warn('FileUpload: Missing dropZone or inputFile element for paste');
        return { dispose: () => {} };
    }

    const handlePaste = (e) => {
        if (inputFileElement.disabled) {
            return;
        }

        // 仅在焦点位于本上传区内时处理，页面其他位置的粘贴不受打扰。
        if (!dropZoneElement.contains(document.activeElement)) {
            return;
        }

        const files = e.clipboardData?.files;
        if (!files || files.length === 0) {
            return;
        }

        e.preventDefault();

        // 文件输入框只接受 FileList，且 multiple="false" 时必须只收到一个文件。
        const transfer = new DataTransfer();
        const limit = inputFileElement.multiple ? files.length : 1;
        for (let i = 0; i < limit; i++) {
            transfer.items.add(files[i]);
        }

        inputFileElement.files = transfer.files;
        inputFileElement.dispatchEvent(new Event('change', { bubbles: true }));
    };

    document.addEventListener('paste', handlePaste);

    return {
        dispose: () => {
            document.removeEventListener('paste', handlePaste);
        }
    };
}
