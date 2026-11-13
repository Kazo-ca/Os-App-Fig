// The Kazo OS Store's screenshots (store/screenshots/), taken by install-and-open.mjs when it is
// given a screenshots folder, in the OS where Kazo Fig was just installed. The designs are
// FigCraft's own starter templates (its original content, `file.newFromTemplate`), so no
// third-party content appears.

const STEP_MS = 60_000;

/** egui paints on input: move the pointer over the canvas so the last frame is drawn. */
async function repaint(page, id, windowById) {
    const box = await windowById(page, id).locator('iframe.craft-frame-iframe').boundingBox();
    for (let step = 0; step < 6; step++) {
        await page.mouse.move(box.x + box.width / 2 + step * 7, box.y + box.height - 60 - step * 5);
        await page.waitForTimeout(400);
    }
}

/**
 * Opens one of FigCraft's starter templates in a new tab of the window and saves it under
 * `fileName` in the home folder, through the Kazo OS save dialog, so the title bar names the
 * design shown.
 */
async function saveTemplateAs({ page, id, frame, execute, menu, windowById }, template, fileName) {
    await execute(frame, 'file.newFromTemplate', { template, name: fileName.replace(/\.figcraft$/, '') });
    await execute(frame, 'view.zoomFit');
    await menu(frame, 'file.save');
    const dialog = page.locator('section.file-dialog', { hasText: 'Save the design' });
    await dialog.waitFor({ state: 'visible', timeout: STEP_MS });
    await dialog.getByLabel('File name').fill(fileName);
    await dialog.getByRole('button', { name: 'Save' }).click();
    await dialog.waitFor({ state: 'hidden', timeout: STEP_MS });
    await page.waitForFunction(
        ([windowId, wanted]) => document.querySelector(`article[data-window-id="${windowId}"] header.window-chrome h2`)?.textContent?.trim() === wanted,
        [id, `${fileName} — Kazo Fig`],
        { timeout: STEP_MS });
    await repaint(page, id, windowById);
}

export async function takeScreenshots({ page, second, reopened, execute, menu, shoot, windowById, openApp, HOME }) {
    const context = { page, id: second, frame: reopened, execute, menu, windowById };
    const chrome = windowById(page, second).locator('header.window-chrome').first();

    // The File Explorer of the journey is reopened at the end, listing the new files. Kazo Fig's
    // window, opened from it, lies over its title bar: the click is sent to the button itself.
    for (const close of await page.locator('article[data-window-id]', { hasText: 'File Explorer' }).locator('header.window-chrome .chrome-button-close').all()) {
        await close.dispatchEvent('click');
    }

    // 1. The mobile app template (screens, components), the window maximized.
    await chrome.dblclick({ position: { x: 300, y: 12 } });
    await saveTemplateAs(context, 'mobile-app', 'Mobile app.figcraft');
    await shoot(page, '1.png');

    // 2. The dashboard template, in a window of the desktop.
    await chrome.dblclick({ position: { x: 300, y: 12 } });
    await saveTemplateAs(context, 'dashboard', 'Dashboard.figcraft');
    await shoot(page, '2.png');

    // 3. The File Explorer in front: the designs open in Kazo Fig with a double-click.
    await openApp(page, 'File Explorer');
    const row = page.locator(`tr[data-item-path="${HOME}/Dashboard.figcraft"]`).first();
    await row.waitFor({ timeout: STEP_MS });
    await row.click({ position: { x: 40, y: 10 } });
    await shoot(page, '3.png');
}
