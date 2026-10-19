import { defineConfig } from '@playwright/test';

const baseURL = process.env.ATOM_STATE_CHANGES_URL || 'http://localhost:7188';

export default defineConfig({
    testDir: '.',
    testMatch: 'state-changes.checks.mjs',
    fullyParallel: true,
    workers: 2,
    retries: 0,
    timeout: 30_000,
    expect: { timeout: 8_000 },
    reporter: [['list']],
    use: { baseURL, viewport: { width: 1440, height: 1000 }, trace: 'retain-on-failure' },
    webServer: process.env.ATOM_STATE_CHANGES_URL ? undefined : {
        command: 'dotnet run --project fixtures/StateChanges --no-launch-profile --urls http://localhost:7188',
        env: { ASPNETCORE_ENVIRONMENT: 'Development' },
        url: baseURL,
        timeout: 120_000,
        reuseExistingServer: false,
    },
    projects: ['chromium', 'webkit'].map(browserName => ({
        name: browserName,
        use: {
            browserName,
            ...(browserName === 'chromium' && process.env.ATOM_CHROMIUM_CHANNEL
                ? { channel: process.env.ATOM_CHROMIUM_CHANNEL } : {}),
            ...(browserName === 'webkit' && process.env.ATOM_WEBKIT_EXECUTABLE
                ? { launchOptions: { executablePath: process.env.ATOM_WEBKIT_EXECUTABLE } } : {}),
        },
    })),
});
