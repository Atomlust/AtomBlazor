import assert from "node:assert/strict";
import { test, expect } from "@playwright/test";

const tests = {
    async time(page, go) {
        await go("time-input");
        const group = page.getByRole("group", {
            name: "Appointment time",
            exact: true,
        });
        const period = group.getByRole("combobox");
        await expect(period).toHaveText(/pm/i);
        await period.click();
        await page.getByRole("option", { name: /^am$/i }).click();
        await expect(page.getByRole("status").first()).toHaveText(
            "Selected: 02:30:00",
        );
        await expect(page.getByRole("listbox")).toBeHidden();
        await expect(period).toBeFocused();
        await group
            .getByRole("spinbutton", { name: "Hour", exact: true })
            .press("ArrowUp");
        await expect(page.getByRole("status").first()).toHaveText(
            "Selected: 03:30:00",
        );
        const clock = group.getByRole("button", { name: "Open time picker" });
        await clock.click();
        const hour = page.getByRole("combobox", { name: "Hour", exact: true });
        await expect(hour).toHaveText("03");
        await hour.click();
        await page.getByRole("option", { name: "04", exact: true }).click();
        await expect(page.getByRole("status").first()).toHaveText(
            "Selected: 04:30:00",
        );
        await expect(page.getByRole("listbox")).toBeHidden();
        await expect(hour).toBeFocused();
        await page.keyboard.press("Escape");
        await expect(hour).toBeHidden();
        await expect(clock).toBeFocused();
    },
    async date(page, go) {
        await go("date-input");
        const group = page.getByRole("group", {
            name: "Appointment date",
            exact: true,
        });
        const fields = group.getByRole("spinbutton");
        await fields.nth(0).focus();
        await page.keyboard.press("ArrowRight");
        await expect(fields.nth(1)).toBeFocused();
        await page.getByRole("button", { name: "Switch to right-to-left", exact: true }).click();
        await expect(group).toHaveCSS("direction", "rtl");
        await fields.nth(0).focus();
        await page.keyboard.press("ArrowLeft");
        await expect(fields.nth(1)).toBeFocused();
        await page.getByRole("button", { name: "Switch to left-to-right", exact: true }).click();
        await expect(group).toHaveCSS("direction", "ltr");
        const form = page.locator("form");
        await form
            .getByRole("spinbutton", { name: "Year", exact: true })
            .fill("2026");
        await form
            .getByRole("spinbutton", { name: "Month", exact: true })
            .fill("02");
        await form
            .getByRole("spinbutton", { name: "Day", exact: true })
            .fill("30");
        await form.getByRole("button", { name: "Save", exact: true }).click();
        await expect(form.getByText("Saved successfully.")).toBeHidden();
        await expect(
            form.getByRole("spinbutton", { name: "Day", exact: true }),
        ).toHaveAttribute("aria-invalid", "true");
        await form
            .getByRole("spinbutton", { name: "Day", exact: true })
            .fill("28");
        await form.getByRole("button", { name: "Save", exact: true }).click();
        await expect(form.getByText("Saved successfully.")).toBeVisible();
    },
    async tree(page, go) {
        await go("tree-select");
        const trigger = page.getByRole("combobox", {
            name: "Team",
            exact: true,
        });
        await trigger.click();
        await expect(page.getByRole("textbox", { name: "Search…", exact: true })).toBeFocused();
        const branch = page
            .getByRole("treeitem")
            .filter({ hasText: /Engineering/ })
            .first();
        await branch.focus();
        const before = await branch.getAttribute("aria-expanded");
        await branch.press("Space");
        await expect(branch).toHaveAttribute(
            "aria-expanded",
            before === "true" ? "false" : "true",
        );
        await expect(trigger).toHaveText("Runtime");
        await branch.press("Enter");
        await expect(trigger).toHaveAttribute("aria-expanded", "false");
        await expect(trigger).toHaveText("Engineering");
        await expect(trigger).toBeFocused();
        const multi = page.getByRole("combobox", {
            name: "Teams",
            exact: true,
        });
        await multi.click();
        await expect(page.getByRole("textbox", { name: "Search…", exact: true })).toBeFocused();
        const multiBranch = page
            .getByRole("treeitem")
            .filter({ hasText: /Engineering/ })
            .first();
        await multiBranch.focus();
        await multiBranch.press("Enter");
        await expect(multi).toHaveAttribute("aria-expanded", "true");
        await expect(multiBranch).toHaveAttribute("aria-checked", "true");
        await multiBranch.press("Enter");
        await expect(multiBranch).toHaveAttribute("aria-checked", "false");
        await page.keyboard.press("Escape");
        await expect(multi).toBeFocused();
    },
    async menu(page, go) {
        await go("dropdown-menu");
        for (const dir of ["ltr", "rtl"]) {
            if (dir === "rtl") {
                await page.getByRole("button", { name: "Switch to right-to-left", exact: true }).click();
            }
            const trigger = page.getByRole("button", {
                name: "View and share",
                exact: true,
            });
            await trigger.click();
            const compact = page.getByRole("menuitemradio", {
                name: "Compact",
                exact: true,
            });
            await compact.click();
            await expect(compact).toHaveAttribute("aria-checked", "true");
            const share = page.getByRole("menuitem", {
                name: "Share",
                exact: true,
            });
            await share.focus();
            await share.press(dir === "ltr" ? "ArrowRight" : "ArrowLeft");
            await expect(
                page.getByRole("menuitem", { name: "Copy link", exact: true }),
            ).toBeFocused();
            const exportItem = page.getByRole("menuitem", {
                name: "Export",
                exact: true,
            });
            await exportItem.focus();
            await exportItem.press(dir === "ltr" ? "ArrowRight" : "ArrowLeft");
            await expect(
                page.getByRole("menuitem", { name: "CSV", exact: true }),
            ).toBeFocused();
            await page.keyboard.press("Escape");
            await expect(exportItem).toBeFocused();
            await exportItem.press(dir === "ltr" ? "ArrowRight" : "ArrowLeft");
            await page
                .getByRole("menuitem", { name: "CSV", exact: true })
                .press("Enter");
            await expect(page.getByRole("menu")).toHaveCount(0);
            await expect(
                page.getByText("Density: Compact. Exported CSV", {
                    exact: true,
                }),
            ).toBeVisible();
        }
    },
    async theme(page, go) {
        await go("theme");
        const scope = page.locator("[data-atom-theme-scope]").first();
        const button = page.getByRole("button", {
            name: "Consumer size override",
            exact: true,
        });
        const size = await button.evaluate((e) => ({
            height: getComputedStyle(e).height,
            padding: getComputedStyle(e).paddingLeft,
        }));
        assert.deepEqual(size, { height: "56px", padding: "32px" });
        await page
            .getByRole("button", { name: "Scoped menu", exact: true })
            .click();
        const menu = page.getByRole("menu");
        await expect(menu).toBeVisible();
        const tokens = [
            "--radius",
            "--atom-spacing",
            "--atom-menu-background",
            "--atom-menu-shadow",
        ];
        const read = (e, names) =>
            Object.fromEntries(
                names.map((n) => [
                    n,
                    getComputedStyle(e).getPropertyValue(n).trim(),
                ]),
            );
        assert.deepEqual(
            await menu.evaluate(read, tokens),
            await scope.evaluate(read, tokens),
        );
        await page.evaluate(() =>
            document.documentElement.classList.add("dark"),
        );
        await expect
            .poll(() =>
                menu.evaluate((e) =>
                    getComputedStyle(e).getPropertyValue("--background").trim(),
                ),
            )
            .toBe(
                await scope.evaluate((e) =>
                    getComputedStyle(e).getPropertyValue("--background").trim(),
                ),
            );
        await page.keyboard.press("Escape");
        await expect(
            page.getByRole("button", { name: "Scoped menu", exact: true }),
        ).toBeFocused();
    },
    async sortable(page, go) {
        await go("sortable");
        const list = page.getByRole("list", {
            name: "Sortable task list",
            exact: true,
        });
        const items = list.getByRole("listitem");
        const original = await items.allTextContents();
        await items.first().focus();
        await items.first().press("Space");
        await page.keyboard.press("ArrowDown");
        await page.keyboard.press("Space");
        await expect
            .poll(() => items.allTextContents())
            .toEqual([original[1], original[0], ...original.slice(2)]);
        const available = page.getByRole("list", {
            name: "Available items",
            exact: true,
        });
        const selected = page.getByRole("list", {
            name: "Selected items",
            exact: true,
        });
        const moved = await available.getByRole("listitem").first().innerText();
        const n = await selected.getByRole("listitem").count();
        await available.getByRole("listitem").first().focus();
        await page.keyboard.press("Space");
        await page.keyboard.press("Control+ArrowRight");
        await page.keyboard.press("Space");
        await expect(selected.getByRole("listitem")).toHaveCount(n + 1);
        await expect(selected.getByRole("listitem").last()).toHaveText(moved);
    },
    async scheduler(page, go) {
        await go("scheduler");
        const schedule = page.locator("[data-scheduler]").first();
        const scroll = schedule.locator("[data-scheduler-scroll]");
        await expect
            .poll(() => scroll.evaluate((e) => e.scrollTop))
            .toBeGreaterThan(600);
        assert(await scroll.evaluate((e) => e.scrollHeight > e.clientHeight));
        await scroll.scrollIntoViewIfNeeded();
        const mainY = await page
            .locator("#main-content")
            .evaluate((e) => e.scrollTop);
        await scroll.hover();
        await page.mouse.wheel(0, 3000);
        await expect
            .poll(() => scroll.evaluate((e) => e.scrollTop))
            .toBeGreaterThan(1200);
        assert.equal(
            await page.locator("#main-content").evaluate((e) => e.scrollTop),
            mainY,
        );
        await page.mouse.wheel(0, -3000);
        await expect.poll(() => scroll.evaluate((e) => e.scrollTop)).toBe(0);
        await schedule
            .getByRole("button", { name: "Week", exact: true })
            .click();
        await expect(schedule.locator("[data-scheduler-lane]")).toHaveCount(7);
        await schedule
            .getByRole("button", { name: "Day", exact: true })
            .click();
        await schedule
            .locator(
                "[data-scheduler-lane] > button:not([data-scheduler-event])",
            )
            .nth(20)
            .dblclick();
        const dialog = page.getByRole("dialog");
        await expect(dialog).toBeVisible();
        await dialog
            .getByRole("textbox", { name: "Title", exact: true })
            .fill("Validation appointment");
        await page.mouse.click(5, 5);
        await expect(dialog).toBeVisible();
        await dialog
            .getByRole("combobox", { name: "Repeat", exact: true })
            .click();
        await page.getByRole("option", { name: "Weekly", exact: true }).click();
        await expect(
            dialog.getByRole("checkbox", { name: "Monday", exact: true }),
        ).toBeChecked();
        await dialog
            .getByRole("checkbox", { name: "Wednesday", exact: true })
            .click();
        await expect(
            dialog.getByRole("checkbox", { name: "Wednesday", exact: true }),
        ).toBeChecked();
        await dialog.getByRole("button", { name: "Save", exact: true }).click();
        await expect(dialog).toBeHidden();
        const event = schedule
            .locator("[data-scheduler-event]")
            .filter({ hasText: "Validation appointment" });
        await event.dblclick();
        const scope = dialog.getByRole("combobox").first();
        await scope.click();
        await page.getByRole("option", { name: /series/i }).click();
        await expect(
            dialog.getByRole("checkbox", { name: "Wednesday", exact: true }),
        ).toBeChecked();
        await dialog
            .getByRole("combobox", { name: "Repeat", exact: true })
            .click();
        await page
            .getByRole("option", { name: "Monthly", exact: true })
            .click();
        await expect(dialog.getByText(/Repeat every/)).toHaveCount(0);
        await expect(
            dialog.getByRole("checkbox", { name: "Wednesday", exact: true }),
        ).toHaveCount(0);
        await dialog
            .getByRole("button", { name: "Cancel", exact: true })
            .click();
        await schedule
            .getByRole("button", { name: "Today", exact: true })
            .click();
        await expect
            .poll(async () =>
                new Date(
                    Number(
                        await schedule
                            .locator("[data-scheduler-lane]")
                            .first()
                            .getAttribute("data-start"),
                    ),
                )
                    .toISOString()
                    .slice(0, 10),
            )
            .toBe(new Date().toISOString().slice(0, 10));
    },
    async drawer(page, go) {
        await page.setViewportSize({ width: 390, height: 844 });
        await go("drawer");
        const trigger = page.getByRole("button", {
            name: "Open snapping drawer",
            exact: true,
        });
        await trigger.click();
        const drawer = page.getByRole("dialog", {
            name: "Delivery details",
            exact: true,
        });
        await expect(drawer).toBeVisible();
        const handle = drawer.getByRole("slider");
        await expect(handle).toHaveAttribute("aria-valuenow", "2");
 // 偏差（授权）：webkit 上 drawer-snaps.js 的 configure 可能晚于按键，事件被丢。
        // 先等 configure 收尾——reset() 把 Blazor 侧的 dvh 高度改写为 px——再按键。
        await expect
            .poll(() => drawer.evaluate((e) => e.style.height))
            .toMatch(/px$/);
        await handle.press("End");
        await expect(handle).toHaveAttribute("aria-valuenow", "3");
        await expect
            .poll(() =>
                drawer.evaluate((e) => e.getBoundingClientRect().height),
            )
            .toBeGreaterThan(740);
        await handle.press("Home");
        await expect(handle).toHaveAttribute("aria-valuenow", "1");
        await expect
            .poll(() =>
                drawer.evaluate((e) => e.getBoundingClientRect().height),
            )
            .toBeLessThan(265);
        await page.keyboard.press("Escape");
        await expect(drawer).toBeHidden();
        await expect(trigger).toBeFocused();
        await page.setViewportSize({ width: 1440, height: 1000 });
    },
    async sheet(page, go) {
        await page.setViewportSize({ width: 390, height: 844 });
        await go("select");
        const trigger = page
            .locator("section")
            .filter({
                has: page.getByRole("heading", {
                    name: "Bottom-sheet presentation",
                    exact: true,
                }),
            })
            .getByRole("combobox");
        await trigger.click();
        const dialog = page.getByRole("dialog", {
            name: "Delivery method",
            exact: true,
        });
        await expect(dialog).toBeVisible();
        const bounds = await dialog.boundingBox();
        assert(bounds.x >= -1 && bounds.x + bounds.width <= 391);
        await page.keyboard.press("Tab");
        assert(
            await dialog.evaluate((e) => e.contains(document.activeElement)),
        );
        await page
            .getByRole("option", { name: "Express delivery", exact: true })
            .click();
        await expect(dialog).toBeHidden();
        await expect(trigger).toHaveText("Express delivery");
        await expect(trigger).toBeFocused();
        await trigger.click();
 // 偏差（授权）：webkit 上重开时 select-sheet.js 的 focusListbox 晚于按键，
        // Escape 落不到 sheet 内即被丢；先等焦点入列再按键。
        await expect(dialog.locator("[data-select-content]")).toBeFocused();
        await page.keyboard.press("Escape");
        await expect(dialog).toBeHidden();
        await expect(trigger).toBeFocused();
        await page.setViewportSize({ width: 1440, height: 1000 });
    },
    async shop(page, go) {
        await page.setViewportSize({ width: 390, height: 844 });
        await page.goto("/recipes/mobile-shop", { waitUntil: "networkidle" });
        await page
            .getByRole("button", { name: "Add to cart", exact: true })
            .first()
            .click();
        const nav = page.getByRole("navigation", {
            name: "Shop navigation",
            exact: true,
        });
        await nav.getByRole("button", { name: /Cart/ }).click();
        await expect(
            page.getByText("Your cart", { exact: true }),
        ).toBeVisible();
        await page
            .getByRole("button", { name: "Place demo order", exact: true })
            .click();
        await nav.getByRole("button", { name: "Account", exact: true }).click();
        await expect(
            page.getByText("You placed 1 demo order(s) in this session.", {
                exact: true,
            }),
        ).toBeVisible();
        assert(
            await page
                .locator("#main-content")
                .evaluate((e) => e.scrollWidth <= e.clientWidth + 1),
        );
        await page.setViewportSize({ width: 1440, height: 1000 });
    },
    async motion(page, go) {
        await page.emulateMedia({ reducedMotion: "reduce" });
        await go("motion");
        await page
            .getByRole("button", { name: "Replay presets", exact: true })
            .click();
        await expect
            .poll(() =>
                page
                    .locator("#main-content")
                    .evaluate(
                        (e) =>
                            e
                                .getAnimations({ subtree: true })
                                .filter((a) => a.playState === "running")
                                .length,
                    ),
            )
            .toBe(0);
        await expect(
            page.getByRole("button", {
                name: "Hover or focus me",
                exact: true,
            }),
        ).toBeVisible();
        await go("render-state-provider");
        await expect(
            page.getByText("Render state: Interactive", { exact: true }),
        ).toBeVisible();
        await page.emulateMedia({ reducedMotion: "no-preference" });
    },
};

