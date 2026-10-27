import { expect, test, type Page } from '@playwright/test';

/**
 * The end-to-end flow across every module that exists today:
 * HR publishes a new nine-box version (Config + Security), an employee sends a costly development plan,
 * the manager found in the synced HR data approves (Integration + Workflow), HR gives the second approval.
 */
const shots = (name: string) => ({ path: `e2e-artifacts/${name}.png`, fullPage: true });

async function signInAs(page: Page, subject: string) {
  await page.goto('/');
  const signOut = page.getByRole('button', { name: 'Sign out' });
  if (await signOut.isVisible()) await signOut.click();
  await page.getByRole('button', { name: new RegExp(`\\(${subject.replace('.', '\\.')}\\)`) }).click();
  await expect(page.getByText(`Signed in as ${subject}`)).toBeVisible();
}

test('HR edits the nine-box, then a development plan is approved by manager and HR', async ({ page }) => {
  const title = `Leadership course ${Date.now()}`;

  // 1. HR administrator publishes a new version of the nine-box.
  await signInAs(page, 'dev.admin');
  await page.getByRole('link', { name: 'HR administration' }).click();
  await page.getByRole('link', { name: 'Grids' }).click();
  await page.getByRole('link', { name: /Nine-box/ }).click();
  await expect(page.getByRole('heading', { name: /NINE_BOX \(version \d+, (Published|Draft)\)/ })).toBeVisible();
  if (await page.getByRole('button', { name: 'Create new version' }).isVisible()) {
    await page.getByRole('button', { name: 'Create new version' }).click();
  }
  await expect(page.getByRole('heading', { name: /Draft\)/ })).toBeVisible();
  const star = page.getByLabel('Label for column 3, row 3');
  await star.fill('Future leader');
  await page.getByRole('button', { name: 'Save draft' }).click();
  await expect(page.getByText('Draft saved')).toBeVisible();
  await page.getByRole('button', { name: 'Publish' }).click();
  await expect(page.getByText(/^Published\./)).toBeVisible();
  await page.screenshot(shots('1-hr-published-nine-box'));

  // 2. The employee sends a plan that costs more than 1000.
  await signInAs(page, 'dev.employee');
  await page.getByRole('link', { name: 'My career' }).click();
  await page.getByRole('link', { name: 'Development plan' }).click();
  await page.getByLabel('Plan title').fill(title);
  await page.getByLabel('Estimated cost').fill('1500');
  await page.getByRole('button', { name: 'Submit for approval' }).click();
  const plan = page.getByRole('article', { name: title });
  await expect(plan).toContainText('PendingManager');
  await expect(plan).toContainText('Waiting for: dev.manager');
  await page.screenshot(shots('2-employee-submitted-plan'));

  // 3. The manager (from the synced Core HR data) approves.
  await signInAs(page, 'dev.manager');
  await page.getByRole('link', { name: 'My team' }).click();
  const task = page.getByRole('article', { name: title });
  await expect(task).toContainText('from dev.employee, estimated cost 1500');
  await page.screenshot(shots('3-manager-inbox'));
  await task.getByRole('button', { name: 'Approve' }).click();
  await expect(task).toBeHidden();

  // 4. HR gives the second approval because the plan costs 1000 or more.
  await signInAs(page, 'dev.admin');
  await page.getByRole('link', { name: 'HR administration' }).click();
  await page.getByRole('link', { name: 'Approvals' }).click();
  const hrTask = page.getByRole('article', { name: title });
  await expect(hrTask).toContainText('step PendingHr');
  await hrTask.getByRole('button', { name: 'Approve' }).click();
  await expect(hrTask).toBeHidden();

  // 5. The employee sees the approved plan and its full history.
  await signInAs(page, 'dev.employee');
  await page.getByRole('link', { name: 'My career' }).click();
  await page.getByRole('link', { name: 'Development plan' }).click();
  const done = page.getByRole('article', { name: title });
  await expect(done).toContainText('Approved');
  await expect(done.getByRole('list', { name: 'History' }).getByRole('listitem')).toHaveCount(4);
  await page.screenshot(shots('4-employee-plan-approved'));
});
