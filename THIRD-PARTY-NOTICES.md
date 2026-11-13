# Third-party notices

> [Version française](THIRD-PARTY-NOTICES-fr.md)

Kazo Fig is under the MIT license ([LICENSE](LICENSE)). Its C# code, its iframe page
(`src/Kazo.Fig/wwwroot/host/`) and its icon (`src/Kazo.Fig/wwwroot/icons/app.svg`,
`store/icon.png`) are this repository's own work. The iframe proxy it uses (`craft-proxy.js`)
belongs to Kazo OS (`Kazo.Os.AppSdk.Craft`, MIT).

It bundles one third-party work, **FigCraft**, which brings its own third-party material.

## FigCraft

| | |
| --- | --- |
| Work | [FigCraft](https://github.com/storytold/figcraft), an interface design tool written in Rust, by the ArtCraft team and the FigCraft contributors |
| Version | 0.18.1, compiled from our fork [`Kazo-ca/figcraft`](https://github.com/Kazo-ca/figcraft) (branch `kazo/v0.18.1`, the upstream tag, unmodified) |
| License | MIT or Apache-2.0, at the user's option |
| Files | `src/Kazo.Fig/wwwroot/figcraft/` (the web build: `.js`, `.wasm`, `snippets/`) |
| Provenance | `src/Kazo.Fig/wwwroot/figcraft/figcraft.json`: the fork's commit, the craft-fonts revision, the compilers, and the SHA-256 of every file |

Kazo Fig does not change FigCraft: it shows its web build in an iframe and talks to it through
the control commands and events FigCraft publishes for a host page.

FigCraft's own notices ship **unmodified** next to its build, and in the installed package
(`_content/Kazo.Fig/figcraft/`):

| File | Content |
| --- | --- |
| `LICENSE-MIT`, `LICENSE-APACHE` | FigCraft's licenses |
| `NOTICE` | FigCraft's copyright, its trademark notice, and the third-party material it bundles (among others the egui icon font subset of emoji-icon-font under the MIT license), each with its license text |
| `OFL-*.txt` (29 files) | The SIL Open Font License 1.1 of each font FigCraft's web build embeds from [`storytold/craft-fonts`](https://github.com/storytold/craft-fonts) (Inter, Roboto, Open Sans, Noto Sans CJK SC, Liberation and others) |

### The ArtCraft brand

The ArtCraft name, wordmark and logos are trademarks of the ArtCraft team; FigCraft's open-source
license does not cover them. Kazo Fig uses none of them: its name, its icon and its Store listing
are this repository's own. "Based on FigCraft by the ArtCraft team" only says where the software
comes from.

## In the package

The Kazo OS package (`.kazoapp`) carries, under `wwwroot/`:

- `legal/LICENSE.txt`: Kazo Fig's MIT license (this repository's `LICENSE`);
- `legal/THIRD-PARTY-NOTICES.md` and `legal/THIRD-PARTY-NOTICES-fr.md`: this file and its French
  version;
- `figcraft/LICENSE-MIT`, `LICENSE-APACHE`, `NOTICE`, `OFL-*.txt`: FigCraft's notices, as above.

A C# test (`LegalFilesTests`) checks that the project puts these files in the package.

## Test data

The end-to-end tests make their own designs with Kazo Fig; they ship no third-party file. The
Store screenshots (`store/screenshots/`) show FigCraft's own starter templates (`mobile-app`,
`dashboard`), made by the FigCraft contributors under FigCraft's license.