const names = {
    time: "Time Input preserves minutes through AM/PM and nested picker changes",
    date: "Date Input supports RTL keys and rejects invalid calendar dates",
    tree: "TreeSelect separates expansion from selection and checkbox toggling",
    menu: "nested menus support radio choices, RTL navigation and Escape",
    theme: "scoped themes reach portals while consumer sizes remain authoritative",
    sortable: "keyboard sorting updates the model and transfers between lists",
    scheduler: "Scheduler scrolls a full day and preserves weekly edit choices",
    drawer: "Drawer snaps on mobile and restores the opening trigger",
    sheet: "Select bottom sheet contains focus and restores it on close",
    shop: "Mobile Shop retains cart and order state across distinct screens",
    motion: "reduced motion retains usable content and interactive render state",
};

const errors = new WeakMap();
test.beforeEach(async ({ page }) => {
    const current = [];
    errors.set(page, current);
    page.on("pageerror", (error) => current.push(error.message));
    page.on("console", (message) => {
        if (
            message.type() === "error" &&
            /blazor|exception|fail:/i.test(message.text())
        ) {
            current.push(message.text());
        }
    });
});
test.afterEach(async ({ page }) => {
    expect(
        errors.get(page),
        "No browser exceptions or Blazor circuit failures",
    ).toEqual([]);
});

