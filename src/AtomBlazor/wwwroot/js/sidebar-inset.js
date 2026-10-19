/**
 * SidebarInset 滚动工具。
 * 同时滚动元素与窗口，以覆盖两种布局形态：
 * - h-screen 布局中的元素级滚动（SidebarInset 即滚动容器）
 * - min-h-screen 布局中的文档级滚动（window 即滚动容器）
 */

/**
 * 滚动到给定元素与窗口的顶部。
 * @param {HTMLElement} element - 要滚回顶部的元素
 */
export function scrollToTop(element) {
  if (element) {
    element.scrollTo(0, 0);
  }
  window.scrollTo(0, 0);
}
