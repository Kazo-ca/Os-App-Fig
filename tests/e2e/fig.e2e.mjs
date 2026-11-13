// End-to-end runs of the Kazo Fig window inside the Kazo OS host (doc/en/plan.md, work item 1):
// `npm run test:e2e` after `dotnet build Kazo.Fig.slnx -c Debug`. Each test opens a fresh page.
// FigCraft starts on WebGPU by default, on WebGL 2 with the `--webgl` launch argument.
import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import { openApp, runInTerminal, startHost } from './host.mjs';
import {
    bootPage, documents, execute, figWindows, frameOf, iframeOf, inspectUi, launchBrowser, rendererOf, waitForNodeNamed, waitForWindow, windowById,
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

test('Kazo Fig opens from the Applications menu on a blank design, on WebGPU, in the OS theme, with the keyboard', { timeout: 300_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);

    await openApp(page, 'Kazo Fig');
    const id = await waitForWindow(page, 1);
    assert.equal(await page.locator('.fig-screen').count(), 0, 'no loading or error screen is left');
    const frame = await frameOf(page, id);
    const query = new URL(frame.url()).searchParams;
    assert.equal(query.has('control'), true, 'FigCraft installs window.figcraft only with ?control');
    assert.equal(query.get('author'), 'Neo', 'the user signs comments and board notes');
    assert.equal(await rendererOf(frame), 'webgpu');

    // The window left FigCraft's home screen for one blank design, in the OS theme (dark).
    const ui = await inspectUi(frame);
    assert.equal(ui.home, false);
    assert.equal(ui.dark, true);
    const designs = await documents(frame);
    assert.equal(designs.length, 1);
    assert.equal(designs[0].dirty, false);

    // R in the canvas picks the rectangle tool: the keyboard reaches FigCraft.
    const box = await iframeOf(page, id).boundingBox();
    await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
    await page.keyboard.press('r');
    await frame.waitForFunction(async () => (await window.figcraft.request('ui.inspect', {})).tool.tool === 'rectangle');

    assert.deepEqual(errors, []);
    await page.close();
});

test('the --webgl launch argument starts FigCraft on WebGL 2', { timeout: 300_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);

    await runInTerminal(page, 'launch ca.kazo.fig --webgl');
    const id = await waitForWindow(page, 1);
    const frame = await frameOf(page, id);
    assert.equal(new URL(frame.url()).searchParams.has('webgl'), true);
    assert.equal(await rendererOf(frame), 'webgl2');
    await execute(frame, 'node.create', { type: 'frame', name: 'WebGL frame' });
    await waitForNodeNamed(frame, 'WebGL frame');

    assert.deepEqual(errors, []);
    await page.close();
});

test('two windows run their own FigCraft, and a window brought to the front keeps its design', { timeout: 300_000 }, async () => {
    const { page, errors } = await bootPage(browser, host.baseUrl);

    await openApp(page, 'Kazo Fig');
    const firstId = await waitForWindow(page, 1);
    await openApp(page, 'Kazo Fig');
    const secondId = await waitForWindow(page, 2, [firstId]);
    const first = await frameOf(page, firstId);
    const second = await frameOf(page, secondId);
    assert.notEqual(first, second);

    // Each FigCraft keeps its own design.
    await execute(second, 'node.create', { type: 'frame', name: 'Second window frame' });
    await waitForNodeNamed(second, 'Second window frame');
    assert.doesNotMatch(JSON.stringify(await execute(first, 'doc.list')), /Second window frame/);
    assert.doesNotMatch(JSON.stringify(await first.evaluate(() => window.figcraft.request('document.inspect', {}))), /Second window frame/);

    // Bringing the first window to the front must not reload either iframe (Kazo OS ADR 0244).
    // The second window is in front: move it aside by its title so part of the first one shows,
    // then click inside the first window's FigCraft, which brings that window to the front.
    await second.evaluate(() => { window.kazoFigMarker = 'kept'; });
    const title = await windowById(page, secondId).locator('h2').first().boundingBox();
    await page.mouse.move(title.x + 40, title.y + title.height / 2);
    await page.mouse.down();
    await page.mouse.move(title.x + 290, title.y + title.height / 2 + 120, { steps: 8 });
    await page.mouse.up();
    const firstBox = await iframeOf(page, firstId).boundingBox();
    await page.mouse.click(firstBox.x + 120, firstBox.y + firstBox.height - 60);
    await page.waitForFunction(
        ([front, back]) => {
            const z = windowId => Number(document.querySelector(`article[data-window-id="${windowId}"]`)?.getAttribute('data-window-z'));
            return z(front) > z(back);
        },
        [firstId, secondId],
        { timeout: 10_000 });

    assert.equal(first.isDetached(), false, 'the first iframe was not reloaded');
    assert.equal(second.isDetached(), false, 'the second iframe was not reloaded');
    assert.equal(await second.evaluate(() => window.kazoFigMarker), 'kept');
    await waitForNodeNamed(second, 'Second window frame');
    assert.equal(await figWindows(page).count(), 2);

    assert.deepEqual(errors, []);
    await page.close();
});
