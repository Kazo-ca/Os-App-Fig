> [Version française](../fr/plan.md)

# Plan: the Kazo Fig app for Kazo OS

Step 0 of the Kazo OS [Craft app port guide](https://github.com/Kazo-ca/Os/blob/main/doc/en/wasm/dev/craft-app-port-guide.md),
written on 2026-11-13 (`TODO` issue [#1](https://github.com/Kazo-ca/Os-App-Fig/issues/1)).
This document sets the goal, what reading FigCraft showed, the decisions and the order of the
work items. The method comes from Kazo Word ([`Kazo-ca/Os-App-Word`](https://github.com/Kazo-ca/Os-App-Word))
and above all from Kazo Spreadsheets ([`Kazo-ca/Os-App-Spreadsheets`](https://github.com/Kazo-ca/Os-App-Spreadsheets),
[plan](https://github.com/Kazo-ca/Os-App-Spreadsheets/blob/spreadsheets/base/doc/en/plan.md)), the
first app on the shared module.

## Goal

Kazo Fig is the **interface design tool of Kazo OS**. It wraps the web build of
[FigCraft](https://github.com/storytold/figcraft) (ArtCraft team, `MIT OR Apache-2.0`), a design
tool written in Rust (egui, wgpu: WebGPU with a WebGL 2 fallback), in alpha, and connects its host
mode (`craft-web-host`) to the files, dialogs and file associations of Kazo OS. Nothing of FigCraft
is rewritten.

The Kazo code shared by the Craft apps (iframe, proxy, file picker, shortcuts, settings,
`DocumentSession`, save rule) comes from the `Kazo.Os.AppSdk.Craft` module shipped by Kazo OS
([ADR 0259](https://github.com/Kazo-ca/Os/blob/main/doc/en/wasm/dev/adr/0259-shared-craft-app-host-module.md),
guide [`craft-apps.md`](https://github.com/Kazo-ca/Os/blob/main/doc/en/wasm/framework/craft-apps.md)).
Kazo Fig keeps only its window, its list of formats, its iframe page, the FigCraft build and its
manifest.

## Reading FigCraft (step 0, 2026-11-13)

Read on the upstream tag `v0.18.1` (commit `4acc293`, `release` branch, 2026-11-11; the `main` of
the fork [`Kazo-ca/figcraft`](https://github.com/Kazo-ca/figcraft) is at `7b34dba` and does not
hold that commit) and on the official zip `figcraft-web-0.18.1.zip`.

| Point | Finding |
| --- | --- |
| Web build | `apps/figcraft-web` (Trunk, `public_url = "./"`), depends on `craft-web-host` and `craft-web-api` at `craft-libs` revision `fd5e2fe` (host contract version `"1"`, as WordCraft and GridCraft). Rust 1.95 at least. |
| Official zip | `https://github.com/storytold/figcraft/releases/download/v0.18.1/figcraft-web-0.18.1.zip`, SHA-256 `2fbeb93f5022b0a7cc9d8528f1ed7e2b54c94d213ef79e65128aa898c399f02b`, 10.5 MB, 40 files. Content: `index.html`, glue `figcraft-web-<hash>.js` (188 KB), `figcraft-web-<hash>_bg.wasm` (**25.5 MB**), licences, 25 `OFL-*.txt` texts (fonts embedded in the `.wasm`), `HOSTING.md`. |
| Size | Well under the Kazo OS limits (128 MiB per file, 64 MiB compressed package). Upstream itself keeps the `.wasm` under 25 MiB (Cloudflare limit). |
| Canvas | `figcraft_canvas`, hard-coded (one instance per HTML document: one iframe per window). Loading screen `figcraft_loading`. |
| Host mode | `data-host="page"` on the canvas: `figcraft:*` `CustomEvent`s. `ready` (app version, `file: false`: FigCraft reads bytes), `open { name, bytes }` (refused over 512 MB; no name: `Untitled.figcraft`), `save { name, saveAs, bytes }`, `dirty`, `failed`. `?author=` signs comments. In host mode the app starts **on its home screen**, with no document. |
| `save` and `saveAs` | Unlike GridCraft, **FigCraft computes `saveAs` itself** (`hostmode.rs`): `true` for a document file (`.figcraft`, `.json`) under a name other than the one the host opened (Save As, or a document the host never sent), `false` for a Save of the opened file and for every copy (PNG, JPG, SVG, PDF, `.fig`, variables). No `id`, no confirmation (craft-libs #611). |
| Several documents | The page holds several tabs (File › New, home). But the bridge keeps **one** "host file" for the whole page, and in host mode the Save of **any tab** with no path goes out under that name with `saveAs: false` (`app/files.rs`, `save`). Read in the code: a new tab, saved, would overwrite the file opened in the first one (question Q1, to confirm with a Playwright journey in work item 2). |
| `dirty` | True when **any** tab has changes (as GridCraft). |
| Commands (`window.figcraft`) | `request`, `execute`, `commands`, `docInspect`, `uiInspect`, `screenshot` (`craft-web-api`) plus `openFile`, `captureDownloads`, `files`, `readFile`, `inspect`. **Installed only with `?control` in the address**: the window passes `control` in the `parameters` of `CraftFrameOptions`. |
| Document list | `ui.inspect` has neither `documents` nor `windows` (it has `tabs[{ name }]` with no modified state): a third shape. The `doc.list` command gives `[{ index, id, name, dirty, active }]`: the window turns it into the `documents[{ id, title, dirty, active }]` shape that `OpenDocument.ListFrom` reads. |
| Theme, language | Theme: `ui.set { theme: "dark" \| "light" \| "system" }`. Language: command `pref.language { language }`, 25 catalogues (`fr`, `pt-BR`, `zh-Hans`, `zh-Hant`, `es-419`…). `file.new`, `file.save`, `file.saveAs`, `file.rename`, `file.close`, `doc.home` exist. |
| Browser picker | The same `input type=file` as WordCraft and GridCraft (the module replaces it): File › Open (`.figcraft .fig .jam .penpot .sketch .json .svg .png .jpg .jpeg .webp .gif .tif .tiff`), Import, Place image (pictures), Add library (`.figcraft`), import a plugin or variables (`.json`). |
| Formats | Opened: the list above (`.fig`, `.sketch`, `.penpot`, `.jam`, SVG and pictures are imported into a new document). Saved document: `.figcraft` (or `.json`). Copies: PNG, JPG, SVG, PDF, `.fig`, variables. A `.fig` opened then saved goes out as `.figcraft` with `saveAs: true` (OS dialog). |
| Settings | `localStorage`: the user's name, and **recovery copies** of unsaved files when the page stops on an internal error (can weigh several MB: to watch with the module's per-user settings file, work item 2). |
| Network | Live sessions and online sharing over WebSocket (`ws://`, `wss://`) to the desktop app or a `figcraft-server`: they go straight from the browser. Known limit, nothing to do for v1. |
| Fonts | `craft-fonts` fonts embedded in the `.wasm` (the web release requires them); "fonts installed on this computer" asks for the browser's Local Font Access permission (known limit, as Kazo Word). |
| Brand | The ArtCraft name and logo are trademarks. A **modified** build removes them; the Store listing says "based on FigCraft by the ArtCraft team", with no logo, and names no other design product. |

## Decisions

| # | Subject | Decision |
| --- | --- | --- |
| 1 | Name and identity | "**Kazo Fig**" (Benoit, 2026-11-13). Id `ca.kazo.fig`, publisher `pub.kazo`, assembly `Kazo.Fig`, category `graphics`, several windows. |
| 2 | Shared code | The `Kazo.Os.AppSdk.Craft` module (ADR 0259), in Kazo OS `main` since Os#491. Minimum version: **Kazo OS 3.3.3** (module, `canChangeScreen` of ADR 0263, `?autolaunch`). Until the `v3.3.3` tag exists, the OS submodule follows `main` and CI builds the OS from its sources, as Kazo Spreadsheets does. |
| 3 | Integration | One iframe per window, page `wwwroot/host/index.html` with the canvas `figcraft_canvas` `data-host="page"` `data-craft-app="figcraft"`, and `control` in the address parameters. |
| 4 | Changing screens | `canChangeScreen: false` in the manifest's `window` block from work item 1 (issue #2). |
| 5 | Build provenance | As Kazo Spreadsheets: first the **official 0.18.1 zip** pinned by SHA-256, in the repository under `wwwroot/figcraft/` with `figcraft.json`; then built from the fork (work item 3, branch `kazo/v0.18.1` = `4acc293`, with `craft-fonts`). |
| 6 | Save as | Revised in work item 2: FigCraft says `saveAs` itself, except for the Save as of the tab whose name is the opened file's (`Poster` → `Poster.figcraft`), handed over with `saveAs: false` like a Save. The window therefore reserves **Ctrl+Shift+S** (FigCraft's chord) and runs File › Save as itself, which always asks for a new file. The same item clicked in the menu stays a Save in that one case (known limit, in the upstream issue). |
| 7 | Several tabs | **Q1 = A** (Benoit, 2026-11-13): the window binds each file to the tab (`doc.list`'s `id`); a `save` from another tab is a Save As (OS dialog); upstream issue and PR prepared by Claude, published by Benoit. |
| 8 | Associated formats | **Q2 = A** (Benoit, 2026-11-13): default app for `.figcraft .fig .sketch .penpot .jam`; "Open with" for `.svg`, `.json` and pictures. |

## Questions asked to Benoit

Answered on 2026-11-13: "for the rest I agree with your recommendations" (A and A).

**Q1. Several tabs in one window.** If the user creates a new design in a window that already has
an opened file, FigCraft sends that new tab's Save under the opened file's name, with no `saveAs`.
The module's rule would rewrite that file: data loss.

- **A (recommended)**: a guard in the Kazo window and an upstream fix. The window binds each file
  to the **tab** (`doc.list`'s `id`), not to the name: on each `save` it asks for the active tab;
  if it is not the one bound to the file, it is a Save As (OS dialog). Claude prepares the upstream
  issue and PR (one "host file" per document), published by Benoit.
- B: fix it only in our fork (modified build from v1, ArtCraft marks removed), and offer the PR
  upstream.
- C: one document per window (close extra tabs): against how FigCraft is used, and nothing stops a
  click on "New" in the app.

**Q2. Formats associated with Kazo Fig.**

- **A (recommended)**: default app for `.figcraft .fig .sketch .penpot .jam` (no other Kazo OS app
  reads them); "Open with" only for `.svg`, `.json` and pictures (`.png .jpg .jpeg .webp .gif .tif
  .tiff`), whose default app stays the OS one.
- B: default app only for `.figcraft`; everything else as "Open with".

## Work items

Series `fig/base`, one branch `fig/<n>-<topic>` and one PR to `fig/base` per work item. As for Kazo
Spreadsheets, the guide's work items 2 and 3 (open and save, rest of the frame) are merged: the
module does most of them. Benoit merges `fig/base` into `main`.

| No. | Branch | Content | Depends on |
| --- | --- | --- | --- |
| 0 | `fig/0-plan` | this plan (fr and en) | — |
| 1 | `fig/1-solution` | template solution renamed (`Kazo.Fig`, `.Os`, `.Tests`), manifest (`canChangeScreen: false`), `tools/vendor-figcraft.sh` (pinned 0.18.1 zip) and `figcraft.json`, iframe page, manifest and provenance tests, OS submodule on `main` (3.3.3) | plan merged |
| 2 | `fig/2-window` | window on the module: formats (Q2), open, save, tab guard (Q1), close, picker, shortcuts, theme, language, author, start on a blank document; two windows; WebGPU and WebGL 2; Playwright | work item 1, answers to Q1 and Q2 |
| 3 | `fig/3-build-from-source` | `Kazo-ca/figcraft` fork (branch `kazo/v0.18.1`) as a submodule, `tools/build-figcraft.sh` (with `craft-fonts` at the release's revision), `build-figcraft.yml` workflow, commit test | work item 1; in parallel with 2 |
| 4 | `fig/4-store` | icon with no ArtCraft logo, fr and en listing, README rebuilt from the listing (`store:readme` script, "Try it" link `?autolaunch=ca.kazo.fig`), `THIRD-PARTY-NOTICES.md`, submodule on `v3.3.3`, install in a published Kazo OS, tag, catalog, `features.md` | work items 2 and 3, Kazo OS 3.3.3 released |

**Done when**: Kazo Fig installs from the Store of a published Kazo OS, opens a `.figcraft` by
double-click, saves it, and two windows run together.

## Work item 1: solution and start-up window (2026-11-13)

| File | Role |
| --- | --- |
| `Kazo.Fig.slnx`, `src/Kazo.Fig`, `src/Kazo.Fig.Os`, `tests/Kazo.Fig.Tests` | template solution renamed |
| `FigApp.cs` | manifest: `ca.kazo.fig`, category `graphics`, several windows, 1200 × 800 (minimum 800 × 520), `canChangeScreen: false` (decision 4), no capability nor format (work item 2) |
| `tools/vendor-figcraft.sh`, `wwwroot/figcraft/figcraft.json` | official 0.18.1 zip pinned by SHA-256 (decision 5): glue, `.wasm`, `snippets/`, licences and the 29 OFL licences of the embedded fonts; neither upstream's page, `README` nor `_headers` |
| `wwwroot/host/index.html` | iframe page: canvas `figcraft_canvas` `data-craft-app="figcraft"`, the module's `craft-proxy.js` and `craft-page.css` |
| `FigWindow.*`, `Hosting/` | start-up on the module (loading, error, "Retry", 60 s timeout), address parameters (`control`, `author`, `webgl`), OS theme (`ui.set { theme }`), language (`pref.language` with the culture name), a blank design at start (`file.new`, then `ui.set { home: false }`) |
| `tests/e2e/` | Playwright journeys in the OS (`npm run test:e2e` after `dotnet build -c Debug`) |
| `.github/` | CI as Kazo Spreadsheets: OS submodule on `main` (`KAZOOS_BRANCH: main`) and Release profile built from the sources (`KAZOOS_USE_SOURCE`) until `v3.3.3` is tagged |

Findings:

- **`?control` is required**: without it FigCraft does not install `window.figcraft` and the module's proxy cannot ask it anything. The window always adds it.
- **Language**: FigCraft resolves a BCP 47 tag itself (`fr-CA`, `pt-PT` → `pt-BR`, `es-MX` → `es-419`, Chinese by script and region). The window passes the culture name as it is, with no list to maintain.
- **Theme**: `light` or `dark` from the OS, never `system` (which would follow the browser).
- **Package**: 10.7 MB `.kazoapp`; 25.5 MB `.wasm`.
- **Kazo OS 3.3.3** is not tagged yet: the install in a published Kazo OS (`kazopkg install`) happens in work item 4, with the submodule on `v3.3.3`.

Tests: 34 xUnit tests (manifest, build provenance, iframe page, fr and en texts, parameters, language, theme) and 3 Playwright journeys in the OS: WebGPU, dark theme, blank design, keyboard (R picks the rectangle); WebGL 2 with `--webgl`; two windows each with its own design, and a window brought to the front with no iframe reloaded (ADR 0244). Screenshot: `kazo-fig-work-item-1-webgl.png` in the project files (`fig/trials/`).

## Work item 2: complete window (2026-11-13)

| File | Role |
| --- | --- |
| `Documents/FigFormats.cs` | formats (decision 8, Q2 = A): default app for `.figcraft .fig .sketch .penpot .jam`, "Open with" for `.json .svg` and images, `.pdf` as a copy only; FigCraft's rule to tell the design from a copy (`.figcraft` or `.json`, except `.variables.json`); the File › Open picker (the only one that accepts `.json`) |
| `Documents/FigTab.cs`, `Documents/FigTabs.cs` | the tabs read from `doc.list` (`id` = FigCraft's `uid`, stable) and their file, bound by `id` (decision 7, Q1 = A); the decision for each `save` by the tab in front; title; tabs to save before closing |
| `FigWindow.Documents.cs` | open (`figcraft:open`, then the new tab in `doc.list` gets the file; the untouched blank design of the start is closed), save, Save as, copies, close guard |
| `FigWindow.Proxy.cs`, `Hosting/FigKeys.cs` | FigCraft's file picker replaced by the OS dialog, Ctrl+N (new window), Ctrl+Shift+S (Save as, decision 6 revised), per-user storage, theme and language |
| `FigApp.cs` | capabilities (dialogs, home folder, private folder, `apps.launch`) and file associations |

How the window decides a `save` (FigCraft keeps one host file per page):

- The window asks for the tab in front (`doc.list`). A name other than `.figcraft` or `.json` is a **copy**: "Save a copy" dialog, the tab keeps its file.
- `saveAs: false` (FigCraft's File › Save, under the opened file's name whichever tab): the tab in front **rewrites its own file** when it has one, otherwise the OS dialog suggests the tab's name. Never another tab's file.
- `saveAs: true`, or Ctrl+Shift+S: always the dialog. After the write the tab takes the file's name (`file.rename`, then `file.markSaved`).
- A file opened in another format (`.fig`, `.sketch`, `.penpot`, an image) is only the tab's **source**: FigCraft saves `.figcraft`, so the first save asks where, in the source's folder.
- A failed or cancelled write leaves the tab "unsaved" (title with `*`) and the next save asks again.
- Close: each modified tab is brought to the front (`doc.switch`) and saved by FigCraft's File › Save (`ui.menu.invoke`); the window closes only when everything is written.

Findings:

- **`ui.menu.invoke`** runs FigCraft's menu items as a click does (Save, Save as, Open, frames PDF); `engine.execute` takes a `document` (the `id` from `doc.list`) to act on a tab without showing it (`file.close`, `file.rename`).
- **Storage**: FigCraft keeps in `localStorage` only the user's name and, when the page stops on an error, the recovery copies. The module puts them in `~/.config/ca.kazo.fig/web-storage.json`: the same user's next window opens the lost designs again.
- **Known limit**: File › Save as **clicked in the menu** on the tab whose name is the opened file's arrives as a Save (`saveAs: false`) and rewrites that file. Ctrl+Shift+S does ask for a new file. The upstream issue (prepared for Benoit) covers both cases.

Tests: 96 xUnit tests (including formats, tabs, keys, manifest) and 5 new Playwright journeys in the OS: a new design saved, rewritten in place and reopened by `xdg-open`; a second tab never overwrites the opened file and each file keeps its design; Ctrl+Shift+S and a PDF copy; the close guard (Cancel, Yes then Cancel, Yes); FigCraft's File › Open through the OS dialog, in the window left blank.

## Upstream track

Texts prepared by Claude in English, published by Benoit (no agent mention, `storytold` repository
rules):

- FigCraft: one "host file" per document, not per page (Q1);
- `craft-libs`: join #611 (save confirmation) and #610 (`open-request`).

## Progress

- [x] Step 0: reading FigCraft 0.18.1 and the official zip.
- [x] Plan fr and en.
- [x] Benoit's answers to Q1 and Q2 (A and A).
- [x] Work item 1 (`fig/1-solution`): solution, manifest, 0.18.1 zip, start-up window on the module, CI on `main`.
- [x] Work item 2 (`fig/2-window`): formats, tabs bound to their file, save, Save as, copies, close, picker, per-user storage.
- [ ] Work item 3.
- [ ] Work item 4.
