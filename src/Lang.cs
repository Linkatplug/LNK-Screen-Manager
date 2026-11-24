// LNK Screen Manager - langues (francais, English, Deutsch, Espanol, Italiano, Portugues).
// By LNK Co (c) 2026
//
// Le texte francais sert de cle : L.T("Écrans") renvoie la traduction de la langue active.
// Langue "auto" = langue de Windows ; une langue non geree bascule en anglais.
// Pour ajouter une langue : ajouter son code + son nom, puis une colonne dans la table.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace LnkScreenManager
{
    static class L
    {
        public static readonly string[] Codes = { "fr", "en", "de", "es", "it", "pt" };
        public static readonly string[] Native = { "Français", "English", "Deutsch", "Español", "Italiano", "Português" };
        static int idx;
        public static event Action Changed;

        public static int Index { get { return idx; } }

        /// setting : "auto" ou un code de langue.
        public static void Apply(string setting)
        {
            string code = string.IsNullOrEmpty(setting) || setting == "auto" ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName : setting;
            int i = Array.IndexOf(Codes, code);
            if (i < 0) i = 1; // langue non geree : anglais
            if (i == idx) return;
            idx = i;
            if (Changed != null) Changed();
        }

        public static string T(string fr)
        {
            if (idx == 0) return fr;
            string[] v;
            return map.TryGetValue(fr, out v) && v[idx - 1] != null ? v[idx - 1] : fr;
        }

        public static string T(string fr, params object[] args) { return string.Format(T(fr), args); }

        static string[] R(string en, string de, string es, string it, string pt) { return new[] { en, de, es, it, pt }; }

        static readonly Dictionary<string, string[]> map = new Dictionary<string, string[]>
        {
            // --- navigation / general
            { "Écrans", R("Screens", "Bildschirme", "Pantallas", "Schermi", "Telas") },
            { "Jeux", R("Games", "Spiele", "Juegos", "Giochi", "Jogos") },
            { "Réglages", R("Settings", "Einstellungen", "Ajustes", "Impostazioni", "Configurações") },
            { "Mode clair", R("Light mode", "Heller Modus", "Modo claro", "Modalità chiara", "Modo claro") },
            { "Mode sombre", R("Dark mode", "Dunkler Modus", "Modo oscuro", "Modalità scura", "Modo escuro") },
            { "Version {0}", R("Version {0}", "Version {0}", "Versión {0}", "Versione {0}", "Versão {0}") },
            { "Prêt", R("Ready", "Bereit", "Listo", "Pronto", "Pronto") },
            { "HDR auto jeux actif", R("Game auto HDR on", "Auto-HDR für Spiele aktiv", "HDR automático de juegos activo", "HDR automatico giochi attivo", "HDR automático de jogos ativo") },
            { "{0} / {1} écrans allumés", R("{0} / {1} screens on", "{0} / {1} Bildschirme an", "{0} / {1} pantallas encendidas", "{0} / {1} schermi accesi", "{0} / {1} telas ligadas") },
            { "HDR sur {0}", R("HDR on {0}", "HDR auf {0}", "HDR en {0}", "HDR su {0}", "HDR em {0}") },
            { "Enregistrer", R("Save", "Speichern", "Guardar", "Salva", "Salvar") },
            { "Annuler", R("Cancel", "Abbrechen", "Cancelar", "Annulla", "Cancelar") },
            { "Ouvrir", R("Open", "Öffnen", "Abrir", "Apri", "Abrir") },
            { "Quitter", R("Quit", "Beenden", "Salir", "Esci", "Sair") },
            { "Impossible : {0}", R("Not possible: {0}", "Nicht möglich: {0}", "No es posible: {0}", "Impossibile: {0}", "Não foi possível: {0}") },

            // --- page Ecrans
            { "Identifier", R("Identify", "Identifizieren", "Identificar", "Identifica", "Identificar") },
            { "Tout allumer", R("Turn all on", "Alle einschalten", "Encender todas", "Accendi tutti", "Ligar todas") },
            { "Moniteurs", R("Monitors", "Monitore", "Monitores", "Monitor", "Monitores") },
            { "Profils", R("Profiles", "Profile", "Perfiles", "Profili", "Perfis") },
            { "ÉCRAN", R("SCREEN", "ANZEIGE", "PANTALLA", "SCHERMO", "TELA") },
            { "Double-clic : allumer / éteindre", R("Double-click: turn on / off", "Doppelklick: ein / aus", "Doble clic: encender / apagar", "Doppio clic: accendi / spegni", "Clique duplo: ligar / desligar") },
            { "Clic : appliquer · clic droit : options", R("Click: apply · right-click: options", "Klick: anwenden · Rechtsklick: Optionen", "Clic: aplicar · clic derecho: opciones", "Clic: applica · clic destro: opzioni", "Clique: aplicar · clique direito: opções") },
            { "Nouveau profil", R("New profile", "Neues Profil", "Nuevo perfil", "Nuovo profilo", "Novo perfil") },
            { "Enregistrer l'état actuel des écrans comme profil", R("Save the current screen setup as a profile", "Aktuellen Bildschirmzustand als Profil speichern", "Guardar el estado actual de las pantallas como perfil", "Salva lo stato attuale degli schermi come profilo", "Salvar o estado atual das telas como perfil") },
            { "Renommer…", R("Rename…", "Umbenennen…", "Renombrar…", "Rinomina…", "Renomear…") },
            { "Reprendre le nom d'origine", R("Restore original name", "Ursprünglichen Namen wiederherstellen", "Restaurar el nombre original", "Ripristina il nome originale", "Restaurar o nome original") },
            { "Éteindre", R("Turn off", "Ausschalten", "Apagar", "Spegni", "Desligar") },
            { "Allumer", R("Turn on", "Einschalten", "Encender", "Accendi", "Ligar") },
            { "Couper le HDR", R("Turn HDR off", "HDR ausschalten", "Desactivar HDR", "Disattiva HDR", "Desativar HDR") },
            { "Activer le HDR", R("Turn HDR on", "HDR einschalten", "Activar HDR", "Attiva HDR", "Ativar HDR") },
            { "Appliquer", R("Apply", "Anwenden", "Aplicar", "Applica", "Aplicar") },
            { "Remplacer par l'état actuel", R("Replace with current setup", "Durch aktuellen Zustand ersetzen", "Reemplazar por el estado actual", "Sostituisci con lo stato attuale", "Substituir pelo estado atual") },
            { "Raccourci sur le bureau", R("Desktop shortcut", "Desktop-Verknüpfung", "Acceso directo en el escritorio", "Collegamento sul desktop", "Atalho na área de trabalho") },
            { "Supprimer", R("Delete", "Löschen", "Eliminar", "Elimina", "Excluir") },
            { "principal", R("main", "Hauptbildschirm", "principal", "principale", "principal") },
            { "Éteint", R("Off", "Aus", "Apagada", "Spento", "Desligada") },

            // --- page Jeux
            { "Le HDR s'active au lancement d'un jeu compatible et revient comme avant à sa fermeture.", R("HDR turns on when a compatible game starts and goes back to how it was when the game closes.", "HDR wird beim Start eines kompatiblen Spiels aktiviert und beim Beenden zurückgesetzt.", "El HDR se activa al iniciar un juego compatible y vuelve a su estado anterior al cerrarlo.", "L'HDR si attiva all'avvio di un gioco compatibile e torna com'era alla chiusura.", "O HDR é ativado ao iniciar um jogo compatível e volta ao estado anterior ao fechá-lo.") },
            { "HDR auto", R("Auto HDR", "Auto-HDR", "HDR automático", "HDR automatico", "HDR automático") },
            { "Tous ({0})", R("All ({0})", "Alle ({0})", "Todos ({0})", "Tutti ({0})", "Todos ({0})") },
            { "HDR auto ({0})", R("Auto HDR ({0})", "Auto-HDR ({0})", "HDR auto ({0})", "HDR auto ({0})", "HDR auto ({0})") },
            { "À vérifier ({0})", R("To check ({0})", "Zu prüfen ({0})", "Por revisar ({0})", "Da verificare ({0})", "A verificar ({0})") },
            { "Ajouter un jeu", R("Add a game", "Spiel hinzufügen", "Añadir un juego", "Aggiungi un gioco", "Adicionar um jogo") },
            { "HDR auto activé pour les jeux.", R("Auto HDR enabled for games.", "Auto-HDR für Spiele aktiviert.", "HDR automático activado para juegos.", "HDR automatico attivato per i giochi.", "HDR automático ativado para jogos.") },
            { "HDR auto désactivé.", R("Auto HDR disabled.", "Auto-HDR deaktiviert.", "HDR automático desactivado.", "HDR automatico disattivato.", "HDR automático desativado.") },
            { "Jeux actualisés.", R("Games refreshed.", "Spiele aktualisiert.", "Juegos actualizados.", "Giochi aggiornati.", "Jogos atualizados.") },
            { "Jeux actualisés, recherche des infos HDR…", R("Games refreshed, looking up HDR info…", "Spiele aktualisiert, HDR-Infos werden gesucht…", "Juegos actualizados, buscando información HDR…", "Giochi aggiornati, ricerca delle info HDR…", "Jogos atualizados, buscando informações de HDR…") },
            { "Infos HDR : {0} / {1} — {2}", R("HDR info: {0} / {1} — {2}", "HDR-Infos: {0} / {1} — {2}", "Info HDR: {0} / {1} — {2}", "Info HDR: {0} / {1} — {2}", "Info de HDR: {0} / {1} — {2}") },
            { "Infos HDR déjà à jour.", R("HDR info already up to date.", "HDR-Infos sind bereits aktuell.", "La información HDR ya está al día.", "Le info HDR sono già aggiornate.", "As informações de HDR já estão atualizadas.") },
            { "Infos HDR à jour : {0} jeu(x) consulté(s) sur PCGamingWiki.", R("HDR info updated: {0} game(s) looked up on PCGamingWiki.", "HDR-Infos aktualisiert: {0} Spiel(e) auf PCGamingWiki abgefragt.", "Información HDR actualizada: {0} juego(s) consultado(s) en PCGamingWiki.", "Info HDR aggiornate: {0} gioco/i consultato/i su PCGamingWiki.", "Informações de HDR atualizadas: {0} jogo(s) consultado(s) no PCGamingWiki.") },
            { "PCGamingWiki injoignable : nouvel essai plus tard.", R("PCGamingWiki unreachable: will retry later.", "PCGamingWiki nicht erreichbar: später neuer Versuch.", "PCGamingWiki no responde: se reintentará más tarde.", "PCGamingWiki non raggiungibile: nuovo tentativo più tardi.", "PCGamingWiki inacessível: nova tentativa mais tarde.") },
            { "Rechercher de nouveaux jeux", R("Scan for new games", "Nach neuen Spielen suchen", "Buscar juegos nuevos", "Cerca nuovi giochi", "Procurar novos jogos") },
            { "manuel", R("manual", "manuell", "manual", "manuale", "manual") },
            { "Raccourci « Jouer en HDR » sur le bureau", R("“Play in HDR” desktop shortcut", "Desktop-Verknüpfung „In HDR spielen“", "Acceso directo «Jugar en HDR» en el escritorio", "Collegamento «Gioca in HDR» sul desktop", "Atalho “Jogar em HDR” na área de trabalho") },
            { "Ouvrir le dossier du jeu", R("Open game folder", "Spielordner öffnen", "Abrir la carpeta del juego", "Apri la cartella del gioco", "Abrir a pasta do jogo") },
            { "Suivre PCGamingWiki ({0})", R("Follow PCGamingWiki ({0})", "PCGamingWiki folgen ({0})", "Seguir PCGamingWiki ({0})", "Segui PCGamingWiki ({0})", "Seguir PCGamingWiki ({0})") },
            { "Retirer de la liste", R("Remove from list", "Aus der Liste entfernen", "Quitar de la lista", "Rimuovi dall'elenco", "Remover da lista") },
            { "Choisir l'exécutable du jeu", R("Choose the game executable", "Spiel-EXE auswählen", "Elegir el ejecutable del juego", "Scegli l'eseguibile del gioco", "Escolher o executável do jogo") },
            { "Programmes (*.exe)|*.exe", R("Programs (*.exe)|*.exe", "Programme (*.exe)|*.exe", "Programas (*.exe)|*.exe", "Programmi (*.exe)|*.exe", "Programas (*.exe)|*.exe") },
            { "« {0} » ajouté en HDR auto.", R("“{0}” added with auto HDR.", "„{0}“ mit Auto-HDR hinzugefügt.", "«{0}» añadido con HDR automático.", "«{0}» aggiunto con HDR automatico.", "“{0}” adicionado com HDR automático.") },
            { "info HDR en attente", R("HDR info pending", "HDR-Info ausstehend", "info HDR pendiente", "info HDR in attesa", "info de HDR pendente") },
            { "pas d'info HDR automatique", R("no automatic HDR info", "keine automatische HDR-Info", "sin info HDR automática", "nessuna info HDR automatica", "sem info de HDR automática") },
            { "HDR natif", R("native HDR", "natives HDR", "HDR nativo", "HDR nativo", "HDR nativo") },
            { "HDR (limité)", R("HDR (limited)", "HDR (eingeschränkt)", "HDR (limitado)", "HDR (limitato)", "HDR (limitado)") },
            { "pas de HDR", R("no HDR", "kein HDR", "sin HDR", "niente HDR", "sem HDR") },
            { "HDR inconnu", R("HDR unknown", "HDR unbekannt", "HDR desconocido", "HDR sconosciuto", "HDR desconhecido") },
            { "Ajouté", R("Added", "Hinzugefügt", "Añadido", "Aggiunto", "Adicionado") },
            { "{0} : l'écran du jeu ne gère pas le HDR.", R("{0}: the game's screen doesn't support HDR.", "{0}: Der Bildschirm des Spiels unterstützt kein HDR.", "{0}: la pantalla del juego no admite HDR.", "{0}: lo schermo del gioco non supporta l'HDR.", "{0}: a tela do jogo não suporta HDR.") },
            { "HDR activé pour {0}.", R("HDR turned on for {0}.", "HDR für {0} eingeschaltet.", "HDR activado para {0}.", "HDR attivato per {0}.", "HDR ativado para {0}.") },
            { "HDR coupé : {0} fermé.", R("HDR turned off: {0} closed.", "HDR aus: {0} beendet.", "HDR desactivado: {0} cerrado.", "HDR disattivato: {0} chiuso.", "HDR desativado: {0} fechado.") },
            { "Jeu introuvable : {0}", R("Game not found: {0}", "Spiel nicht gefunden: {0}", "Juego no encontrado: {0}", "Gioco non trovato: {0}", "Jogo não encontrado: {0}") },

            // --- page Reglages
            { "Système", R("System", "System", "Sistema", "Sistema", "Sistema") },
            { "Sombre", R("Dark", "Dunkel", "Oscuro", "Scuro", "Escuro") },
            { "Clair", R("Light", "Hell", "Claro", "Chiaro", "Claro") },
            { "Écran du jeu", R("Game's screen", "Spielbildschirm", "Pantalla del juego", "Schermo del gioco", "Tela do jogo") },
            { "Apparence", R("Appearance", "Darstellung", "Apariencia", "Aspetto", "Aparência") },
            { "Thème", R("Theme", "Design", "Tema", "Tema", "Tema") },
            { "Système = comme Windows", R("System = same as Windows", "System = wie Windows", "Sistema = igual que Windows", "Sistema = come Windows", "Sistema = igual ao Windows") },
            { "Langue", R("Language", "Sprache", "Idioma", "Lingua", "Idioma") },
            { "Automatique = langue de Windows", R("Automatic = Windows language", "Automatisch = Windows-Sprache", "Automático = idioma de Windows", "Automatica = lingua di Windows", "Automático = idioma do Windows") },
            { "Automatique", R("Automatic", "Automatisch", "Automático", "Automatica", "Automático") },
            { "Fenêtre et démarrage", R("Window & startup", "Fenster & Start", "Ventana e inicio", "Finestra e avvio", "Janela e inicialização") },
            { "Réduire près de l'heure", R("Minimize to the clock", "Neben die Uhr minimieren", "Minimizar junto al reloj", "Riduci accanto all'orologio", "Minimizar perto do relógio") },
            { "Le bouton X garde l'appli près de l'horloge", R("The X button keeps the app next to the clock", "Die X-Taste hält die App neben der Uhr", "El botón X mantiene la app junto al reloj", "Il pulsante X tiene l'app accanto all'orologio", "O botão X mantém o app perto do relógio") },
            { "Lancer avec Windows", R("Start with Windows", "Mit Windows starten", "Iniciar con Windows", "Avvia con Windows", "Iniciar com o Windows") },
            { "Démarre discrètement près de l'horloge", R("Starts quietly next to the clock", "Startet unauffällig neben der Uhr", "Se inicia discretamente junto al reloj", "Si avvia in silenzio accanto all'orologio", "Inicia discretamente perto do relógio") },
            { "Notifications", R("Notifications", "Benachrichtigungen", "Notificaciones", "Notifiche", "Notificações") },
            { "Bulle lors d'un changement fait hors fenêtre", R("Pop-up when something changes outside the window", "Hinweis bei Änderungen außerhalb des Fensters", "Aviso cuando algo cambia fuera de la ventana", "Avviso quando qualcosa cambia fuori dalla finestra", "Aviso quando algo muda fora da janela") },
            { "Mini-carte des écrans", R("Screen mini-map", "Bildschirm-Minikarte", "Minimapa de pantallas", "Minimappa degli schermi", "Minimapa das telas") },
            { "Affiche la disposition sur la page Écrans", R("Shows the layout on the Screens page", "Zeigt die Anordnung auf der Seite Bildschirme", "Muestra la disposición en la página Pantallas", "Mostra la disposizione nella pagina Schermi", "Mostra o layout na página Telas") },
            { "HDR des jeux", R("Game HDR", "Spiele-HDR", "HDR de juegos", "HDR dei giochi", "HDR dos jogos") },
            { "HDR auto pour les jeux", R("Auto HDR for games", "Auto-HDR für Spiele", "HDR automático para juegos", "HDR automatico per i giochi", "HDR automático para jogos") },
            { "Au lancement d'un jeu compatible", R("When a compatible game starts", "Beim Start eines kompatiblen Spiels", "Al iniciar un juego compatible", "All'avvio di un gioco compatibile", "Ao iniciar um jogo compatível") },
            { "Écran à passer en HDR", R("Screen to switch to HDR", "Bildschirm für HDR", "Pantalla que pasa a HDR", "Schermo da passare in HDR", "Tela a mudar para HDR") },
            { "Infos HDR en ligne", R("Online HDR info", "Online-HDR-Infos", "Información HDR en línea", "Info HDR online", "Informações de HDR online") },
            { "PCGamingWiki · seul l'identifiant Steam est envoyé", R("PCGamingWiki · only the Steam ID is sent", "PCGamingWiki · nur die Steam-ID wird gesendet", "PCGamingWiki · solo se envía el ID de Steam", "PCGamingWiki · viene inviato solo l'ID Steam", "PCGamingWiki · só o ID da Steam é enviado") },
            { "Couper", R("Turn off", "Ausschalten", "Desactivar", "Disattiva", "Desativar") },
            { "Actions", R("Actions", "Aktionen", "Acciones", "Azioni", "Ações") },
            { "Allumer tous les écrans", R("Turn on all screens", "Alle Bildschirme einschalten", "Encender todas las pantallas", "Accendi tutti gli schermi", "Ligar todas as telas") },
            { "Couper le HDR partout", R("Turn HDR off everywhere", "HDR überall ausschalten", "Desactivar el HDR en todas", "Disattiva l'HDR ovunque", "Desativar o HDR em todas") },
            { "Dossier des données", R("Data folder", "Datenordner", "Carpeta de datos", "Cartella dei dati", "Pasta de dados") },
            { "Profils, noms, réglages", R("Profiles, names, settings", "Profile, Namen, Einstellungen", "Perfiles, nombres, ajustes", "Profili, nomi, impostazioni", "Perfis, nomes, configurações") },
            { "À propos", R("About", "Über", "Acerca de", "Informazioni", "Sobre") },
            { "Gratuit et open source · Windows 10 / 11", R("Free and open source · Windows 10 / 11", "Kostenlos und Open Source · Windows 10 / 11", "Gratis y de código abierto · Windows 10 / 11", "Gratuito e open source · Windows 10 / 11", "Gratuito e de código aberto · Windows 10 / 11") },
            { "Source des infos HDR", R("HDR info source", "Quelle der HDR-Infos", "Fuente de la información HDR", "Fonte delle info HDR", "Fonte das informações de HDR") },
            { "Lancement avec Windows activé.", R("Start with Windows enabled.", "Start mit Windows aktiviert.", "Inicio con Windows activado.", "Avvio con Windows attivato.", "Iniciar com o Windows ativado.") },
            { "Lancement avec Windows désactivé.", R("Start with Windows disabled.", "Start mit Windows deaktiviert.", "Inicio con Windows desactivado.", "Avvio con Windows disattivato.", "Iniciar com o Windows desativado.") },
            { "Mini-carte affichée.", R("Mini-map shown.", "Minikarte eingeblendet.", "Minimapa visible.", "Minimappa visibile.", "Minimapa exibido.") },
            { "Mini-carte masquée.", R("Mini-map hidden.", "Minikarte ausgeblendet.", "Minimapa oculto.", "Minimappa nascosta.", "Minimapa oculto.") },
            { "Écran débranché", R("Screen unplugged", "Bildschirm getrennt", "Pantalla desconectada", "Schermo scollegato", "Tela desconectada") },
            { "Écran où s'affiche le jeu", R("Screen the game is shown on", "Bildschirm, auf dem das Spiel läuft", "Pantalla donde se muestra el juego", "Schermo in cui appare il gioco", "Tela onde o jogo aparece") },

            // --- actions et messages
            { "Il faut garder au moins un écran allumé.", R("At least one screen must stay on.", "Mindestens ein Bildschirm muss an bleiben.", "Al menos una pantalla debe quedar encendida.", "Almeno uno schermo deve restare acceso.", "Pelo menos uma tela precisa ficar ligada.") },
            { "Allumage de {0}…", R("Turning on {0}…", "{0} wird eingeschaltet…", "Encendiendo {0}…", "Accensione di {0}…", "Ligando {0}…") },
            { "Extinction de {0}…", R("Turning off {0}…", "{0} wird ausgeschaltet…", "Apagando {0}…", "Spegnimento di {0}…", "Desligando {0}…") },
            { "{0} allumé.", R("{0} on.", "{0} an.", "{0} encendida.", "{0} acceso.", "{0} ligada.") },
            { "{0} éteint.", R("{0} off.", "{0} aus.", "{0} apagada.", "{0} spento.", "{0} desligada.") },
            { "HDR activé sur {0}.", R("HDR on for {0}.", "HDR auf {0} eingeschaltet.", "HDR activado en {0}.", "HDR attivato su {0}.", "HDR ativado em {0}.") },
            { "HDR coupé sur {0}.", R("HDR off for {0}.", "HDR auf {0} ausgeschaltet.", "HDR desactivado en {0}.", "HDR disattivato su {0}.", "HDR desativado em {0}.") },
            { "Profil « {0} »…", R("Profile “{0}”…", "Profil „{0}“…", "Perfil «{0}»…", "Profilo «{0}»…", "Perfil “{0}”…") },
            { "Profil « {0} » appliqué.", R("Profile “{0}” applied.", "Profil „{0}“ angewendet.", "Perfil «{0}» aplicado.", "Profilo «{0}» applicato.", "Perfil “{0}” aplicado.") },
            { "Allumage de tous les écrans…", R("Turning on all screens…", "Alle Bildschirme werden eingeschaltet…", "Encendiendo todas las pantallas…", "Accensione di tutti gli schermi…", "Ligando todas as telas…") },
            { "Tous les écrans sont allumés.", R("All screens are on.", "Alle Bildschirme sind an.", "Todas las pantallas están encendidas.", "Tutti gli schermi sono accesi.", "Todas as telas estão ligadas.") },
            { "Coupure du HDR…", R("Turning HDR off…", "HDR wird ausgeschaltet…", "Desactivando HDR…", "Disattivazione HDR…", "Desativando HDR…") },
            { "HDR coupé partout.", R("HDR off everywhere.", "HDR überall aus.", "HDR desactivado en todas.", "HDR disattivato ovunque.", "HDR desativado em todas.") },
            { "Renommer l'écran", R("Rename screen", "Bildschirm umbenennen", "Renombrar pantalla", "Rinomina schermo", "Renomear tela") },
            { "Nom pour « {0} »  (vide = nom d'origine) :", R("Name for “{0}”  (empty = original name):", "Name für „{0}“  (leer = Originalname):", "Nombre para «{0}»  (vacío = nombre original):", "Nome per «{0}»  (vuoto = nome originale):", "Nome para “{0}”  (vazio = nome original):") },
            { "{0} a repris son nom d'origine.", R("{0} is back to its original name.", "{0} hat wieder seinen Originalnamen.", "{0} recuperó su nombre original.", "{0} ha ripreso il nome originale.", "{0} voltou ao nome original.") },
            { "Écran renommé en « {0} ».", R("Screen renamed to “{0}”.", "Bildschirm in „{0}“ umbenannt.", "Pantalla renombrada a «{0}».", "Schermo rinominato in «{0}».", "Tela renomeada para “{0}”.") },
            { "Nom non enregistré : {0}", R("Name not saved: {0}", "Name nicht gespeichert: {0}", "Nombre no guardado: {0}", "Nome non salvato: {0}", "Nome não salvo: {0}") },
            { "Nom du profil (l'état actuel des écrans est enregistré) :", R("Profile name (the current screen setup is saved):", "Profilname (der aktuelle Bildschirmzustand wird gespeichert):", "Nombre del perfil (se guarda el estado actual de las pantallas):", "Nome del profilo (viene salvato lo stato attuale degli schermi):", "Nome do perfil (o estado atual das telas é salvo):") },
            { "Jeu", R("Game", "Spiel", "Juego", "Gioco", "Jogo") },
            { "Profil « {0} » enregistré.", R("Profile “{0}” saved.", "Profil „{0}“ gespeichert.", "Perfil «{0}» guardado.", "Profilo «{0}» salvato.", "Perfil “{0}” salvo.") },
            { "Profil « {0} » mis à jour.", R("Profile “{0}” updated.", "Profil „{0}“ aktualisiert.", "Perfil «{0}» actualizado.", "Profilo «{0}» aggiornato.", "Perfil “{0}” atualizado.") },
            { "Renommer le profil", R("Rename profile", "Profil umbenennen", "Renombrar perfil", "Rinomina profilo", "Renomear perfil") },
            { "Nouveau nom :", R("New name:", "Neuer Name:", "Nuevo nombre:", "Nuovo nome:", "Novo nome:") },
            { "Supprimer le profil", R("Delete profile", "Profil löschen", "Eliminar perfil", "Elimina profilo", "Excluir perfil") },
            { "Supprimer le profil « {0} » ?", R("Delete profile “{0}”?", "Profil „{0}“ löschen?", "¿Eliminar el perfil «{0}»?", "Eliminare il profilo «{0}»?", "Excluir o perfil “{0}”?") },
            { "Profil supprimé.", R("Profile deleted.", "Profil gelöscht.", "Perfil eliminado.", "Profilo eliminato.", "Perfil excluído.") },
            { "Jouer en HDR - {0}", R("Play in HDR - {0}", "In HDR spielen - {0}", "Jugar en HDR - {0}", "Gioca in HDR - {0}", "Jogar em HDR - {0}") },
            { "Active le HDR puis lance {0}", R("Turns HDR on, then starts {0}", "Schaltet HDR ein und startet dann {0}", "Activa el HDR y luego inicia {0}", "Attiva l'HDR e poi avvia {0}", "Ativa o HDR e depois inicia {0}") },
            { "{0} - profil {1}", R("{0} - profile {1}", "{0} - Profil {1}", "{0} - perfil {1}", "{0} - profilo {1}", "{0} - perfil {1}") },
            { "Raccourci « {0} » créé sur le bureau.", R("Shortcut “{0}” created on the desktop.", "Verknüpfung „{0}“ auf dem Desktop erstellt.", "Acceso directo «{0}» creado en el escritorio.", "Collegamento «{0}» creato sul desktop.", "Atalho “{0}” criado na área de trabalho.") },
            { "Raccourci impossible : {0}", R("Could not create shortcut: {0}", "Verknüpfung nicht möglich: {0}", "No se pudo crear el acceso directo: {0}", "Impossibile creare il collegamento: {0}", "Não foi possível criar o atalho: {0}") },
            { "Identifier les écrans", R("Identify screens", "Bildschirme identifizieren", "Identificar pantallas", "Identifica gli schermi", "Identificar telas") },
            { "Toujours disponible près de l'heure (clic droit : profils, écrans, HDR). Désactivable dans Réglages.", R("Still available next to the clock (right-click: profiles, screens, HDR). Can be turned off in Settings.", "Weiterhin neben der Uhr verfügbar (Rechtsklick: Profile, Bildschirme, HDR). In den Einstellungen abschaltbar.", "Sigue disponible junto al reloj (clic derecho: perfiles, pantallas, HDR). Se puede desactivar en Ajustes.", "Sempre disponibile accanto all'orologio (clic destro: profili, schermi, HDR). Disattivabile in Impostazioni.", "Continua disponível perto do relógio (clique direito: perfis, telas, HDR). Pode ser desativado em Configurações.") },
            { "Profil introuvable : {0}", R("Profile not found: {0}", "Profil nicht gefunden: {0}", "Perfil no encontrado: {0}", "Profilo non trovato: {0}", "Perfil não encontrado: {0}") },

            // --- coeur (erreurs)
            { "Écran inconnu", R("Unknown screen", "Unbekannter Bildschirm", "Pantalla desconocida", "Schermo sconosciuto", "Tela desconhecida") },
            { "Impossible de changer le HDR de {0} (erreur {1})", R("Could not change HDR on {0} (error {1})", "HDR von {0} konnte nicht geändert werden (Fehler {1})", "No se pudo cambiar el HDR de {0} (error {1})", "Impossibile cambiare l'HDR di {0} (errore {1})", "Não foi possível alterar o HDR de {0} (erro {1})") },
            { "Windows a refusé la configuration (erreur {0}).", R("Windows rejected the configuration (error {0}).", "Windows hat die Konfiguration abgelehnt (Fehler {0}).", "Windows rechazó la configuración (error {0}).", "Windows ha rifiutato la configurazione (errore {0}).", "O Windows recusou a configuração (erro {0}).") },
            { "Aucun des écrans demandés n'est branché.", R("None of the requested screens is connected.", "Keiner der angeforderten Bildschirme ist angeschlossen.", "Ninguna de las pantallas solicitadas está conectada.", "Nessuno degli schermi richiesti è collegato.", "Nenhuma das telas solicitadas está conectada.") },
            { "Impossible de tout rallumer (erreur {0}).", R("Could not turn everything back on (error {0}).", "Konnte nicht alles wieder einschalten (Fehler {0}).", "No se pudo volver a encender todo (error {0}).", "Impossibile riaccendere tutto (errore {0}).", "Não foi possível religar tudo (erro {0}).") },
        };
    }
}
