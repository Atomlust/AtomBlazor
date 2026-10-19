import { test, expect } from '@playwright/test';

async function go(page, route) {
    await page.goto(route, { waitUntil: 'networkidle' });
    await expect(page.locator('h1')).toBeVisible();
}

test('headless menubar moves between closed triggers and restores focus on Escape', async ({ page }) => {
    await go(page, '/primitives/menubar');
    const bar = page.getByRole('menubar').first();
    const triggers = bar.locator('button[aria-haspopup="menu"]');
    await triggers.first().focus();
    await page.keyboard.press('ArrowRight');
    await expect(triggers.nth(1)).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(bar.getByRole('menu')).toBeVisible();
    await expect(bar.getByRole('menu').getByRole('menuitem').first()).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(bar.getByRole('menu')).toHaveCount(0);
    await expect(triggers.nth(1)).toBeFocused();
    await triggers.first().click();
    await triggers.nth(1).hover();
    await expect(triggers.nth(1)).toHaveAttribute('aria-expanded', 'true');
    await page.locator('h1').click();
    await expect(bar.getByRole('menu')).toHaveCount(0);
});

test('headless navigation opens links with arrows and returns focus on Escape', async ({ page }) => {
    await go(page, '/primitives/navigation-menu');
    const trigger = page.locator('[data-nav-trigger]').first();
    await expect(page.getByRole('menu')).toHaveCount(0);
    await trigger.focus();
    await trigger.press('ArrowDown');
    const links = page.getByRole('menu').getByRole('menuitem');
    await expect(links.first()).toBeFocused();
    await page.keyboard.press('End');
    await expect(links.last()).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(page.getByRole('menu')).toHaveCount(0);
    await expect(trigger).toBeFocused();
});

test('responsive editable tabs expose add and keyboard reordering', async ({ page }) => {
    await go(page, '/components/tabs');
    const add = page.getByRole('button', { name: 'New sheet', exact: true });
    const root = add.locator('..');
    const tabs = root.getByRole('tab');
    const initial = await tabs.allTextContents();
    await tabs.first().focus();
    await page.keyboard.press('Control+ArrowRight');
    await expect(tabs.nth(1)).toHaveText(initial[0]);
    await add.click();
    await expect(tabs).toHaveCount(initial.length + 1);
});

test('conditional step indicators preserve markup order after insertion and removal', async ({ page }) => {
    await go(page, '/components/stepper');
    const section = page.getByTestId('conditional-steps');
    const items = section.locator('ol > li');
    await expect(items).toHaveCount(2);
    await section.getByRole('button', { name: 'Toggle review step' }).click();
    await expect(items).toHaveCount(3);
    await expect(items.nth(1)).toContainText('Review');
    await expect(items.nth(2)).toContainText('Finish');
    await section.getByRole('button', { name: 'Toggle review step' }).click();
    await expect(items).toHaveCount(2);
    await expect(items.nth(1)).toContainText('Finish');
});

for (const route of ['form-field-checkbox-group', 'form-field-date-range-picker', 'form-field-file-upload']) {
    test(`${route} displays EditForm field validation`, async ({ page }) => {
        await go(page, `/components/${route}`);
        const section = page.getByTestId('editform-validation');
        await section.getByRole('button', { name: 'Validate selection' }).click();
        await expect(section.getByText('Please make a selection.', { exact: true })).toBeVisible();
    });
}

test('scroll watcher finds late targets, updates thresholds, restores focus and waits for completion', async ({ page }) => {
    await go(page, '/components/scroll-to-top');
    const result = await page.evaluate(async () => {
        const module = await import('/_content/AtomBlazor.Components/js/scroll-to-top.js');
        const reports = [];
        const callback = { invokeMethodAsync: async (_, visible) => { reports.push(visible); } };
        module.observe('audit', '#audit-scroll', 50, callback);
        const missingHidden = reports.at(-1) === false;
        const target = document.createElement('div');
        target.id = 'audit-scroll';
        target.style.cssText = 'height:100px; overflow:auto; position:fixed; top:0; left:0; width:100px; z-index:99999';
        target.innerHTML = '<div style="height:1500px"></div>';
        document.body.append(target);
        await new Promise(requestAnimationFrame);
        target.scrollTop = 500;
        target.dispatchEvent(new Event('scroll'));
        // WebKit must present the starting scroll position before a second smooth scroll.
        // This also separates setup from activation, as real user input does.
        await new Promise(requestAnimationFrame);
        const appeared = reports.at(-1);
        module.observe('audit', '#audit-scroll', 600, callback);
        const hidden = reports.at(-1) === false;
        const button = document.createElement('button');
        button.dataset.slot = 'scroll-to-top';
        document.body.append(button);
        button.focus();
        let completed = false;
        const pending = module.scrollToTop('#audit-scroll', true).then(value => { completed = value; return value; });
        const movedFocus = document.activeElement === target;
        const waited = !completed;
        const reached = await pending;
        const top = target.scrollTop;
        module.dispose('audit'); target.remove(); button.remove();
        return { missingHidden, appeared, hidden, movedFocus, waited, reached, top };
    });
    expect(result).toEqual({ missingHidden: true, appeared: true, hidden: true, movedFocus: true, waited: true, reached: true, top: 0 });
});
