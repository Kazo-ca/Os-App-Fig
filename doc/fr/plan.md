> [English version](../en/plan.md)

# Plan : l'app Kazo Fig pour Kazo OS

Étape 0 du [guide de portage des apps Craft](https://github.com/Kazo-ca/Os/blob/main/doc/fr/wasm/dev/craft-app-port-guide.md)
de Kazo OS, écrite le 2026-11-13 (issue `TODO` [#1](https://github.com/Kazo-ca/Os-App-Fig/issues/1)).
Ce document fixe le but, ce que la lecture de FigCraft a montré, les décisions et l'ordre des
chantiers. La méthode vient de Kazo Word ([`Kazo-ca/Os-App-Word`](https://github.com/Kazo-ca/Os-App-Word))
et surtout de Kazo Spreadsheets ([`Kazo-ca/Os-App-Spreadsheets`](https://github.com/Kazo-ca/Os-App-Spreadsheets),
[plan](https://github.com/Kazo-ca/Os-App-Spreadsheets/blob/spreadsheets/base/doc/fr/plan.md)), la
première app sur le module commun.

## But

Kazo Fig est l'**outil de design d'interfaces de Kazo OS**. Il emballe le build web de
[FigCraft](https://github.com/storytold/figcraft) (équipe ArtCraft, `MIT OR Apache-2.0`), un outil
de design écrit en Rust (egui, wgpu : WebGPU avec repli WebGL 2), en alpha, et branche son mode
hébergé (`craft-web-host`) sur les fichiers, les dialogues et les associations de Kazo OS. Rien de
FigCraft n'est réécrit.

Le code Kazo commun aux apps Craft (iframe, proxy, sélecteur de fichiers, raccourcis, réglages,
`DocumentSession`, règle d'enregistrement) vient du module `Kazo.Os.AppSdk.Craft` livré par Kazo OS
([ADR 0259](https://github.com/Kazo-ca/Os/blob/main/doc/fr/wasm/dev/adr/0259-shared-craft-app-host-module.md),
guide [`craft-apps.md`](https://github.com/Kazo-ca/Os/blob/main/doc/fr/wasm/framework/craft-apps.md)).
Kazo Fig garde seulement sa fenêtre, sa liste de formats, sa page d'iframe, le build de FigCraft et
son manifeste.

## Lecture de FigCraft (étape 0, 2026-11-13)

Lue sur le tag amont `v0.18.1` (commit `4acc293`, branche `release`, 2026-11-11 ; le `main` du fork
[`Kazo-ca/figcraft`](https://github.com/Kazo-ca/figcraft) est à `7b34dba` et ne contient pas ce
commit) et sur le zip officiel `figcraft-web-0.18.1.zip`.

| Point | Constat |
| --- | --- |
| Build web | `apps/figcraft-web` (Trunk, `public_url = "./"`), dépend de `craft-web-host` et `craft-web-api` à la révision `craft-libs` `fd5e2fe` (contrat hôte version `"1"`, comme WordCraft et GridCraft). Rust 1.95 au minimum. |
| Zip officiel | `https://github.com/storytold/figcraft/releases/download/v0.18.1/figcraft-web-0.18.1.zip`, SHA-256 `2fbeb93f5022b0a7cc9d8528f1ed7e2b54c94d213ef79e65128aa898c399f02b`, 10,5 Mo, 40 fichiers. Contenu : `index.html`, glue `figcraft-web-<hash>.js` (188 Ko), `figcraft-web-<hash>_bg.wasm` (**25,5 Mo**), licences, 25 textes `OFL-*.txt` (polices embarquées dans le `.wasm`), `HOSTING.md`. |
| Taille | Bien sous les limites de Kazo OS (128 Mio par fichier, paquet de 64 Mio compressé). L'amont bloque lui-même le `.wasm` sous 25 Mio (limite Cloudflare). |
| Canevas | `figcraft_canvas`, codé en dur (une instance par document HTML : un iframe par fenêtre). Écran de chargement `figcraft_loading`. |
| Mode hébergé | `data-host="page"` sur le canevas : `CustomEvent` `figcraft:*`. `ready` (version de l'app, `file: false` : FigCraft lit des octets), `open { name, bytes }` (refusé au-delà de 512 Mo ; sans nom : `Untitled.figcraft`), `save { name, saveAs, bytes }`, `dirty`, `failed`. `?author=` signe les commentaires. En mode hébergé, l'app démarre **sur son écran d'accueil**, sans document. |
| `save` et `saveAs` | Contrairement à GridCraft, **FigCraft calcule `saveAs` lui-même** (`hostmode.rs`) : `true` pour un fichier de document (`.figcraft`, `.json`) sous un autre nom que celui que l'hôte a ouvert (Enregistrer sous, ou document jamais envoyé par l'hôte), `false` pour un Enregistrer du fichier ouvert et pour toutes les copies (PNG, JPG, SVG, PDF, `.fig`, variables). Pas d'`id`, pas de confirmation (craft-libs #611). |
| Plusieurs documents | La page tient plusieurs onglets (Fichier › Nouveau, accueil). Mais le pont ne retient **qu'un seul** « fichier de l'hôte » pour toute la page, et en mode hébergé l'Enregistrer de **n'importe quel onglet** sans chemin part sous ce nom avec `saveAs: false` (`app/files.rs`, `save`). Lu dans le code : un nouvel onglet enregistré écraserait le fichier ouvert dans le premier (question Q1, à confirmer par un parcours Playwright au chantier 2). |
| `dirty` | Vrai quand **un** des onglets a des changements (comme GridCraft). |
| Commandes (`window.figcraft`) | `request`, `execute`, `commands`, `docInspect`, `uiInspect`, `screenshot` (`craft-web-api`) et `openFile`, `captureDownloads`, `files`, `readFile`, `inspect`. **Installé seulement avec `?control` dans l'adresse** : la fenêtre passe `control` dans les `parameters` de `CraftFrameOptions`. |
| Liste des documents | `ui.inspect` n'a ni `documents` ni `windows` (il a `tabs[{ name }]` sans état modifié) : troisième forme. La commande `doc.list` donne `[{ index, id, name, dirty, active }]` : la fenêtre la met sous la forme `documents[{ id, title, dirty, active }]` que lit `OpenDocument.ListFrom`. |
| Thème, langue | Thème : `ui.set { theme: "dark" \| "light" \| "system" }`. Langue : commande `pref.language { language }`, 25 catalogues (`fr`, `pt-BR`, `zh-Hans`, `zh-Hant`, `es-419`…). `file.new`, `file.save`, `file.saveAs`, `file.rename`, `file.close`, `doc.home` existent. |
| Sélecteur du navigateur | Le même `input type=file` que WordCraft et GridCraft (le module le remplace) : Fichier › Ouvrir (`.figcraft .fig .jam .penpot .sketch .json .svg .png .jpg .jpeg .webp .gif .tif .tiff`), Importer, Placer une image (images), Ajouter une bibliothèque (`.figcraft`), importer un plugin ou des variables (`.json`). |
| Formats | Ouverts : la liste ci-dessus (`.fig`, `.sketch`, `.penpot`, `.jam`, SVG et images sont importés dans un nouveau document). Document enregistré : `.figcraft` (ou `.json`). Copies : PNG, JPG, SVG, PDF, `.fig`, variables. Un `.fig` ouvert puis enregistré part en `.figcraft` avec `saveAs: true` (dialogue de l'OS). |
| Réglages | `localStorage` : le nom de l'utilisateur, et des **copies de récupération** des fichiers non enregistrés quand la page s'arrête sur une erreur interne (peut peser plusieurs Mo : à surveiller avec le fichier de réglages par utilisateur du module, chantier 2). |
| Réseau | Sessions en direct et partage en ligne par WebSocket (`ws://`, `wss://`) vers l'app de bureau ou un `figcraft-server` : partent directement du navigateur. Limite connue, rien à faire pour la v1. |
| Polices | Polices de `craft-fonts` embarquées dans le `.wasm` (la release web les exige) ; « polices installées sur cet ordinateur » demande la permission Local Font Access du navigateur (limite connue, comme Kazo Word). |
| Marque | Le nom et le logo ArtCraft sont des marques. Un build **modifié** les retire ; la fiche du Store dit « basé sur FigCraft de l'équipe ArtCraft », sans logo, et ne nomme pas d'autre produit de design. |

## Décisions

| # | Sujet | Décision |
| --- | --- | --- |
| 1 | Nom et identité | « **Kazo Fig** » (Benoit, 2026-11-13). Id `ca.kazo.fig`, éditeur `pub.kazo`, assemblage `Kazo.Fig`, catégorie `graphics`, plusieurs fenêtres. |
| 2 | Code commun | Le module `Kazo.Os.AppSdk.Craft` (ADR 0259), dans `main` de Kazo OS depuis Os#491. Version minimale : **Kazo OS 3.3.3** (module, `canChangeScreen` de l'ADR 0263, `?autolaunch`). Tant que le tag `v3.3.3` n'existe pas, le sous-module de l'OS suit `main` et la CI compile l'OS depuis ses sources, comme Kazo Spreadsheets. |
| 3 | Intégration | Un iframe par fenêtre, page `wwwroot/host/index.html` avec le canevas `figcraft_canvas` `data-host="page"` `data-craft-app="figcraft"`, et `control` dans les paramètres de l'adresse. |
| 4 | Changer d'écran | `canChangeScreen: false` dans le bloc `window` du manifeste dès le chantier 1 (issue #2). |
| 5 | Provenance du build | Comme Kazo Spreadsheets : d'abord le **zip officiel 0.18.1** épinglé par SHA-256, dans le dépôt sous `wwwroot/figcraft/` avec `figcraft.json` ; puis compilé depuis le fork (chantier 3, branche `kazo/v0.18.1` = `4acc293`, avec `craft-fonts`). |
| 6 | Enregistrer sous | Rien à contourner : FigCraft dit `saveAs` lui-même, et son Ctrl+Maj+S passe par son propre Enregistrer sous. Pas de touche réservée en plus de celles de l'OS et de Ctrl+N. |

## Questions posées à Benoit

**Q1. Plusieurs onglets dans une fenêtre.** Si l'utilisateur crée un nouveau design dans une fenêtre
qui a déjà un fichier ouvert, FigCraft envoie l'Enregistrer de ce nouvel onglet sous le nom du
fichier ouvert, sans `saveAs`. La règle du module récrirait ce fichier : perte de données.

- **A (recommandé)** : garde dans la fenêtre Kazo et correctif en amont. La fenêtre lie chaque
  fichier à l'**onglet** (`id` de `doc.list`) et non au nom : à chaque `save`, elle demande
  l'onglet actif ; si ce n'est pas celui qui est lié au fichier, c'est un Enregistrer sous
  (dialogue de l'OS). Claude prépare l'issue et la PR amont (un « fichier de l'hôte » par document),
  publiées par Benoit.
- B : corriger seulement dans notre fork (build modifié dès la v1, marques ArtCraft retirées), et
  proposer la PR en amont.
- C : un seul document par fenêtre (fermer les onglets en trop) : contre l'usage de FigCraft, et
  rien n'empêche de cliquer « Nouveau » dans l'app.

**Q2. Formats associés à Kazo Fig.**

- **A (recommandé)** : app par défaut pour `.figcraft .fig .sketch .penpot .jam` (aucune autre app
  de Kazo OS ne les lit) ; « Ouvrir avec » seulement pour `.svg`, `.json` et les images (`.png .jpg
  .jpeg .webp .gif .tif .tiff`), dont l'app par défaut reste celle de l'OS.
- B : app par défaut seulement pour `.figcraft` ; tout le reste en « Ouvrir avec ».

## Chantiers

Série `fig/base`, une branche `fig/<n>-<sujet>` et une PR vers `fig/base` par chantier. Comme pour
Kazo Spreadsheets, les chantiers 2 et 3 du guide (ouvrir et enregistrer, reste du cadre) sont
réunis : le module en fait l'essentiel. Benoit fusionne `fig/base` dans `main`.

| N° | Branche | Contenu | Dépend de |
| --- | --- | --- | --- |
| 0 | `fig/0-plan` | ce plan (fr et en) | — |
| 1 | `fig/1-solution` | solution du modèle renommée (`Kazo.Fig`, `.Os`, `.Tests`), manifeste (`canChangeScreen: false`), `tools/vendor-figcraft.sh` (zip 0.18.1 épinglé) et `figcraft.json`, page d'iframe, tests de manifeste et de provenance, sous-module de l'OS sur `main` (3.3.3) | plan fusionné |
| 2 | `fig/2-window` | fenêtre sur le module : formats (Q2), ouvrir, enregistrer, garde des onglets (Q1), fermeture, sélecteur, raccourcis, thème, langue, auteur, démarrage sur un document vide ; deux fenêtres ; WebGPU et WebGL 2 ; Playwright | chantier 1, réponses Q1 et Q2 |
| 3 | `fig/3-build-from-source` | fork `Kazo-ca/figcraft` (branche `kazo/v0.18.1`) en sous-module, `tools/build-figcraft.sh` (avec `craft-fonts` à la révision de la release), workflow `build-figcraft.yml`, test du commit | chantier 1 ; en parallèle du 2 |
| 4 | `fig/4-store` | icône sans logo ArtCraft, fiche fr et en, README refait depuis la fiche (script `store:readme`, lien « Essayer » `?autolaunch=ca.kazo.fig`), `THIRD-PARTY-NOTICES.md`, sous-module sur `v3.3.3`, installation dans un Kazo OS publié, tag, catalogue, `features.md` | chantiers 2 et 3, sortie de Kazo OS 3.3.3 |

**Terminé quand** : Kazo Fig s'installe depuis le Store d'un Kazo OS publié, ouvre un `.figcraft`
par double-clic, l'enregistre, et deux fenêtres tournent ensemble.

## Piste amont

Textes préparés par Claude en anglais, publiés par Benoit (aucune mention d'agent, règles des
dépôts `storytold`) :

- FigCraft : un « fichier de l'hôte » par document, pas par page (Q1) ;
- `craft-libs` : se joindre à #611 (confirmation d'enregistrement) et #610 (`open-request`).

## Avancement

- [x] Étape 0 : lecture de FigCraft 0.18.1 et du zip officiel.
- [x] Plan fr et en.
- [ ] Réponses de Benoit à Q1 et Q2.
- [ ] Chantier 1.
- [ ] Chantier 2.
- [ ] Chantier 3.
- [ ] Chantier 4.
