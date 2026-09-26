# Strate

Voyez ce qui pèse. Gardez ce qui compte.

Application Windows (.NET 10, WPF) pour mesurer un disque, trouver les gros fichiers et les doublons, nettoyer des cibles connues, et lire le démarrage ainsi que l’état des disques. Rien n’est supprimé sans confirmation.

L’interface suit Material Design 3 : schéma HCT tonal spot, rôles de couleur, échelle typographique Noto, composants de [MaterialDesignInXAML](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) 5.3.2. Le détail est dans [docs/material.md](docs/material.md). L’identité est dans [docs/identite.html](docs/identite.html).

## Prérequis

- Windows 10 ou 11, 64 bits
- [SDK .NET 10](https://dotnet.microsoft.com/download) (le runtime seul ne suffit pas pour compiler)

Aucune installation de Visual Studio n’est obligatoire. Le SDK en ligne de commande suffit.

## Lancer

Dans le dossier du projet :

```powershell
dotnet run --project src\Strate\Strate.csproj
```

La première exécution télécharge les paquets NuGet (MaterialDesignThemes, CommunityToolkit.Mvvm, System.Management). Il faut une connexion.

## Produire l’exécutable sur la machine

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

Le résultat est `artifacts\Strate\Strate.exe`. Le dossier est autonome : le runtime .NET est inclus. On lance `Strate.exe`, pas un installeur.

## Produire l’exécutable sur GitHub

Le workflow est manuel. Il ne part pas à chaque commit.

1. Pousser ce dépôt sur GitHub, avec `.github/workflows/compiler.yml` sur la branche par défaut.
2. Onglet **Actions**.
3. **Compiler l'exécutable** → **Run workflow**.
4. Laisser `win-x64` et `single_file = false`, puis lancer.
5. Ouvrir l’exécution terminée, télécharger l’artefact, décompresser, lancer `Strate.exe`.

`single_file = false` produit un dossier publié, emballé en zip. C’est le mode fiable pour WPF. `single_file = true` produit un seul `.exe` plus lourd au premier lancement ; si Windows ou l’antivirus le bloque, relancer le workflow avec `false`.

Le fichier du workflow doit être sur la branche par défaut, sinon GitHub ne le propose pas dans Actions.

## Ce que fait le logiciel

- **Tableau de bord** — volumes fixes et amovibles, part utilisée, estimation des fichiers temporaires et de la corbeille.
- **Analyse** — taille des éléments d’un dossier, descente d’un niveau à la fois. Les liens symboliques sont ignorés.
- **Gros fichiers** — au-dessus de 50 Mo, 100 Mo, 500 Mo ou 1 Go. Envoi à la corbeille sur confirmation.
- **Doublons** — regroupement par taille puis empreinte SHA-256. Une copie est marquée à conserver (la plus récente). Rien n’est coché automatiquement. Un groupe ne peut pas être entièrement supprimé.
- **Nettoyage** — cibles nommées seulement : Temp, corbeille, caches de navigateurs, miniatures, WER, caches NuGet / npm / pip. Pas de balayage de `Windows` ni de `Program Files`. Délai de grâce de 15 minutes sur les fichiers temporaires.
- **Démarrage** — clés Run et dossiers Démarrage. Désactivation du compte utilisateur par déplacement, pas par suppression. Les entrées machine sont en lecture seule.
- **Disques** — `Win32_DiskDrive` et SMART si le pilote l’expose.
- **Rapport** — HTML dans `Documents\Strate\Rapports`.
- **Réglages** — thème clair, sombre ou système.

Les réglages et le journal sont dans `%AppData%\Strate`.

## Sécurité

Strate ne demande pas l’administrateur. Une cible inaccessible est ignorée, pas forcée. Les suppressions de documents passent par la corbeille. Les caches, eux, sont définitifs : ils se reconstruisent. La corbeille n’est vidée que si sa case est cochée et confirmée.

Ce n’est pas un outil de réparation disque, ni un antivirus. L’état SMART « indisponible » signifie que Windows ne l’a pas exposé, pas que le disque est sain.
