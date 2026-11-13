// Shared helpers of the end-to-end runs: the browser, the Kazo Fig windows and FigCraft's
// page API (window.figcraft in each iframe, craft-web-api).
import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { boot } from './host.mjs';

// FigCraft's first start downloads 25.5 MB of .wasm from the local host and compiles it.
export const READY_TIMEOUT_MS = 90_000;
export const STEP_TIMEOUT_MS = 20_000;
export const HOME = '/home/neo';

// A software WebGPU adapter (SwiftShader), so both of FigCraft's renderers can be tried.
export const launchBrowser = () => chromium.launch({
    args: ['--enable-unsafe-webgpu', '--enable-features=Vulkan', '--use-vulkan=swiftshader', '--use-webgpu-adapter=swiftshader', '--use-angle=vulkan'],
});

/** A fresh page with the OS booted; `errors` collects the page's uncaught errors. */
export async function bootPage(browser, baseUrl) {
    const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await boot(page, baseUrl);
    return { page, errors };
}

/** Kazo Fig's OS windows, in opening order. */
export const figWindows = page => page.locator('article[data-window-id]', { has: page.locator('.fig-window') });

/** One OS window by its id. */
export const windowById = (page, id) => page.locator(`article[data-window-id="${id}"]`);

/** Waits for `count` Kazo Fig windows to show FigCraft; returns the id of the newest one. */
export async function waitForWindow(page, count, known = []) {
    try {
        await page.locator('.fig-frame.running').nth(count - 1).waitFor({ timeout: READY_TIMEOUT_MS });
    } catch (error) {
        if (process.env.FIG_E2E_SHOTS) {
            await page.screenshot({ path: `${process.env.FIG_E2E_SHOTS}/window-${count}.png` });
        }
        throw error;
    }
    const ids = await figWindows(page).evaluateAll(nodes => nodes.map(node => node.getAttribute('data-window-id')));
    assert.equal(ids.length, count);
    return ids.find(id => !known.includes(id));
}

/** The iframe element of a window. */
export const iframeOf = (page, id) => windowById(page, id).locator('iframe.craft-frame-iframe');

/** The FigCraft frame of a window. */
export async function frameOf(page, id) {
    const handle = await iframeOf(page, id).elementHandle();
    return handle.contentFrame();
}

/** Runs a FigCraft command (engine.execute) in a frame. */
export const execute = (frame, command, params = {}) =>
    frame.evaluate(([name, args]) => window.figcraft.request('engine.execute', { command: name, params: args }), [command, params]);

/** Runs a method of FigCraft's control API in a frame. */
export const request = (frame, method, params = {}) =>
    frame.evaluate(([name, args]) => window.figcraft.request(name, args), [method, params]);

/** FigCraft's open designs (`doc.list`): `[{ index, id, name, dirty, active }]`. */
export const documents = frame => execute(frame, 'doc.list');

/** FigCraft's UI state (`ui.inspect`). */
export const inspectUi = frame => request(frame, 'ui.inspect');

/** Waits until the frame's active design has a node named `name` (`document.inspect`). */
export const waitForNodeNamed = (frame, name) => frame.waitForFunction(async wanted => {
    try {
        return JSON.stringify(await window.figcraft.request('document.inspect', {})).includes(wanted);
    } catch {
        return false;
    }
}, name, { timeout: STEP_TIMEOUT_MS });

/** Whether the frame's active design has a node named `name`. */
export const hasNodeNamed = async (frame, name) => JSON.stringify(await request(frame, 'document.inspect')).includes(name);

/** Runs a FigCraft menu item as a click on it would (`ui.menu.invoke`): File › Save, Save as, Open… */
export const menu = (frame, id) => request(frame, 'ui.menu.invoke', { id });

/** Answers the OS save dialog with a file name (in the folder it opens in). */
export async function answerSaveDialog(page, title, fileName) {
    const dialog = page.locator('section.file-dialog', { hasText: title });
    await dialog.waitFor({ state: 'visible', timeout: STEP_TIMEOUT_MS });
    await dialog.getByLabel('File name').fill(fileName);
    await dialog.getByRole('button', { name: 'Save' }).click();
    await dialog.waitFor({ state: 'hidden', timeout: STEP_TIMEOUT_MS });
}

/** Waits for a window's title bar to read `text`; on failure, says what it reads and the window's message. */
export async function expectTitle(page, id, text) {
    try {
        await page.waitForFunction(
            ([windowId, wanted]) => document.querySelector(`article[data-window-id="${windowId}"] header.window-chrome h2`)?.textContent?.trim() === wanted,
            [id, text],
            { timeout: STEP_TIMEOUT_MS });
    } catch (error) {
        const actual = await windowById(page, id).locator('header.window-chrome h2').first().textContent().catch(() => null);
        const notice = await windowById(page, id).locator('.fig-notice').textContent().catch(() => null);
        throw new Error(`title "${actual}" instead of "${text}"; notice: ${notice}`, { cause: error });
    }
}

/** The renderer FigCraft's canvas uses: `webgpu`, `webgl2` or null. */
export const rendererOf = frame => frame.evaluate(() => {
    const canvas = document.getElementById('figcraft_canvas');
    if (canvas.getContext('webgpu')) {
        return 'webgpu';
    }
    return canvas.getContext('webgl2') ? 'webgl2' : null;
});
