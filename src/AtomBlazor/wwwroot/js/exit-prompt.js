/**
 * 为「关标签页、重新加载、或点出站外链接」武装浏览器自带的「离开站点？」提示。
 * 浏览器在这里会忽略任何自定义消息并展示它自己的文案——这正是应用内提示改由 C#
 * 用真对话框处理的原因。
 */
const armed = new Map();

export function arm(id) {
    if (armed.has(id)) {
        return;
    }
    const handler = event => {
        event.preventDefault();
        // 旧浏览器需要设置 returnValue 才会显示提示。
        event.returnValue = '';
        return '';
    };
    window.addEventListener('beforeunload', handler);
    armed.set(id, handler);
}

export function disarm(id) {
    const handler = armed.get(id);
    if (!handler) {
        return;
    }
    window.removeEventListener('beforeunload', handler);
    armed.delete(id);
}
