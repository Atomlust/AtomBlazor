import { defineConfig } from "@playwright/test";

// 已全量跑通（158 passed／4 skipped／0 failed）：@playwright/test 与 chromium+webkit 皆在位，三宿主页面缺口已补齐。
// 执行前置＝三宿主以 Release 起停（--no-build 默认落 Debug 二进制，会静默起不来），测试侧不自举 webServer。
// 地址沿用本仓端口约定（Server 5199 / Wasm 5184 / Auto 5185，取自各宿主 launchSettings）；
// 可用环境变量 ATOM_SERVER_URL / ATOM_WASM_URL / ATOM_AUTO_URL 覆盖。
const hosts = {
    server: process.env.ATOM_SERVER_URL || "http://localhost:5199",
    wasm: process.env.ATOM_WASM_URL || "http://localhost:5184",
    auto: process.env.ATOM_AUTO_URL || "http://localhost:5185",
};

export default defineConfig({
    testDir: ".",
    testMatch: "*.spec.mjs",
    fullyParallel: true,
    workers: 2,
    retries: 0,
    timeout: 45_000,
    expect: { timeout: 8_000 },
    reporter: [["list"]],
    use: {
        viewport: { width: 1440, height: 1000 },
        locale: "en-GB",
        screenshot: "only-on-failure",
        trace: "retain-on-failure",
    },
    projects: Object.entries(hosts).flatMap(([host, baseURL]) =>
        ["chromium", "webkit"].map((browserName) => ({
            name: `${host}-${browserName}`,
            use: {
                baseURL,
                browserName,
                // 文档站显示语言默认中文（zh）；规格与英文基线 1:1，故每个测试上下文
                // 预先写入 atom-lang=en cookie，使 SSR 首帧即为英文，规格无需改动。
                contextOptions: {
                    storageState: {
                        cookies: [{
                            name: "atom-lang",
                            value: "en",
                            domain: new URL(baseURL).hostname,
                            path: "/",
                            expires: -1,
                            httpOnly: false,
                            secure: false,
                            sameSite: "Lax",
                        }],
                        origins: [],
                    },
                },
                ...(browserName === "webkit" && process.env.ATOM_WEBKIT_EXECUTABLE
                    ? { launchOptions: { executablePath: process.env.ATOM_WEBKIT_EXECUTABLE } }
                    : {}),
                ...(browserName === "chromium" && process.env.ATOM_CHROMIUM_CHANNEL
                    ? { channel: process.env.ATOM_CHROMIUM_CHANNEL }
                    : {}),
            },
        })),
    ),
});
