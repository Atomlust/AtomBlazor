// -c：overlay.js 触发器焦点归还与外部.dismiss 语义测试（同名件 1:1，仅模块路径改名）。
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { readFile } from 'node:fs/promises';

// Isolate closing from positioning and input listeners; these tests exercise focus after teardown.
const source = (await readFile(new URL('../../src/AtomBlazor.Primitives/wwwroot/js/primitives/overlay.js', import.meta.url), 'utf8'))
  .replace(/^import \* as (\w+) from .*;$/gm, 'const $1 = {};');
const { close } = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);
globalThis.CSS = { escape: value => value };

test('closing a picker restores its actual trigger when a custom ID replaces the generated ID', async () => {
  const focused = [];
  globalThis.document = { querySelector: () => null, getElementById: () => null };
  await close('custom-picker', {
    restoreFocusToId: 'generated-trigger',
    restoreFocusToElement: { focus: options => focused.push(options) }
  });
  assert.deepEqual(focused, [{ preventScroll: true }]);
});

test('an explicit focus target takes precedence and outside dismissal does not steal focus', async () => {
  let explicitFocus = 0;
  let anchorFocus = 0;
  globalThis.document = { querySelector: () => null, getElementById: () => ({ focus: () => explicitFocus++ }) };
  await close('explicit-picker', {
    restoreFocusToId: 'target',
    restoreFocusToElement: { focus: () => anchorFocus++ }
  });
  await close('outside-dismissal');
  assert.equal(explicitFocus, 1);
  assert.equal(anchorFocus, 0);
});
