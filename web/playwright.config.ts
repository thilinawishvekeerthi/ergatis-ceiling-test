import { defineConfig, devices } from '@playwright/test';

/**
 * Browser end-to-end run against the real backend and a real PostgreSQL database.
 * Needs PostgreSQL on 127.0.0.1:5433 (../scripts/pg-start.sh) and the .NET SDK. Both servers start if they are not running.
 * Set PLAYWRIGHT_CHROMIUM_PATH to use an installed Chromium instead of `npx playwright install chromium`.
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  workers: 1,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'retain-on-failure',
    ...devices['Desktop Chrome'],
    launchOptions: process.env.PLAYWRIGHT_CHROMIUM_PATH ? { executablePath: process.env.PLAYWRIGHT_CHROMIUM_PATH } : {},
  },
  webServer: [
    {
      command: 'dotnet run --project ../src/Talent.Host --launch-profile http',
      url: 'http://localhost:5000/health/ready',
      reuseExistingServer: true,
      timeout: 180_000,
    },
    { command: 'npm run dev -- --port 5173 --strictPort', url: 'http://localhost:5173', reuseExistingServer: true },
  ],
});
