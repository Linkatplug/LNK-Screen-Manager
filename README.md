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

## HDR automatique pour les jeux
- Bouton manette (ou menu près de l'heure → *Jeux…*) : liste des jeux détectés (Steam, Epic, jeux reconnus par Windows) + ajout manuel d'un exe.
- Un jeu marqué *HDR auto* passe l'écran en HDR dès que sa fenêtre apparaît ; l'état d'avant est remis à sa fermeture (y compris si un lanceur démarre le vrai jeu).
- Pré-réglage par PCGamingWiki (n'envoie que l'identifiant Steam public, une fois par jeu ; désactivable).
- Écran ciblé : celui où s'affiche le jeu, ou un écran imposé.
- Clic droit sur un jeu → raccourci **« Jouer en HDR »** : active le HDR *avant* de lancer le jeu (pour les jeux qui ne détectent le HDR qu'au démarrage).
- Coût au repos : 0 % CPU (notification Windows, aucune surveillance en boucle).
## Ligne de commande
```
"LNK Screen Manager.exe" --profile "Jeu"   applique un profil sans fenêtre
"LNK Screen Manager.exe" --all-on          rallume tous les écrans
"LNK Screen Manager.exe" --tray            démarre réduit près de l'heure
"LNK Screen Manager.exe" --play "steam:ID"  active le HDR puis lance le jeu
```

## Sur un autre PC
Windows 10 / 11 uniquement, rien à installer (.NET Framework 4 est inclus dans Windows). Copier l'exe, le lancer, recréer ses profils.
Les données (profils, disposition, options) sont dans `%APPDATA%\LNK Screen Manager`.

## Compiler
`build.bat` (utilise le compilateur C# fourni avec Windows). Sources dans `src\`, icône et logo générés dans `assets\`.