for (const [name, run] of Object.entries(tests)) {
    test(names[name], async ({ page }, testInfo) => {
 // 偏差（授权）：WebAssembly 宿主（net11 运行时装配 + 229 项 _framework 资源）
        // 首屏耗时明显高于 net10，Scheduler 一类重整件在 45s 默认闸门口反复压线，
        // 该组项目统一 test.slow()（仅放宽超时，不改判据）。
        if (testInfo.project.name.startsWith("wasm-")) {
            test.slow();
        }
        const go = async (route) => {
            await page.goto(`/components/${route}`, {
                waitUntil: "networkidle",
            });
            await expect(page.locator("h1")).toBeVisible();
        };
        await run(page, go);
    });
}

test("Drawer restores native and composed triggers after close-button and backdrop dismissal", async ({
    page,
}) => {
    await page.goto("/components/drawer", { waitUntil: "networkidle" });
    for (const name of ["Plain text trigger", "AsChild trigger"]) {
        const trigger = page.getByRole("button", { name, exact: true });
        await trigger.click();
        await page
            .getByRole("dialog")
            .getByRole("button", { name: "Close", exact: true })
            .click();
        await expect(page.getByRole("dialog")).toBeHidden();
        await expect(trigger).toBeFocused();
        await trigger.click();
        await expect(page.getByRole("dialog")).toBeVisible();
        await page.mouse.click(5, 5);
        await expect(page.getByRole("dialog")).toBeHidden();
        await expect(trigger).toBeFocused();
    }
});

