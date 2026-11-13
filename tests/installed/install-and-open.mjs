// Kazo Fig as the Kazo OS Store installs it (work item 4 of doc/en/plan.md):
// `npm run test:installed -- [--store] <Kazo OS wwwroot> [packages] [screenshots folder]`.
//
// --store: installs from the real Kazo OS Store (the published catalog of Os-AppMarketPlace, read
//   over the network) instead of `kazopkg install` from [packages]: the check after a release.
//
// <Kazo OS wwwroot>: a published Kazo OS, for example the kazoos-wasm-<version>.zip of a Kazo OS
//   release, unzipped (the trimmed OS users run; 3.3.3 is the minimum).
// [packages]: the folder `dotnet build src/Kazo.Fig -c Release -t:KazoOsPack` writes (default
//   src/Kazo.Fig/bin/kazoos-packages), the content of the kazoos-packages branch.
// [screenshots folder]: when given, the run also writes the Store's screenshots there (1.png …).
//
// The run serves the OS as a plain static host, answers the GitHub addresses of this repository's
// packages from [packages], installs Kazo Fig with `sudo kazopkg install` (its API check against
// the trimmed runtime included), writes a .figcraft with Kazo Fig, closes it, then double-clicks the
// file in the File Explorer: Kazo Fig opens again on it. Not part of CI: it needs WebGPU or WebGL
// in the browser and about five minutes.
import { createServer } from 'node:http';
import { mkdir, readFile, stat } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { chromium } from 'playwright';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const fromStore = process.argv.includes('--store');
const [wwwrootArgument, packagesArgument, shotsArgument] = process.argv.slice(2).filter(argument => argument !== '--store');
if (!wwwrootArgument) {
    console.error('usage: npm run test:installed -- <Kazo OS wwwroot> [packages folder] [screenshots folder]');
    process.exit(2);
}
const wwwroot = path.resolve(wwwrootArgument);
const packages = path.resolve(packagesArgument ?? path.join(root, 'src', 'Kazo.Fig', 'bin', 'kazoos-packages'));
const shots = shotsArgument && path.resolve(shotsArgument);

const REPOSITORY = 'Kazo-ca/Os-App-Fig';
const APP = 'Kazo Fig';
const LOGIN = 'neo';
const PASSWORD = 'matrix123';
const HOME = `/home/${LOGIN}`;
const STEP_MS = 120_000;
const TYPES = {
    '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css',
    '.json': 'application/json', '.wasm': 'application/wasm', '.svg': 'image/svg+xml', '.png': 'image/png',
    '.webmanifest': 'application/manifest+json', '.dat': 'application/octet-stream', '.blat': 'application/octet-stream',
};
// WebGPU and WebGL 2 on the CPU (SwiftShader), as the other journeys of this repository.
const launchOptions = {
    args: ['--enable-unsafe-webgpu', '--enable-features=Vulkan', '--use-vulkan=swiftshader', '--use-webgpu-adapter=swiftshader', '--use-angle=vulkan'],
};

const log = message => console.log(`[installed] ${message}`);

