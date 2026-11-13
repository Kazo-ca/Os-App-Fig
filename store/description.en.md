# Kazo Fig

An interface design and prototyping tool for Kazo OS. Kazo Fig is based on
[FigCraft](https://github.com/storytold/figcraft) by the ArtCraft team, an open-source design tool
written in Rust, which runs here in a Kazo OS window.

## Design

- Frames, auto layout (rows, columns, wrap and grid), constraints and layout grids.
- Components, instances with overrides, variants, local styles and variables with modes.
- Vector networks with the Pen and Pencil tools, boolean groups, gradients, image fills, shadows,
  blurs and every blend mode.
- Text edited in place on the canvas, with the open fonts FigCraft includes.
- Prototypes: connections, transitions, a presentation view, and code for the selected layer.
- Several designs in tabs, and several windows at once, each with its own designs.

## Your Kazo OS files

- Double-click a `.figcraft`, `.fig`, `.sketch`, `.penpot` or `.jam` file in the File Explorer, or
  use "Open with" on a `.json`, `.svg` or image file: the design opens in Kazo Fig.
- Save writes `.figcraft`. Each tab is bound to its own file: a `.figcraft` you opened is rewritten
  in place, and saving another tab never overwrites it. A file of another format is never
  overwritten, so its first save asks for a name in the Kazo OS save dialog. Save As
  (Ctrl+Shift+S) and the first save of a new design use that dialog too.
- Exports to PNG, JPG, WebP, SVG and PDF are copies: the design stays bound to its file.
- Closing the window asks before losing unsaved changes, for every open tab.
- File › Open and the pictures you place come from Kazo OS files, through the Kazo OS dialog.
- Kazo Fig follows the OS theme (light or dark) and language; its settings are kept per Kazo OS
  user in `~/.config/ca.kazo.fig/`.

## Known limits

- **FigCraft is in early alpha.** Keep a copy of the designs that matter.
- FigCraft's own File › Save As menu item, on the tab that carries the opened file's name, saves
  under that name: use Ctrl+Shift+S for Save As.
- FigCraft draws with **WebGPU or WebGL 2**: a browser or computer with neither shows an error
  screen instead of the design.
- The download is about 11 MB (26 MB once installed), and each window loads its own copy of
  FigCraft.
- The window stays on the screen where it opened: moving it to another screen would reload
  FigCraft and lose the open designs.
- Needs Kazo OS 3.3.3 or later.

## License

Kazo Fig is free software under the MIT license. FigCraft is under the MIT or Apache-2.0 license;
its notices and the licenses of the fonts it includes ship with the app (see
`THIRD-PARTY-NOTICES.md` in the repository). The ArtCraft name and logos are trademarks of the
ArtCraft team and are not used by Kazo Fig.
