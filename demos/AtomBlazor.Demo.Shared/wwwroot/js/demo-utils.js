/**
 * Demo site utility functions.
 * Replaces inline eval() calls with proper module exports.
 */

let globalSearchHandler = null;
let globalSearchDotNetRef = null;

/**
 * Scroll the main content area to the top.
 */
export function scrollMainContentToTop() {
  const el = document.getElementById('main-content');
  if (el) {
    el.scrollTo(0, 0);
  }
}

/**
 * Register a global Ctrl+K keyboard shortcut for search.
 * @param {object} dotNetRef - DotNetObjectReference for callbacks
 */
export function registerGlobalSearch(dotNetRef) {
  disposeGlobalSearch();
  globalSearchDotNetRef = dotNetRef;
  globalSearchHandler = function (e) {
    if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
      e.preventDefault();
      if (globalSearchDotNetRef) {
        globalSearchDotNetRef.invokeMethodAsync('OpenSearch');
      }
    }
  };
  document.addEventListener('keydown', globalSearchHandler);
}

/**
 * Remove the global search keyboard shortcut.
 */
export function disposeGlobalSearch() {
  if (globalSearchHandler) {
    document.removeEventListener('keydown', globalSearchHandler);
    globalSearchHandler = null;
  }
  globalSearchDotNetRef = null;
}

/**
 * Get all localStorage keys matching a prefix.
 * @param {string} prefix - The prefix to filter by
 * @returns {string[]} Matching keys
 */
export function getLocalStorageKeysByPrefix(prefix) {
  const keys = [];
  for (let i = 0; i < localStorage.length; i++) {
    const key = localStorage.key(i);
    if (key && key.startsWith(prefix)) {
      keys.push(key);
    }
  }
  return keys;
}

/**
 * Persist the demo site display language to a cookie (read server-side for SSR prerender).
 * @param {string} lang - Language code ("zh" or "en")
 */
export function setLanguageCookie(lang) {
  document.cookie = `atom-lang=${encodeURIComponent(lang)}; path=/; max-age=31536000; samesite=lax`;
}

/**
 * Read the persisted display language cookie.
 * @returns {string|null} The stored language code, or null when absent
 */
export function readLanguageCookie() {
  const match = document.cookie.match(/(?:^|;\s*)atom-lang=([^;]*)/);
  return match ? decodeURIComponent(match[1]) : null;
}

/**
 * Mirror the display language onto the document element so assistive tech announces the right
 * language. Hosts with a static root document (WebAssembly index.html) cannot decide server-side.
 * @returns {string} The language code that was applied
 */
export function applyDocumentLanguage() {
  const lang = readLanguageCookie();
  const code = lang && lang.indexOf('en') === 0 ? 'en' : 'zh-CN';
  document.documentElement.lang = code;
  return code;
}
