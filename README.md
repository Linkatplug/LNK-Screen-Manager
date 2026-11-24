# LNK Screen Manager

By LNK Co © 2026 — allumer / éteindre ses écrans et gérer le HDR en un clic, sans passer par les Paramètres Windows.

## Utilisation
- **LNK Screen Manager.exe** : ouvre la fenêtre (interrupteurs *Écran* et *HDR* par écran, mini-carte, profils).
- Fermer la fenêtre (X) la réduit près de l'heure. Clic droit sur l'icône : profils, écrans, HDR. Désactivable dans *Options*.
- Profils : *Nouveau profil* enregistre l'état actuel. Clic = appliquer, clic droit = raccourci bureau / renommer / supprimer.
- Double-clic sur un écran de la mini-carte : l'allumer / l'éteindre.
- Renommer un écran : clic sur son nom (crayon), clic droit sur sa carte, ou clic droit sur l'icône près de l'heure → *Renommer un écran*. Nom vide = nom d'origine.
- En cas de souci : **Win + P → Étendre** rallume tous les écrans.

## Ressources
Au repos (icône près de l'heure) : ~2-4 Mo de RAM, 0 % de processeur (aucun minuteur, aucune tâche de fond active).
La fenêtre n'existe que lorsqu'elle est ouverte : à la fermeture elle est détruite et la mémoire rendue à Windows.
Option *Afficher la disposition* pour masquer la mini-carte.

## Ligne de commande
```
"LNK Screen Manager.exe" --profile "Jeu"   applique un profil sans fenêtre
"LNK Screen Manager.exe" --all-on          rallume tous les écrans
"LNK Screen Manager.exe" --tray            démarre réduit près de l'heure
```

## Sur un autre PC
Windows 10 / 11 uniquement, rien à installer (.NET Framework 4 est inclus dans Windows). Copier l'exe, le lancer, recréer ses profils.
Les données (profils, disposition, options) sont dans `%APPDATA%\LNK Screen Manager`.

## Compiler
`build.bat` (utilise le compilateur C# fourni avec Windows). Sources dans `src\`, icône et logo générés dans `assets\`.
