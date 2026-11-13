# Kazo Fig

Un outil de design d'interfaces et de prototypage pour Kazo OS. Kazo Fig est basé sur
[FigCraft](https://github.com/storytold/figcraft) de l'équipe ArtCraft, un outil de design libre
écrit en Rust, qui tourne ici dans une fenêtre de Kazo OS.

## Concevoir

- Cadres, mise en page automatique (lignes, colonnes, retour à la ligne et grille), contraintes et
  grilles de mise en page.
- Composants, instances avec leurs remplacements, variantes, styles locaux et variables avec modes.
- Réseaux vectoriels avec les outils Plume et Crayon, groupes booléens, dégradés, images en
  remplissage, ombres, flous et tous les modes de fusion.
- Texte modifié sur place dans le canevas, avec les polices libres incluses dans FigCraft.
- Prototypes : liens, transitions, une vue de présentation, et le code du calque choisi.
- Plusieurs designs dans des onglets, et plusieurs fenêtres à la fois, chacune avec ses designs.

## Vos fichiers Kazo OS

- Double-cliquez sur un fichier `.figcraft`, `.fig`, `.sketch`, `.penpot` ou `.jam` dans
  l'Explorateur de fichiers, ou utilisez « Ouvrir avec » sur un fichier `.json`, `.svg` ou une
  image : le design s'ouvre dans Kazo Fig.
- Enregistrer écrit du `.figcraft`. Chaque onglet est lié à son propre fichier : un `.figcraft`
  ouvert est réécrit en place, et enregistrer un autre onglet ne l'écrase jamais. Un fichier d'un
  autre format n'est jamais écrasé, donc son premier enregistrement demande un nom dans le dialogue
  d'enregistrement de Kazo OS. Enregistrer sous (Ctrl+Maj+S) et le premier enregistrement d'un
  nouveau design passent aussi par ce dialogue.
- Les exportations en PNG, JPG, WebP, SVG et PDF sont des copies : le design reste lié à son
  fichier.
- Fermer la fenêtre demande avant de perdre des modifications, pour chaque onglet ouvert.
- Fichier › Ouvrir et les images placées viennent des fichiers Kazo OS, par le dialogue de Kazo OS.
- Kazo Fig suit le thème (clair ou sombre) et la langue de l'OS ; ses réglages sont gardés par
  utilisateur Kazo OS dans `~/.config/ca.kazo.fig/`.

## Limites connues

- **FigCraft est en alpha.** Gardez une copie des designs importants.
- L'élément Fichier › Enregistrer sous du menu de FigCraft, sur l'onglet qui porte le nom du
  fichier ouvert, enregistre sous ce nom : utilisez Ctrl+Maj+S pour Enregistrer sous.
- FigCraft dessine avec **WebGPU ou WebGL 2** : un navigateur ou un ordinateur sans l'un ni
  l'autre montre un écran d'erreur au lieu du design.
- Le téléchargement fait environ 11 Mo (26 Mo une fois installé), et chaque fenêtre charge sa
  propre copie de FigCraft.
- La fenêtre reste sur l'écran où elle s'est ouverte : la passer sur un autre écran rechargerait
  FigCraft et perdrait les designs ouverts.
- Demande Kazo OS 3.3.3 ou plus récent.

## Licence

Kazo Fig est un logiciel libre sous licence MIT. FigCraft est sous licence MIT ou Apache-2.0 ; ses
avis et les licences des polices qu'il contient sont livrés avec l'app (voir
`THIRD-PARTY-NOTICES-fr.md` dans le dépôt). Le nom et les logos ArtCraft sont des marques de
l'équipe ArtCraft et ne sont pas utilisés par Kazo Fig.
