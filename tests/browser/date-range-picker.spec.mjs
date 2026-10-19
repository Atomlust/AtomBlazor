import { test, expect } from '@playwright/test';

test('mobile date range quick select follows presets and manual calendar selections', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/components/date-range-picker', { waitUntil: 'networkidle' });
    const trigger = page.getByRole('button', { name: 'Choose a date range', exact: true });
    const popup = page.locator('[data-drp]');
    const select = popup.locator('[data-drp-presets-select] select');
    const selected = () => select.locator('option:checked');
    const apply = () => popup.getByRole('button', { name: 'Apply', exact: true }).click();
    const reopen = async () => {
        await expect(popup).toHaveCount(0);
        await trigger.click();
        await expect(select).toBeVisible();
    };

    await trigger.click();
    await expect(selected()).toHaveText('Select date range');
    for (const label of ['Yesterday', 'Today', 'Last 7 days', 'Last 30 days']) {
        const previous = await trigger.innerText();
        await select.selectOption({ label });
        await expect(selected()).toHaveText(label);
        await apply();
        await expect(trigger).not.toHaveText(previous);
        await reopen();
        await expect(selected()).toHaveText(label);
    }

    const calendar = popup.locator('[data-drp-calendar]').first();
    await calendar.getByRole('button', { name: '10', exact: true }).click();
    await expect(selected()).toHaveText('Custom');
    await calendar.getByRole('button', { name: '14', exact: true }).click();
    await expect(selected()).toHaveText('Custom');
    await apply();
    await reopen();
    await expect(selected()).toHaveText('Custom');
    const custom = await trigger.innerText();
    await select.selectOption({ label: 'Today' });
    await apply();
    await expect(trigger).not.toHaveText(custom);
    await reopen();
    await expect(selected()).toHaveText('Today');

    await page.setViewportSize({ width: 1440, height: 1000 });
    await popup.getByRole('button', { name: 'Yesterday', exact: true }).click();
    await page.setViewportSize({ width: 390, height: 844 });
    await expect(select).toBeVisible();
    await expect(selected()).toHaveText('Yesterday');
    await popup.getByRole('button', { name: 'Clear', exact: true }).click();
    await expect(selected()).toHaveText('Select date range');
});
