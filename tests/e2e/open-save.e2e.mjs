// End-to-end runs of opening, saving, Save as and closing (doc/en/plan.md, work item 2, decisions
// 7 and 8), inside the Kazo OS host: `npm run test:e2e` after `dotnet build Kazo.Fig.slnx -c Debug`.
//
// FigCraft is driven through its page API (window.figcraft in the iframe): `node.create` draws,
// `ui.menu.invoke` runs File › Save, Save as and Open as a click on the menu does; Ctrl+Shift+S
// is typed on the keyboard; everything else goes through the OS: its save and open dialogs, its
// message box, the window's title bar and close button, and the Terminal (`xdg-open` starts the
// app associated with the file type).
import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import { openApp, runInTerminal, startHost } from './host.mjs';
import {
    HOME, STEP_TIMEOUT_MS, answerSaveDialog, bootPage, documents, execute, expectTitle, frameOf, hasNodeNamed, iframeOf, launchBrowser, menu,
    waitForNodeNamed, waitForWindow, windowById,
} from './common.mjs';

let host;
let browser;

before(async () => {
    host = await startHost();
    browser = await launchBrowser();
});

after(async () => {
    await browser?.close();
    host?.stop();
});

/** Waits for the Terminal's output to show `text`. */
async function expectInTerminal(page, text) {
    await page.locator('.xterm-rows').last().getByText(text, { exact: false }).first().waitFor({ timeout: STEP_TIMEOUT_MS });
}

/** Draws a frame named `name` in the design in front. */
const draw = (frame, name) => execute(frame, 'node.create', { type: 'frame', name, x: 40, y: 40, width: 200, height: 120 });

/** A new window whose design is saved as `fileName` with a frame `node` in it; returns its id and iframe. */
async function savedDesign(page, fileName, node) {
    await openApp(page, 'Kazo Fig');
    const id = await waitForWindow(page, 1);
    const frame = await frameOf(page, id);
    await draw(frame, node);
    await menu(frame, 'file.save');
    await answerSaveDialog(page, 'Save the design', fileName);
    await expectTitle(page, id, `${fileName} — Kazo Fig`);
    return { id, frame };
}

test('a new design is saved where the user says, saved again in place, and reopens from the OS', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);

    await openApp(page, 'Kazo Fig');
    const id = await waitForWindow(page, 1);
    const frame = await frameOf(page, id);
    await expectTitle(page, id, 'Untitled — Kazo Fig');

    await draw(frame, 'Landing');
    await expectTitle(page, id, '*Untitled — Kazo Fig');

    // The first save of a new design asks where to write it; the tab takes the file's name.
    await menu(frame, 'file.save');
    await answerSaveDialog(page, 'Save the design', 'landing.figcraft');
    await expectTitle(page, id, 'landing.figcraft — Kazo Fig');
    assert.equal((await documents(frame))[0].name, 'landing');

    // The next save writes the same file, with no dialog.
    await draw(frame, 'Pricing');
    await expectTitle(page, id, '*landing.figcraft — Kazo Fig');
    await menu(frame, 'file.save');
    await expectTitle(page, id, 'landing.figcraft — Kazo Fig');
    assert.equal(await page.locator('section.file-dialog').count(), 0);

    // `xdg-open` starts the app associated with .figcraft: a second window, on the file.
    await runInTerminal(page, `xdg-open ${HOME}/landing.figcraft`);
    const second = await waitForWindow(page, 2, [id]);
    await expectTitle(page, second, 'landing.figcraft — Kazo Fig');
    const reopened = await frameOf(page, second);
    await waitForNodeNamed(reopened, 'Pricing');
    assert.equal((await documents(reopened)).length, 1, 'the file replaced the blank design');

    assert.deepEqual(errors, []);
    await page.close();
});

test('a save of another tab never goes over the opened file: each tab keeps its own file', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);
    const { id: first } = await savedDesign(page, 'poster.figcraft', 'PosterArt');
    await windowById(page, first).locator('header.window-chrome .chrome-button-close').click();
    await windowById(page, first).waitFor({ state: 'detached', timeout: STEP_TIMEOUT_MS });

    // poster.figcraft opened from the OS: FigCraft's host file is poster.figcraft.
    await runInTerminal(page, `xdg-open ${HOME}/poster.figcraft`);
    const id = await waitForWindow(page, 1);
    const frame = await frameOf(page, id);
    await expectTitle(page, id, 'poster.figcraft — Kazo Fig');

    // A second tab: FigCraft hands its Save over as poster.figcraft; the window asks instead.
    await execute(frame, 'file.new');
    await draw(frame, 'CardArt');
    await menu(frame, 'file.save');
    await answerSaveDialog(page, 'Save the design', 'card.figcraft');
    await expectTitle(page, id, 'card.figcraft — Kazo Fig');

    // Back on the first tab, a save writes poster.figcraft in place.
    const tabs = await documents(frame);
    await execute(frame, 'doc.switch', { index: tabs.findIndex(tab => tab.name === 'poster') });
    await draw(frame, 'PosterMore');
    await menu(frame, 'file.save');
    await expectTitle(page, id, 'poster.figcraft — Kazo Fig');
    assert.equal(await page.locator('section.file-dialog').count(), 0);

    // Each file holds its own design.
    await runInTerminal(page, `xdg-open ${HOME}/poster.figcraft`);
    const posterWindow = await waitForWindow(page, 2, [id]);
    const poster = await frameOf(page, posterWindow);
    await waitForNodeNamed(poster, 'PosterMore');
    assert.equal(await hasNodeNamed(poster, 'CardArt'), false, 'poster.figcraft was not overwritten by the other tab');

    await runInTerminal(page, `xdg-open ${HOME}/card.figcraft`);
    const cardWindow = await waitForWindow(page, 3, [id, posterWindow]);
    const card = await frameOf(page, cardWindow);
    await waitForNodeNamed(card, 'CardArt');
    assert.equal(await hasNodeNamed(card, 'PosterArt'), false);

    assert.deepEqual(errors, []);
    await page.close();
});

