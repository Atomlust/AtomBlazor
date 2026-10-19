const defaults = {
  "Cancelled": "Move cancelled.",
  "Unchanged": "Item dropped in its original position.",
  "MoveFailed": "Unable to move the item. Try again.",
  "Transferred": "Item transferred to the connected list.",
  "TransferRejected": "The transfer was not allowed or no connected list is available.",
  "TransferFailed": "Unable to transfer the item. Try again.",
  "PickedUp": "Picked up item {0} of {1}. Use arrow keys to move.",
  "Disabled": "Reordering is disabled. Control plus Left or Right transfers to a connected list.",
  "Position": "Position {0} of {1}. Press Space or Enter to drop."
};
// Keep a keyboard preview in the DOM, restore it before Blazor applies a committed
// change, and leave the consumer's collection untouched on Escape.
export function attachKeyboardSorting(root, handle, filter, commit, transfer) {
  let active = null;
  let pending = false;
  let disposed = false;
  const ownedAttributes = new Map();
  const items = () => [...root.children].filter(e => e.hasAttribute('data-atom-sortable-item'));
  const enabled = () => root.dataset.keyboardSorting === 'true';
  const status = () => document.getElementById(`${root.id}-status`);
  const message = (key, ...args) => {
    const overrides = JSON.parse(root.dataset.sortableAnnouncements || 'null');
    const template = overrides?.[key] ?? defaults[key];
    return template.replace(/\{(\d+)\}/g, (_, index) => String(args[Number(index)] ?? ''));
  };
  const announce = text => { const region = status(); if (region) region.textContent = text; };
  const own = (element, name, value) => {
    let attributes = ownedAttributes.get(element);
    if (!attributes) { attributes = new Map(); ownedAttributes.set(element, attributes); }
    if (!attributes.has(name)) attributes.set(name, element.getAttribute(name));
    element.setAttribute(name, value);
  };
  const restoreAttributes = () => {
    for (const [element, attributes] of ownedAttributes) {
      for (const [name, value] of attributes) {
        if (value === null) element.removeAttribute(name);
        else element.setAttribute(name, value);
      }
    }
    ownedAttributes.clear();
  };
  const refresh = () => {
    restoreAttributes();
    if (!enabled()) { cancel(); return; }
    for (const item of items()) {
      const target = handle ? item.querySelector(handle) : item.querySelector?.('[data-atom-sortable-handle]') || item;
      if (!target || target.disabled || (filter && (target.matches(filter) || target.closest(filter)))) continue;
      own(target, 'tabindex', '0');
      const description = target.getAttribute('aria-describedby');
      own(target, 'aria-describedby', [description, `${root.id}-instructions`].filter(Boolean).join(' '));
    }
    if (active && (items().length !== active.original.length || active.original.some(item => item.parentElement !== root))) cancel();
  };
  const restoreOrder = state => {
    // If the app replaced the list while dragging, do not resurrect removed nodes.
    for (const item of state.original) if (item.parentElement === root) root.appendChild(item);
    delete state.item.dataset.keyboardDragging;
  };
  function cancel() {
    if (!active) return;
    const state = active;
    active = null;
    restoreOrder(state);
    if (!disposed) { state.target.focus(); announce(message('Cancelled')); }
  }
  const finish = async () => {
    const state = active;
    if (!state || pending) return;
    active = null;
    pending = true;
    const newIndex = items().indexOf(state.item);
    restoreOrder(state);
    try {
      if (newIndex !== state.oldIndex) await commit(state.oldIndex, newIndex);
      else announce(message('Unchanged'));
    } catch {
      announce(message('MoveFailed'));
    } finally {
      pending = false;
      if (!disposed && state.target.isConnected) state.target.focus();
    }
  };
  const finishTransfer = async direction => {
    const state = active;
    if (!state || pending || !transfer) return;
    active = null;
    pending = true;
    restoreOrder(state);
    let moved = false;
    try {
      moved = await transfer(state.oldIndex, direction);
      announce(moved ? message('Transferred') : message('TransferRejected'));
    } catch { announce(message('TransferFailed')); }
    finally {
      pending = false;
      if (!disposed && !moved && state.target.isConnected) state.target.focus();
    }
  };
  const keydown = event => {
    if (!enabled() || pending || event.isComposing || event.metaKey || event.altKey) return;
    const crossList = active && transfer && event.ctrlKey && ['ArrowLeft', 'ArrowRight'].includes(event.key);
    if (event.ctrlKey && !crossList) return;
    const item = event.target.closest('[data-atom-sortable-item]');
    if (!item || item.parentElement !== root) return;
    const actualHandle = handle || (item.querySelector?.('[data-atom-sortable-handle]') ? '[data-atom-sortable-handle]' : null);
    const target = actualHandle ? event.target.closest(actualHandle) : item;
    if (!target || !item.contains(target) || target.disabled) return;
    // Editable controls and action buttons continue to operate normally. An
    // explicitly configured handle can itself be a button.
    if (!actualHandle && event.target !== item && event.target.closest('input,textarea,select,button,a,[contenteditable=true]')) return;
    if (filter && event.target.closest(filter)) return;
    if (crossList) {
      event.preventDefault();
      void finishTransfer((event.key === 'ArrowRight' ? 1 : -1) * (getComputedStyle(root).direction === 'rtl' ? -1 : 1));
    } else if (event.key === ' ' || event.key === 'Enter') {
      event.preventDefault();
      if (active) { void finish(); return; }
      const original = items();
      active = { item, target, original, oldIndex: original.indexOf(item) };
      item.dataset.keyboardDragging = 'true';
      announce(message('PickedUp', active.oldIndex + 1, original.length));
    } else if (active && event.key === 'Escape') {
      event.preventDefault(); cancel();
    } else if (active && event.key === 'Tab') {
      cancel(); // Preserve native tab navigation without committing a move.
    } else if (active && ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) {
      event.preventDefault();
      if (root.dataset.sortableOrder === 'false') { announce(message('Disabled')); return; }
      const list = items();
      const current = list.indexOf(active.item);
      const rtl = getComputedStyle(root).direction === 'rtl';
      const delta = event.key === 'ArrowUp' ? -1 : event.key === 'ArrowDown' ? 1
        : (event.key === 'ArrowRight' ? 1 : -1) * (rtl ? -1 : 1);
      const next = event.key === 'Home' ? 0 : event.key === 'End' ? list.length - 1
        : Math.min(list.length - 1, Math.max(0, current + delta));
      if (next !== current) {
        root.insertBefore(active.item, next > current ? list[next].nextSibling : list[next]);
        active.target.focus();
        announce(message('Position', next + 1, list.length));
      }
    }
  };
  const pointerdown = () => cancel();
  const observer = new MutationObserver(refresh);
  observer.observe(root, { childList: true, attributes: true, attributeFilter: ['data-keyboard-sorting', 'data-sortable-order'] });
  root.addEventListener('keydown', keydown);
  root.addEventListener('pointerdown', pointerdown, true);
  refresh();
  return () => {
    disposed = true;
    observer.disconnect();
    cancel();
    restoreAttributes();
    root.removeEventListener('keydown', keydown);
    root.removeEventListener('pointerdown', pointerdown, true);
  };
}
