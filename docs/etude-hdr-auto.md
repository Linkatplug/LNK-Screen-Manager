# Étude : HDR automatique pour les jeux

Branche `feature/hdr-auto`. Objectif : quand un jeu compatible HDR se lance, l'écran passe tout seul en HDR, puis revient à l'état d'avant quand le jeu se ferme.

**Verdict : faisable**, sans droits administrateur et sans consommation au repos. Il y a deux réserves (voir §4) : savoir si un jeu « a le HDR » demande une source externe ou ton avis, et certains jeux ne détectent le HDR qu'au démarrage.

---

## 1. Détecter qu'un jeu démarre

| Méthode | Coût au repos | Droits | Moment de détection | Retenu |
|---|---|---|---|---|
| **A. Notification Windows « fenêtre au premier plan »** (`SetWinEventHook` / `EVENT_SYSTEM_FOREGROUND`) | 0 % CPU : Windows nous prévient, on ne surveille rien | aucun | quand la fenêtre du jeu apparaît | ✅ |
| B. Surveillance WMI des nouveaux processus | sondage permanent (CPU non nul) ou droits admin (`Win32_ProcessStartTrace`) | admin | au démarrage du processus | ❌ |
| **C. Lancer le jeu via LNK** (raccourci « Jouer en HDR ») | 0 | aucun | **avant** le jeu : le HDR est déjà actif quand il démarre | ✅ en complément |

- **Fin du jeu** : on attend la fin du processus sur son handle (`RegisterWaitForSingleObject`). Cela ne coûte rien, sans boucle ni minuteur. Ensuite, on remet l'état HDR d'avant.
- **Écran ciblé** : celui où s'affiche la fenêtre du jeu (`MonitorFromWindow`), ou un écran choisi (par ex. « Main »).
- **Alt-Tab** : on laisse le HDR actif tant que le jeu tourne, pour éviter des bascules répétées.
- **Anti-triche** : aucun risque. LNK n'injecte rien et ne touche pas au processus du jeu. Il change seulement le réglage d'écran de Windows, comme le menu Paramètres.

## 2. Savoir si un jeu « a le HDR »

Aucune API Windows ne le dit : c'est le jeu qui choisit sa sortie SDR ou HDR, en interne. Il faut donc combiner trois sources.

**Liste des jeux installés (mesurée sur ce PC) :**
- Windows (Game Bar, `HKCU\System\GameConfigStore`) : 91 exécutables connus, dont 24 encore présents. On y trouve Cyberpunk 2077, Overwatch, Star Citizen, Alan Wake Remastered, Path of Exile… Il y a aussi du bruit (`java.exe`).
- Manifestes Steam (`steamapps\appmanifest_*.acf`, bibliothèques `D:\Jeux\Steam` et `E:\Games`) : ils donnent l'**identifiant Steam** et le dossier de chaque jeu.
- Manifestes Epic : 1 jeu.

**Info HDR par jeu : PCGamingWiki** (base publique, testée) :
- identifiant Steam → `pcgamingwiki.com/api/appid.php?appid=…` → page du jeu → champ `hdr` ;
- Cyberpunk 2077 → `limited` (HDR présent, avec réserves) ✔ ; Brotato → `unknown` ;
- les requêtes « Cargo » (base structurée) sont désormais refusées au public. Il faut lire le texte de la page : une requête par jeu, **une seule fois**, puis mise en cache ;
- le seul élément envoyé est l'identifiant Steam public du jeu. Cette recherche serait **optionnelle** (désactivable) ;
- beaucoup de petits jeux sont en `unknown` : l'utilisateur garde la main.

**Choix manuel** : une liste « Jeux » avec un interrupteur *HDR auto* par jeu, pré-réglé grâce à PCGamingWiki et modifiable.

## 3. Mesures sur ce PC

- Bascule HDR (Main Droit) : **0,42 s** pour l'activer, **0,62 s** pour le couper.
- L'« Auto HDR » de Windows (conversion SDR → HDR) est désactivé ici (`AutoHDREnable=0`). Sans effet sur ce projet.

## 4. Limites et risques

1. **Jeux qui décident SDR/HDR au démarrage** : avec la méthode A, la fenêtre apparaît parfois juste après ce choix. Le jeu reste alors en SDR jusqu'au prochain lancement. La parade est la méthode C (raccourci « Jouer en HDR » : HDR activé, puis jeu lancé). Elle marche aussi pour Steam (`steam://rungameid/<id>`).
2. **Fiabilité de PCGamingWiki** : base communautaire, souvent `unknown` pour les petits jeux, et le format des pages peut changer. Ce n'est qu'un pré-réglage, jamais une obligation.
3. **Jeux hors Steam ou Epic** (Star Citizen, EVE, WoW privé…) : ils sont repérés par Windows ou ajoutables à la main (choisir l'exe), mais sans info HDR automatique.

## 5. Recommandation (v1.2)

- Nouvelle section **Jeux** : jeux détectés (Steam + Epic + Windows) et ajout manuel. Chaque jeu a un interrupteur *HDR auto*, pré-réglé par PCGamingWiki si l'option est activée.
- Détection automatique **A** + restauration à la fermeture du jeu, plus un raccourci **« Jouer en HDR » (C)** pour les jeux capricieux.
- Écran ciblé : celui du jeu, avec la possibilité d'en imposer un.
- Option générale *HDR auto pour les jeux* (on / off) dans Options.
- Coût attendu : **0 % CPU au repos**, moins de 1 Mo de RAM en plus, aucun droit administrateur.

**À trancher avant de coder :**
1. Recherche en ligne PCGamingWiki : activée par défaut, désactivée par défaut, ou pas du tout ?
2. Écran à passer en HDR : celui du jeu, ou toujours le même (ex. « Main ») ?
