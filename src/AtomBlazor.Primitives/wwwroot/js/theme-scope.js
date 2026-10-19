/** 让局部主题跨过程门边界延续，而不接管全局主题化。 */
const names = [
  '--background', '--foreground', '--card', '--card-foreground', '--popover', '--popover-foreground',
  '--primary', '--primary-foreground', '--secondary', '--secondary-foreground', '--muted', '--muted-foreground',
  '--accent', '--accent-foreground', '--destructive', '--destructive-foreground', '--border', '--input', '--ring',
  '--radius', '--font-sans', '--font-mono', '--atom-spacing', '--atom-card-shadow', '--atom-menu-shadow', '--atom-menu-blur',
  '--atom-menu-opacity', '--atom-menu-background', '--atom-menu-foreground', '--atom-menu-accent', '--atom-menu-accent-foreground'
];
export function inheritTheme(reference, floating) {
  const scope = reference?.closest?.('[data-atom-theme-scope]');
  if (!scope || !floating) return () => {};
  const previousScope = floating.getAttribute('data-atom-theme-scope');
  floating.setAttribute('data-atom-theme-scope', '');
  const previous = new Map(names.map(name => [name, floating.style.getPropertyValue(name)]));
  const sync = () => {
    const styles = getComputedStyle(scope);
    for (const name of names) {
      const value = styles.getPropertyValue(name).trim();
      if (value) floating.style.setProperty(name, value);
      else floating.style.removeProperty(name);
    }
    floating.style.fontFamily = styles.fontFamily;
    // 用参照元素自己的 direction，而不是主题的：DirectionProvider 未必与主题作用域
    // 是同一个元素，而触发元素同时在这两者之内。
    floating.style.direction = getComputedStyle(reference).direction;
  };
  const previousFont = floating.style.fontFamily;
  const previousDirection = floating.style.direction;
  sync();
  const observer = new MutationObserver(sync);
  // 浮层打开期间，外层主题或文档调色板都可能变化。
  for (let ancestor = scope; ancestor; ancestor = ancestor.parentElement) {
    observer.observe(ancestor, { attributes: true, attributeFilter: [
      'style', 'class', 'data-base-color', 'data-primary-color', 'data-atom-density', 'data-atom-font',
      'data-atom-surface', 'data-atom-menu-color', 'data-atom-menu-accent'
    ] });
  }
  return () => {
    observer.disconnect();
    if (previousScope === null) floating.removeAttribute('data-atom-theme-scope');
    else floating.setAttribute('data-atom-theme-scope', previousScope);
    for (const [name, value] of previous) {
      if (value) floating.style.setProperty(name, value);
      else floating.style.removeProperty(name);
    }
    floating.style.fontFamily = previousFont;
    floating.style.direction = previousDirection;
  };
}