// A plain static server, as a release host is: no COOP/COEP, no cache.
const server = createServer(async (request, response) => {
    const relative = decodeURIComponent(new URL(request.url, 'http://host').pathname);
    let file = path.join(wwwroot, relative);
    if (!file.startsWith(wwwroot)) {
        response.writeHead(403).end();
        return;
    }
    try {
        if ((await stat(file)).isDirectory()) file = path.join(file, 'index.html');
        const body = await readFile(file);
        response.writeHead(200, { 'Content-Type': TYPES[path.extname(file)] ?? 'application/octet-stream', 'Cache-Control': 'no-store' });
        response.end(body);
    } catch {
        response.writeHead(404).end();
    }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const baseUrl = `http://127.0.0.1:${server.address().port}/`;

const waitForText = async (locator, predicate, what) => {
    const deadline = Date.now() + STEP_MS;
    while (Date.now() < deadline) {
        const text = (await locator.innerText()).replace(/ /g, ' ');
        if (predicate(text)) return text;
        await new Promise(resolve => setTimeout(resolve, 250));
    }
    throw new Error(`timed out waiting for ${what}`);
};

const figWindows = page => page.locator('article[data-window-id]', { has: page.locator('.fig-window') });
const windowById = (page, id) => page.locator(`article[data-window-id="${id}"]`);
const titleOf = (page, id) => windowById(page, id).locator('header.window-chrome h2').first();

/** Waits for a new Kazo Fig window showing FigCraft; returns its id. */
async function waitForNewWindow(page, known) {
    const deadline = Date.now() + STEP_MS;
    while (Date.now() < deadline) {
        const ids = await figWindows(page).evaluateAll(nodes => nodes
            .filter(node => node.querySelector('.fig-frame.running'))
            .map(node => node.getAttribute('data-window-id')));
        const id = ids.find(candidate => !known.includes(candidate));
        if (id) return id;
        await new Promise(resolve => setTimeout(resolve, 250));
    }
    throw new Error('no new Kazo Fig window showed FigCraft');
}

async function frameOf(page, id) {
    const handle = await windowById(page, id).locator('iframe.craft-frame-iframe').elementHandle();
    return handle.contentFrame();
}

const execute = (frame, command, params = {}) =>
    frame.evaluate(([name, args]) => window.figcraft.request('engine.execute', { command: name, params: args }), [command, params]);

/** Runs a FigCraft menu item as a click on it would (`ui.menu.invoke`): File › Save. */
const menu = (frame, id) => frame.evaluate(item => window.figcraft.request('ui.menu.invoke', { id: item }), id);

async function expectTitle(page, id, text) {
    await page.waitForFunction(
        ([windowId, wanted]) => document.querySelector(`article[data-window-id="${windowId}"] header.window-chrome h2`)?.textContent?.trim() === wanted,
        [id, text],
        { timeout: STEP_MS });
}

async function shoot(page, name) {
    if (!shots) return;
    await mkdir(shots, { recursive: true });
    // Let FigCraft paint its last frame.
    await page.waitForTimeout(1500);
    await page.screenshot({ path: path.join(shots, name) });
    log(`screenshot ${name}`);
}

async function openApp(page, name) {
    await page.getByRole('button', { name: 'App launcher' }).click({ timeout: STEP_MS });
    await page.getByRole('combobox', { name: 'Search applications' }).fill(name);
    await page.getByRole('button', { name: `Launch ${name}`, exact: true }).click();
}

/** `sudo kazopkg install` in the Terminal, as an administrator. */
async function installWithKazopkg(page) {
    await openApp(page, 'Terminal');
    const rows = page.locator('[data-terminal-panel] .xterm-rows').first();
    await waitForText(rows, text => text.includes('$'), 'the shell prompt');
    await page.locator('[data-terminal-panel] .xterm-screen').first().click();
    await page.keyboard.type(`sudo kazopkg install ${REPOSITORY}`);
    await page.keyboard.press('Enter');
    const prompt = page.locator('.credential-prompt-dialog, .aaa-window').first();
    await prompt.waitFor({ state: 'visible', timeout: STEP_MS });
    const login = prompt.locator('input[type=text]');
    if (await login.count() > 0 && !(await login.inputValue())) await login.fill(LOGIN);
    await prompt.locator('input[type=password]').fill(PASSWORD);
    await prompt.locator('input[type=password]').press('Enter');
    const output = await waitForText(rows, text => text.includes('available now') || /does not have|refused|error/i.test(text), 'the end of the installation');
    if (!output.includes('available now')) throw new Error(`kazopkg did not install ${APP}:\n${output}`);
    log(`kazopkg installed ${APP}.`);
    await page.locator('[data-terminal-panel]').first().locator('xpath=ancestor::article[1]').locator('header.window-chrome .chrome-button-close').click();
}

/** The Kazo OS Store: search Kazo Fig in the published catalog, "Install for me". */
async function installFromStore(page) {
    await openApp(page, 'Kazo OS Store');
    const store = page.locator('.store-root').first();
    await store.locator('input.store-search').fill(APP);
    const card = store.locator('button.store-card', { hasText: APP }).first();
    await card.waitFor({ timeout: STEP_MS });
    await card.click();
    await store.getByRole('button', { name: 'Install for me', exact: true }).click({ timeout: STEP_MS });
    // A first install from the catalog may ask an administrator.
    const prompt = page.locator('.credential-prompt-dialog, .aaa-window').first();
    const opened = store.getByRole('button', { name: 'Open', exact: true });
    await Promise.race([
        prompt.waitFor({ state: 'visible', timeout: STEP_MS }).then(async () => {
            const login = prompt.locator('input[type=text]');
            if (await login.count() > 0 && !(await login.inputValue())) await login.fill(LOGIN);
            await prompt.locator('input[type=password]').fill(PASSWORD);
            await prompt.locator('input[type=password]').press('Enter');
        }),
        opened.waitFor({ timeout: STEP_MS }),
    ]).catch(() => {});
    try {
        await opened.waitFor({ timeout: STEP_MS * 2 });
    } catch (error) {
        const banner = await store.locator('.store-banner').allInnerTexts().catch(() => []);
        throw new Error(`the Store did not install ${APP}: ${banner.join(' | ')}`, { cause: error });
    }
    log(`The Kazo OS Store installed ${APP}.`);
    await store.locator('xpath=ancestor::article[1]').locator('header.window-chrome .chrome-button-close').click();
}

// Screenshots: WebGPU on SwiftShader paints nothing into a page screenshot, so the screenshot
// run leaves WebGPU off and FigCraft draws with WebGL 2 (its fallback), which screenshots show.
const browserOptions = shots ? { args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] } : { ...launchOptions };
// The Store reads the catalog on GitHub: go through the machine's proxy when there is one.
const proxy = process.env.HTTPS_PROXY ?? process.env.https_proxy;
if (fromStore && proxy) browserOptions.proxy = { server: proxy, bypass: '127.0.0.1,localhost' };
const browser = await chromium.launch(browserOptions);
const errors = [];
let page;
try {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, ignoreHTTPSErrors: fromStore && Boolean(proxy) });
    // raw.githubusercontent.com, as kazopkg reads it (ADR 0182 of Kazo OS), answered locally.
    if (!fromStore) await context.route(new RegExp(`^https://raw\\.githubusercontent\\.com/${REPOSITORY}/kazoos-packages/`), async route => {
        const relative = decodeURIComponent(new URL(route.request().url()).pathname).split('/kazoos-packages/')[1];
        const file = path.join(packages, relative);
        const headers = { 'Access-Control-Allow-Origin': '*' };
        try {
            if (!file.startsWith(packages)) throw new Error('outside the packages folder');
            await route.fulfill({ status: 200, body: await readFile(file), headers: { ...headers, 'Content-Type': 'application/octet-stream' } });
            log(`served ${relative}`);
        } catch {
            await route.fulfill({ status: 404, headers });
        }
    });
    page = await context.newPage();
    page.on('pageerror', error => errors.push(String(error)));

    // First boot: the administrator is created.
    await page.goto(baseUrl, { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { name: 'Create administrator' }).waitFor({ timeout: 180_000 });
    await page.getByLabel('Login name').fill(LOGIN);
    await page.getByLabel('Display name').fill('Neo');
    await page.getByLabel('Password', { exact: true }).fill(PASSWORD);
    await page.getByLabel('Confirm password').fill(PASSWORD);
    await page.getByRole('button', { name: 'Create local profile' }).click();
    await page.getByText('Applications', { exact: true }).waitFor({ timeout: STEP_MS });

    if (fromStore) {
        await installFromStore(page);
    } else {
        await installWithKazopkg(page);
    }

    // Draw a design with the installed app and save it as .figcraft in the home folder.
    await openApp(page, APP);
    const first = await waitForNewWindow(page, []);
    const frame = await frameOf(page, first);
    await expectTitle(page, first, `Untitled — ${APP}`);
    // The window opens its blank design (file.new) once FigCraft is ready: wait for it.
    await frame.waitForFunction(async () => {
        try {
            return (await window.figcraft.request('engine.execute', { command: 'doc.list', params: {} })).length > 0;
        } catch {
            return false;
        }
    }, null, { timeout: STEP_MS });
    await execute(frame, 'node.create', { type: 'frame', name: 'Opened from the File Explorer', x: 40, y: 40, width: 240, height: 160 });
    await menu(frame, 'file.save');
    const dialog = page.locator('section.file-dialog', { hasText: 'Save the design' });
    await dialog.waitFor({ state: 'visible', timeout: STEP_MS });
    await dialog.getByLabel('File name').fill('installed.figcraft');
    await dialog.getByRole('button', { name: 'Save' }).click();
    await dialog.waitFor({ state: 'hidden', timeout: STEP_MS });
    await expectTitle(page, first, `installed.figcraft — ${APP}`);
    log(`${APP} saved installed.figcraft.`);
    await windowById(page, first).locator('header.window-chrome .chrome-button-close').click();
    await windowById(page, first).waitFor({ state: 'detached', timeout: STEP_MS });

    // A double-click on the .figcraft in the File Explorer: the installed app's file types open it.
    await openApp(page, 'File Explorer');
    const row = page.locator(`tr[data-item-path="${HOME}/installed.figcraft"]`).first();
    await row.waitFor({ timeout: STEP_MS });
    await row.dblclick({ position: { x: 40, y: 10 } });
    const second = await waitForNewWindow(page, []);
    await expectTitle(page, second, `installed.figcraft — ${APP}`);
    const reopened = await frameOf(page, second);
    await reopened.waitForFunction(async () => {
        try {
            return JSON.stringify(await window.figcraft.request('document.inspect', {})).includes('Opened from the File Explorer');
        } catch {
            return false;
        }
    }, null, { timeout: STEP_MS });
    log(`A double-click on installed.figcraft opened it in ${APP}.`);

    // The licenses came with the package.
    for (const file of ['legal/LICENSE.txt', 'legal/THIRD-PARTY-NOTICES.md', 'figcraft/NOTICE', 'figcraft/LICENSE-MIT', 'figcraft/LICENSE-APACHE', 'figcraft/OFL-inter.txt']) {
        const status = await reopened.evaluate(async url => (await fetch(url)).status, new URL(`../${file}`, reopened.url()).href);
        if (status !== 200) throw new Error(`${file} is not in the installed package (${status})`);
    }
    log('The licenses are installed with the app.');

    if (shots) await import('./store-screenshots.mjs').then(module => module.takeScreenshots({ page, second, reopened, execute, menu, shoot, windowById, openApp, HOME }));

    if (errors.length > 0) throw new Error(`page errors: ${errors.join(' | ')}`);
    log('PASS');
} catch (error) {
    console.error(`[installed] FAIL: ${error.message}`);
    await page?.screenshot({ path: 'installed-failure.png' }).catch(() => {});
    process.exitCode = 1;
} finally {
    await browser.close();
    server.close();
}
