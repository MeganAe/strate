# Strate — application de Material Design 3

Recherche appliquée, pas un thème « qui ressemble à Material ». Les valeurs viennent de la spécification M3 et de Material Color Utilities (schéma tonal spot, spec 2021, contraste standard).

## Couleur

Couleur source : `#1E4D5C` (encre de lac, hue HCT 223, chroma 26, tone 30).

Le schéma n’est pas la palette violette par défaut de Material, ni un dégradé. Les rôles sont ceux de [m3.material.io/styles/color/roles](https://m3.material.io/styles/color/roles) :

| Rôle | Clair | Sombre | Usage dans Strate |
| --- | --- | --- | --- |
| primary / on-primary | `#06677F` / `#FFFFFF` | `#88D1EB` / `#003543` | Bouton rempli, barre de progression, signe |
| primary-container | `#B6EAFF` | `#004E60` | Réservé, non utilisé comme texte |
| secondary-container / on-secondary-container | `#CFE6F1` / `#344A52` | `#344A52` / `#CFE6F1` | Indicateur de navigation, puce sélectionnée |
| tertiary | `#5A5C7E` | `#C3C3EB` | Accent secondaire, pas un avertissement |
| error / on-error | `#BA1A1A` / `#FFFFFF` | `#FFB4AB` / `#690005` | Disque ≥ 90 %, confirmation destructive, fermer |
| surface | `#F5FAFD` | `#0F1416` | Fond de fenêtre et barre d’application |
| surface-container-low | `#EFF4F7` | `#171C1F` | Tiroir de navigation |
| surface-container-high | `#E4E9EC` | `#252B2D` | Dialogue |
| surface-container-highest | `#DEE3E6` | `#303638` | Carte remplie, piste de progression, champ rempli |
| on-surface / on-surface-variant | `#171C1F` / `#40484C` | `#DEE3E6` / `#BFC8CC` | Texte, texte secondaire |
| outline / outline-variant | `#70787C` / `#BFC8CC` | `#8A9296` / `#40484C` | Puce, carte contour, séparateur |
| inverse-surface | `#2C3134` | `#DEE3E6` | Snackbar |

Les couches d’état suivent la spec : 8 % (`#14`) au survol, 12 % (`#1F`) à l’appui, sur `on-surface`. Le contenu désactivé est à 38 % d’opacité.

Les contrôles MaterialDesignThemes reçoivent les mêmes primary / secondary / surface via `Theme.Create` et `PaletteHelper`, pour que boutons, cases, champs et barres de progression restent dans le schéma.

## Typographie

Échelle M3, telle qu’implémentée par `MaterialDesign3.TextBlock.xaml` :

| Style | Taille / interligne | Graisse | Usage |
| --- | --- | --- | --- |
| headline-small | 24 / 32 | Regular | Titre de dialogue |
| headline-medium | 28 / 36 | Regular | Chiffre du tableau de bord |
| title-large | 22 / 28 | Medium | Titre d’écran, dans la barre de 64 |
| title-medium | 16 / 24 | Medium | Titre de carte |
| title-small | 14 / 20 | Medium | En-tête de section de contenu |
| body-large | 16 / 24 | Regular | Ligne de liste principale |
| body-medium | 14 / 20 | Regular | Sous-titre d’écran |
| body-small | 12 / 16 | Regular | Description de cible |
| label-large | 14 / 20 | Medium | Élément de navigation, puce |
| label-small | 11 / 16 | Medium | Libellé de section du tiroir |

La police est **Noto**, fournie par MaterialDesignThemes pour Material Design 3 (`MaterialDesign3.Font.xaml`), pas Roboto. C’est le choix du toolkit M3, pas une substitution.

## Forme, espacement, élévation

- Grille de 4.
- Forme extra-small 4 : snackbar.
- Forme small 8 : puces de filtre.
- Forme medium 12 : cartes.
- Forme extra-large 28 : dialogue.
- Forme pleine : indicateur de navigation (rayon 28 sur 56 de haut), boutons du toolkit M3.
- Espacement de page : 24.
- Élévation 0 sur les cartes remplies. Pas d’ombre portée décorative : en WPF elle brouille le texte. La séparation vient du rôle de surface, comme le prévoit la carte remplie M3.

## Composants

Bibliothèque [MaterialDesignInXAML 5.3.2](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit), dictionnaire `MaterialDesign3.Defaults.xaml` :

- Bouton rempli (implicite `MaterialDesignRaisedButton`), contourné, texte, icône
- Champ rempli et champ contourné
- Case à cocher, liste déroulante, grille de données
- Barre de progression linéaire, 4 dp
- `DialogHost`, snackbar
- `PackIcon` (Material Design Icons)
- Fenêtre M3, police Noto

Composants dessinés sur la spec, parce que le style de tiroir du toolkit ne pose pas un indicateur `secondary-container` opaque :

- Tiroir permanent, largeur **280** (maximum du composant NavigationView ; la planche M3 dit 360, trop large pour une grille de fichiers sur un portable)
- Élément : hauteur 56, retrait horizontal 12, icône 24, écart 12, indicateur pilule
- Barre d’application petite : 64, surface, élévation 0, titre title-large. La progression indéterminée se pose en bas de barre, sans décaler la page
- Puce de filtre : 32 de haut, rayon 8, coche en sélection
- Dialogue : rayon 28, `surface-container-high`, boutons texte, action destructive en `error`

## Adaptations desktop, assumées

- Pas de FAB. L’action principale est un bouton rempli dans la rangée d’actions. Le FAB M3 couvre les grilles.
- Chrome de fenêtre custom, pour que la barre M3 soit la barre de titre. `WM_GETMINMAXINFO` limite l’agrandissement à la zone de travail. Coins DWM arrondis, mode sombre immersif.
- Les tailles sont en octets binaires (1 Ko = 1024), comme l’Explorateur, pas en octets décimaux.