test("Interactive Auto uses Server initially and WebAssembly on the next visit", async ({
    page,
}, testInfo) => {
    test.skip(
        !testInfo.project.name.startsWith("auto-"),
        "Only applicable to the Interactive Auto host.",
    );
    const sockets = [];
    page.on("websocket", (socket) => sockets.push(socket.url()));
    await page.goto("/components/render-state-provider", {
        waitUntil: "networkidle",
    });
    await expect(
        page.getByText("Render state: Interactive", { exact: true }),
    ).toBeVisible();
    expect(sockets.some((url) => url.includes("/_blazor"))).toBe(true);
 // 偏差（授权）：.NET 11 起 InteractiveAuto 把 WebAssembly 运行时的下载推迟到
    // 同一会话内的第二次导航（ net10 首载即下）。先做一次"下一访"再断言运行时资源，
    // 冷缓存下拉取耗时较长，轮询放宽到 20s。
    await page.goto("/components/render-state-provider", {
        waitUntil: "networkidle",
    });
    await expect(
        page.getByText("Render state: Interactive", { exact: true }),
    ).toBeVisible();
    await expect
        .poll(
            () =>
                page.evaluate(() =>
                    performance
                        .getEntriesByType("resource")
                        .some((entry) => /dotnet\.native.*\.wasm/.test(entry.name)),
                ),
            { timeout: 20_000 },
        )
        .toBe(true);
    sockets.length = 0;
    await page.reload({ waitUntil: "networkidle" });
    await expect(
        page.getByText("Render state: Interactive", { exact: true }),
    ).toBeVisible();
    expect(sockets.filter((url) => url.includes("/_blazor"))).toEqual([]);
    await tests.time(page, async (route) => {
        await page.goto(`/components/${route}`, { waitUntil: "networkidle" });
    });
});