test('Ctrl+Shift+S is Save as: it asks for a new file and binds the tab to it; a PDF is a copy', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);
    const { id, frame } = await savedDesign(page, 'report.figcraft', 'Report');

    // FigCraft would hand this Save as over as report.figcraft with saveAs: false; the chord is the window's.
    const box = await iframeOf(page, id).boundingBox();
    await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
    await page.keyboard.press('Control+Shift+S');
    await answerSaveDialog(page, 'Save the design', 'report-2.figcraft');
    await expectTitle(page, id, 'report-2.figcraft — Kazo Fig');

    // A plain save now writes report-2.figcraft in place.
    await draw(frame, 'Appendix');
    await expectTitle(page, id, '*report-2.figcraft — Kazo Fig');
    await menu(frame, 'file.save');
    await expectTitle(page, id, 'report-2.figcraft — Kazo Fig');

    // A PDF of the frames is a copy: its own dialog, a message, and the tab keeps its file.
    await menu(frame, 'file.exportFramesPdf');
    await answerSaveDialog(page, 'Save a copy', 'report.pdf');
    await windowById(page, id).locator('.fig-notice', { hasText: 'Copy saved: report.pdf.' }).waitFor({ timeout: STEP_TIMEOUT_MS });
    await expectTitle(page, id, 'report-2.figcraft — Kazo Fig');

    await runInTerminal(page, `ls ${HOME}`);
    await expectInTerminal(page, 'report-2.figcraft');
    const listing = await page.locator('.xterm-rows').last().innerText();
    for (const name of ['report.figcraft', 'report.pdf']) {
        assert.match(listing, new RegExp(name.replace('.', '\\.')));
    }

    assert.deepEqual(errors, []);
    await page.close();
});

test('closing with changes asks; Cancel keeps the window, Yes saves before closing', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);

    await openApp(page, 'Kazo Fig');
    const id = await waitForWindow(page, 1);
    const frame = await frameOf(page, id);
    await draw(frame, 'KeepMe');
    await expectTitle(page, id, '*Untitled — Kazo Fig');

    const close = windowById(page, id).locator('header.window-chrome .chrome-button-close');
    const question = page.locator('.message-box-dialog', { hasText: 'Save the changes' });

    // Cancel: nothing closes, nothing is lost.
    await close.click();
    await question.waitFor({ timeout: STEP_TIMEOUT_MS });
    await question.getByRole('button', { name: 'Cancel' }).click();
    await question.waitFor({ state: 'hidden', timeout: STEP_TIMEOUT_MS });
    assert.equal(await windowById(page, id).count(), 1);
    await waitForNodeNamed(frame, 'KeepMe');

    // Yes, then Cancel in the save dialog: the window stays open, and the message says so.
    await close.click();
    await question.waitFor({ timeout: STEP_TIMEOUT_MS });
    await question.getByRole('button', { name: 'Yes' }).click();
    const saveDialog = page.locator('section.file-dialog', { hasText: 'Save the design' });
    await saveDialog.waitFor({ timeout: STEP_TIMEOUT_MS });
    await saveDialog.getByRole('button', { name: 'Cancel' }).click();
    await windowById(page, id).locator('.fig-notice', { hasText: 'was not saved' }).waitFor({ timeout: STEP_TIMEOUT_MS });
    assert.equal(await windowById(page, id).count(), 1);

    // Yes, then a file name: the design is written, then the window closes.
    await close.click();
    await question.waitFor({ timeout: STEP_TIMEOUT_MS });
    await question.getByRole('button', { name: 'Yes' }).click();
    await answerSaveDialog(page, 'Save the design', 'closing.figcraft');
    await windowById(page, id).waitFor({ state: 'detached', timeout: STEP_TIMEOUT_MS });

    await runInTerminal(page, `xdg-open ${HOME}/closing.figcraft`);
    const reopened = await waitForWindow(page, 1);
    await expectTitle(page, reopened, 'closing.figcraft — Kazo Fig');
    await waitForNodeNamed(await frameOf(page, reopened), 'KeepMe');

    assert.deepEqual(errors, []);
    await page.close();
});

test('FigCraft\'s File › Open shows the OS dialog, and a design opens in the untouched window', { timeout: 400_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);
    const { id } = await savedDesign(page, 'picked.figcraft', 'PickedArt');
    await windowById(page, id).locator('header.window-chrome .chrome-button-close').click();
    await windowById(page, id).waitFor({ state: 'detached', timeout: STEP_TIMEOUT_MS });

    // A new window: its blank design is untouched, so the picked design replaces it here.
    await openApp(page, 'Kazo Fig');
    const fresh = await waitForWindow(page, 1);
    const blank = await frameOf(page, fresh);
    await menu(blank, 'file.open');
    const dialog = page.locator('section.file-dialog[role="dialog"]').last();
    await dialog.getByRole('heading', { name: 'Open a design' }).waitFor({ timeout: STEP_TIMEOUT_MS });
    await dialog.getByText('picked.figcraft', { exact: true }).dblclick();
    await dialog.waitFor({ state: 'detached', timeout: STEP_TIMEOUT_MS });

    await expectTitle(page, fresh, 'picked.figcraft — Kazo Fig');
    await waitForNodeNamed(blank, 'PickedArt');
    assert.equal((await documents(blank)).length, 1);
    assert.equal(await page.locator('article[data-window-id]', { has: page.locator('.fig-window') }).count(), 1);

    assert.deepEqual(errors, []);
    await page.close();
});
