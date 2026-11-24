# LNK Screen Manager

By LNK Co © 2026 — gratuit et open source (licence MIT) · *free and open source (MIT license)*

Allumer / éteindre ses écrans et gérer le HDR en un clic, sans passer par les Paramètres Windows — avec HDR automatique au lancement des jeux.
*Turn screens on / off and toggle HDR in one click — with automatic HDR when a game starts.*

**Langues / Languages :** Français · English · Deutsch · Español · Italiano · Português (automatique selon Windows, modifiable dans Réglages).

---

## Français

### Fonctions
- **Écrans** : un interrupteur *Écran* et un interrupteur *HDR* par moniteur, mini-carte de la disposition, noms personnalisés (« Main », « Gauche haut »…), identification.
- **Profils** : enregistre l'état des écrans (ex. « Jeu ») et applique-le en un clic, depuis un raccourci bureau ou en ligne de commande.
- **HDR auto pour les jeux** : jeux détectés automatiquement (Steam, Epic, jeux reconnus par Windows, ajout manuel). Un jeu compatible passe l'écran en HDR à son lancement ; l'état d'avant revient à sa fermeture. Raccourci « Jouer en HDR » pour les jeux qui ne détectent le HDR qu'au démarrage.
- **Icône près de l'heure** : profils, écrans et HDR au clic droit. Le X réduit l'appli à cet endroit (désactivable).
- Mode sombre / clair / comme Windows, 6 langues, net à toutes les échelles d'affichage (100 % à 200 %, écrans d'échelles différentes), mise en page compacte automatique sur petits écrans.
- **Léger** : au repos ~2-4 Mo de RAM et 0 % de processeur (aucune surveillance en boucle).

### Installation
Rien à installer : copier `LNK Screen Manager.exe` où on veut et le lancer. Windows 10 ou 11 (.NET Framework 4, inclus dans Windows). Pas besoin de droits administrateur.

**Avertissement SmartScreen** : l'exe n'est pas signé (pas de certificat payant). Au premier lancement, Windows peut afficher « Windows a protégé votre ordinateur » : cliquer sur **Informations complémentaires** puis **Exécuter quand même**. Pour vérifier l'exe, comparez son empreinte SHA-256 avec celle publiée dans la release (`Get-FileHash "LNK Screen Manager.exe"`), ou compilez-le vous-même (voir plus bas).

### Données et vie privée
- Profils, noms d'écrans, disposition et réglages : `%APPDATA%\LNK Screen Manager` (rien d'autre n'est écrit, sauf la clé de démarrage Windows si l'option est activée).
- Seule connexion réseau : l'option *Infos HDR en ligne* interroge PCGamingWiki avec l'**identifiant Steam public** de chaque jeu, une seule fois par jeu. Désactivable dans Réglages.
- Aucun anti-triche concerné : l'appli ne touche jamais aux jeux, elle change uniquement le réglage d'écran de Windows.

### Ligne de commande
```
"LNK Screen Manager.exe" --profile "Jeu"    applique un profil sans fenêtre
"LNK Screen Manager.exe" --play "steam:ID"   active le HDR puis lance le jeu
"LNK Screen Manager.exe" --all-on           rallume tous les écrans
"LNK Screen Manager.exe" --tray             démarre réduit près de l'heure
```
En cas de souci d'affichage : **Win + P → Étendre** rallume tous les écrans.

### Compiler
`build.bat` (utilise le compilateur C# fourni avec Windows, aucune dépendance). Sources dans `src\`, icône et logo générés dans `assets\`.
Ajouter une langue : `src\Lang.cs` (une colonne dans la table).

---

## English

### Features
- **Screens**: one *Screen* switch and one *HDR* switch per monitor, layout mini-map, custom names, identify.
- **Profiles**: save your screen setup (e.g. “Gaming”) and apply it in one click, from a desktop shortcut or the command line.
- **Auto HDR for games**: games are detected automatically (Steam, Epic, games known to Windows, manual add). A compatible game switches its screen to HDR when it starts; the previous state comes back when it closes. “Play in HDR” shortcut for games that only check HDR at startup.
- **Tray icon** next to the clock with profiles, screens and HDR on right-click.
- Dark / light / system theme, 6 languages, sharp at any display scaling (including mixed-DPI setups), automatic compact layout on small screens.
- **Lightweight**: ~2-4 MB RAM and 0 % CPU when idle (no polling).

### Install
Nothing to install: copy `LNK Screen Manager.exe` anywhere and run it. Windows 10 or 11, no admin rights needed.
The exe is not code-signed, so SmartScreen may warn on first launch: click **More info** → **Run anyway**, or build it yourself.

### Privacy
Data lives in `%APPDATA%\LNK Screen Manager`. The only network access is the optional *Online HDR info* lookup, which sends a game's public Steam ID to PCGamingWiki once per game.

### Build
Run `build.bat` (uses the C# compiler shipped with Windows, no dependencies).

---

## Crédits / Credits
- Informations HDR des jeux : [PCGamingWiki](https://www.pcgamingwiki.com) — contenu sous licence [CC BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/). LNK Screen Manager n'est pas affilié à PCGamingWiki.
  *Game HDR information: PCGamingWiki, CC BY-NC-SA 3.0. Not affiliated with PCGamingWiki.*
- Steam, Epic Games et Windows sont des marques de leurs propriétaires respectifs.

## Licence
[MIT](LICENSE) © 2026 LNK Co