test("DataGrid cell editors fit narrow columns and never cover Save or Cancel", async ({
    page,
}) => {
    // Two regressions shared one block of classes on the editor slot.
    //
    // A combobox and a multi select wrap their trigger in a container, so the grid's
    // `[&>button]` reset never reached it and the trigger kept its own PopoverWidth —
    // 200px and 300px — inside a narrower cell. The trigger then painted over the addon
    // holding Save and Cancel. Both buttons stayed in the DOM, visible and focusable, so
    // nothing but a hit test catches it.
    //
    // A checkbox is a plain button, which that same reset *did* reach: it was stretched
    // to the full cell and had its border removed, so an unchecked cell looked empty.
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.goto("/components/datagrid-editing", { waitUntil: "networkidle" });

    const grid = page.locator('[aria-label="Editors in narrow columns"]');
    await expect(grid).toBeVisible();

    for (const column of ["Status", "Skills", "Approved"]) {
        await grid.getByRole("button", { name: `Edit ${column}` }).first().click();

        const editor = grid.locator("[data-atom-cell-editor]");
        await expect(editor).toBeVisible();

        const measured = await editor.evaluate((element) => {
            const slot = element.querySelector('[class*="flex-1"]');
            const trigger = element.querySelector("button[aria-haspopup]");
            const checkbox = element.querySelector('button[role="checkbox"]');
            const button = (pattern) =>
                [...element.querySelectorAll("button")].find((candidate) =>
                    pattern.test(candidate.getAttribute("aria-label") || ""),
                );
            // The only assertion that catches "painted over": ask the browser what is
            // actually on top at the button's own centre point.
            const onTop = (target) => {
                const box = target.getBoundingClientRect();
                const hit = document.elementFromPoint(
                    Math.round(box.x + box.width / 2),
                    Math.round(box.y + box.height / 2),
                );
                return hit === target || target.contains(hit);
            };
            const save = button(/save/i);
            const cancel = button(/cancel/i);
            return {
                slotWidth: Math.round(slot.getBoundingClientRect().width),
                triggerWidth: trigger
                    ? Math.round(trigger.getBoundingClientRect().width)
                    : null,
                checkboxWidth: checkbox
                    ? Math.round(checkbox.getBoundingClientRect().width)
                    : null,
                checkboxBorder: checkbox
                    ? getComputedStyle(checkbox).borderTopWidth
                    : null,
                saveOnTop: onTop(save),
                cancelOnTop: onTop(cancel),
            };
        });

        assert.equal(
            measured.saveOnTop,
            true,
            `${column}: Save is covered by the editor`,
        );
        assert.equal(
            measured.cancelOnTop,
            true,
            `${column}: Cancel is covered by the editor`,
        );

        if (measured.triggerWidth !== null) {
            assert.ok(
                measured.triggerWidth <= measured.slotWidth,
                `${column}: trigger is ${measured.triggerWidth}px in a ${measured.slotWidth}px slot`,
            );
        }

        if (measured.checkboxWidth !== null) {
            assert.ok(
                measured.checkboxWidth < 40,
                `${column}: checkbox stretched to ${measured.checkboxWidth}px`,
            );
            assert.notEqual(
                measured.checkboxBorder,
                "0px",
                `${column}: checkbox lost its border`,
            );
        }

        await editor.getByRole("button", { name: /cancel/i }).click();
    }
});
