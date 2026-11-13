# Avis de tiers

> [English version](THIRD-PARTY-NOTICES.md)

Kazo Fig est sous licence MIT ([LICENSE](LICENSE)). Son code C#, sa page d'iframe
(`src/Kazo.Fig/wwwroot/host/`) et son icône (`src/Kazo.Fig/wwwroot/icons/app.svg`,
`store/icon.png`) sont le travail de ce dépôt. Le proxy d'iframe qu'il utilise (`craft-proxy.js`)
appartient à Kazo OS (`Kazo.Os.AppSdk.Craft`, MIT).

Il embarque une seule œuvre de tiers, **FigCraft**, qui apporte ses propres éléments de tiers.

## FigCraft

| | |
| --- | --- |
| Œuvre | [FigCraft](https://github.com/storytold/figcraft), un outil de design d'interfaces écrit en Rust, de l'équipe ArtCraft et des contributeurs de FigCraft |
| Version | 0.18.1, compilée depuis notre fork [`Kazo-ca/figcraft`](https://github.com/Kazo-ca/figcraft) (branche `kazo/v0.18.1`, l'étiquette amont, sans modification) |
| Licence | MIT ou Apache-2.0, au choix de l'utilisateur |
| Fichiers | `src/Kazo.Fig/wwwroot/figcraft/` (la page web compilée : `.js`, `.wasm`, `snippets/`) |
| Provenance | `src/Kazo.Fig/wwwroot/figcraft/figcraft.json` : le commit du fork, la révision de craft-fonts, les compilateurs et le SHA-256 de chaque fichier |

Kazo Fig ne modifie pas FigCraft : il affiche sa page web dans un iframe et lui parle par les
commandes et événements de contrôle que FigCraft publie pour une page hôte.

Les avis de FigCraft sont livrés **sans modification** à côté de sa compilation, et dans le paquet
installé (`_content/Kazo.Fig/figcraft/`) :

| Fichier | Contenu |
| --- | --- |
| `LICENSE-MIT`, `LICENSE-APACHE` | Les licences de FigCraft |
| `NOTICE` | Le copyright de FigCraft, son avis sur les marques et les éléments de tiers qu'il embarque (entre autres le sous-ensemble d'emoji-icon-font de la police d'icônes d'egui, sous licence MIT), chacun avec le texte de sa licence |
| `OFL-*.txt` (29 fichiers) | La SIL Open Font License 1.1 de chaque police que la page web de FigCraft embarque depuis [`storytold/craft-fonts`](https://github.com/storytold/craft-fonts) (Inter, Roboto, Open Sans, Noto Sans CJK SC, Liberation et d'autres) |

### La marque ArtCraft

Le nom, le logotype et les logos ArtCraft sont des marques de l'équipe ArtCraft ; la licence libre
de FigCraft ne les couvre pas. Kazo Fig n'en utilise aucun : son nom, son icône et sa fiche du
Store sont ceux de ce dépôt. « Basé sur FigCraft de l'équipe ArtCraft » dit seulement d'où vient
le logiciel.

## Dans le paquet

Le paquet Kazo OS (`.kazoapp`) contient, sous `wwwroot/` :

- `legal/LICENSE.txt` : la licence MIT de Kazo Fig (le `LICENSE` de ce dépôt) ;
- `legal/THIRD-PARTY-NOTICES.md` et `legal/THIRD-PARTY-NOTICES-fr.md` : ce fichier en anglais et
  en français ;
- `figcraft/LICENSE-MIT`, `LICENSE-APACHE`, `NOTICE`, `OFL-*.txt` : les avis de FigCraft, comme
  ci-dessus.

Un test C# (`LegalFilesTests`) vérifie que le projet met ces fichiers dans le paquet.

## Données de test

Les tests de bout en bout créent leurs propres designs avec Kazo Fig ; ils n'embarquent aucun
fichier de tiers. Les captures du Store (`store/screenshots/`) montrent les modèles de départ de
FigCraft (`mobile-app`, `dashboard`), faits par les contributeurs de FigCraft sous sa licence.
