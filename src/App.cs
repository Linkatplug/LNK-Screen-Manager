// LNK Screen Manager - controleur (icone pres de l'heure, etat, actions), options, demarrage.
// By LNK Co (c) 2026
//
// Sobriete des ressources :
//  - au repos l'appli n'est qu'une icone pres de l'heure : aucun minuteur, aucune tache de fond qui tourne ;
//  - la fenetre n'existe que quand elle est ouverte ; fermee, elle est detruite et la memoire rendue a Windows ;
//  - les animations utilisent un seul minuteur qui ne tourne que pendant un mouvement ;
//  - les menus sont construits a l'ouverture puis liberes.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("LNK Screen Manager")]
[assembly: AssemblyDescription("Allumer / éteindre les écrans et gérer le HDR")]
[assembly: AssemblyCompany("LNK Co")]
[assembly: AssemblyProduct("LNK Screen Manager")]
[assembly: AssemblyCopyright("© 2026 LNK Co")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]

namespace LnkScreenManager
{
    // ------------------------------------------------------------------ Options (settings.ini dans %APPDATA%)
    static class Settings
    {
        static Dictionary<string, string> d;
        static string FilePath { get { return AppData.File("settings.ini"); } }

        static void Load()
        {
            if (d != null) return;
            d = new Dictionary<string, string>();
            try
            {
                if (File.Exists(FilePath))
                    foreach (var l in File.ReadAllLines(FilePath))
                    {
                        int i = l.IndexOf('=');
                        if (i > 0) d[l.Substring(0, i).Trim()] = l.Substring(i + 1).Trim();
                    }
            }
            catch { }
        }

        static void Save() { try { File.WriteAllLines(FilePath, d.Select(kv => kv.Key + "=" + kv.Value)); } catch { } }
        static bool Get(string k, bool def) { Load(); string v; return d.TryGetValue(k, out v) ? v == "1" : def; }
        static void Set(string k, bool v) { Load(); d[k] = v ? "1" : "0"; Save(); }
        static string GetS(string k, string def) { Load(); string v; return d.TryGetValue(k, out v) ? v : def; }
        static void SetS(string k, string v) { Load(); d[k] = v ?? ""; Save(); }

        public static bool TrayOnClose { get { return Get("TrayOnClose", true); } set { Set("TrayOnClose", value); } }
        public static bool TrayHintShown { get { return Get("TrayHintShown", false); } set { Set("TrayHintShown", value); } }
        public static bool NotifyChanges { get { return Get("NotifyChanges", true); } set { Set("NotifyChanges", value); } }
        public static bool ShowMap { get { return Get("ShowMap", true); } set { Set("ShowMap", value); } }
        public static bool AutoHdrGames { get { return Get("AutoHdrGames", true); } set { Set("AutoHdrGames", value); } }
        public static bool WikiLookup { get { return Get("WikiLookup", true); } set { Set("WikiLookup", value); } }
        /// Ecran a passer en HDR pour les jeux ("" = ecran ou s'affiche le jeu).
        public static string HdrTarget { get { return GetS("HdrTarget", ""); } set { SetS("HdrTarget", value); } }
        /// Theme : "system", "dark" ou "light".
        public static string ThemeMode { get { return GetS("Theme", "system"); } set { SetS("Theme", value); } }
    }

    static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        static string Cmd { get { return "\"" + Application.ExecutablePath + "\" --tray"; } }

        public static bool Enabled
        {
            get { try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(Brand.Name) != null; } catch { return false; } }
            set
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value) k.SetValue(Brand.Name, Cmd); else k.DeleteValue(Brand.Name, false);
                }
            }
        }

        /// Si l'exe a ete deplace, met a jour le chemin de demarrage (n'ecrit que si necessaire).
        public static void RefreshPath()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    var v = k.GetValue(Brand.Name) as string;
                    if (v != null && v != Cmd) k.SetValue(Brand.Name, Cmd);
                }
            }
            catch { }
        }
    }

    // ------------------------------------------------------------------ Controleur : icone pres de l'heure + etat + actions
    class TrayApp : ApplicationContext
    {
        readonly NotifyIcon tray = new NotifyIcon();
        readonly ContextMenuStrip menu = Menus.Create();
        readonly Control sync = new Control();
        readonly System.Windows.Forms.Timer debounce = new System.Windows.Forms.Timer { Interval = 600 };
        readonly GameWatcher watcher;
        MainForm form;
        DateTime lastRefresh = DateTime.MinValue;
        bool lookingUp;

        public List<Monitor> Mons = new List<Monitor>();
        public List<Profile> ProfileList = new List<Profile>();
        public bool Busy, StatusError, Exiting;
        public string Status = "";
        public event Action Changed, GamesUpdated;

        public TrayApp(bool hidden)
        {
            var h = sync.Handle; // file de messages pour revenir sur le thread de l'interface
            tray.Icon = LogoArt.AppIcon(SystemInformation.SmallIconSize.Width);
            tray.Text = Brand.Name;
            tray.ContextMenuStrip = menu;
            menu.Opening += (s, e) => { Refresh(); BuildTrayMenu(); e.Cancel = false; };
            menu.Closed += (s, e) => { if (form == null) Post(() => { Menus.Clear(menu); Win.Trim(); }); };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowMain(Page.Screens); };
            tray.BalloonTipClicked += (s, e) => ShowMain(Page.Screens);
            tray.Visible = true;
            debounce.Tick += (s, e) => { debounce.Stop(); Refresh(); };
            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            SystemEvents.UserPreferenceChanged += OnUserPreference;

            watcher = new GameWatcher(this);
            ApplyGameSettings();
            if (Settings.AutoHdrGames && Settings.WikiLookup) LookupGames();

            if (hidden) Post(Win.Trim); else ShowMain(Page.Screens); // Post : apres l'initialisation de la boucle de messages
        }

        public void Post(Action a) { try { if (sync.IsHandleCreated) sync.BeginInvoke(a); } catch { } }

        // fenetre fermee = rien a mettre a jour : on ne fait rien du tout
        void OnDisplayChanged(object s, EventArgs e) { if (form != null) Post(() => { debounce.Stop(); debounce.Start(); }); }

        // theme "Systeme" : suit le passage clair / sombre de Windows
        void OnUserPreference(object s, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General && Settings.ThemeMode == "system")
                Post(() => Theme.Apply(Theme.SystemDark));
        }

        // ------------------------------------------------ etat
        public void Refresh()
        {
            if (Busy) return;
            try { Mons = ReadingOrder(Displays.List()); ProfileList = Profiles.Load(); }
            catch (Exception ex) { SetStatus(ex.Message, true); return; }
            lastRefresh = DateTime.Now;
            tray.Text = Brand.Name + "\n" + Summary;
            if (Changed != null) Changed();
        }

        public void RefreshIfStale() { if ((DateTime.Now - lastRefresh).TotalMilliseconds > 1500) Refresh(); }
        public void RefreshIfOpen() { if (form != null) Refresh(); }

        public string Summary
        {
            get
            {
                int on = Mons.Count(m => m.Active), hdr = Mons.Count(m => m.Active && m.HdrOn);
                return on + " / " + Mons.Count + " écrans allumés" + (hdr > 0 ? " · HDR sur " + hdr : "");
            }
        }

        public void SetStatus(string s, bool error) { Status = s; StatusError = error; if (Changed != null) Changed(); }

        public int Number(Monitor m) { return Mons.IndexOf(m) + 1; }

        public static string Detail(Monitor m)
        {
            Rectangle r; int hz;
            bool known = Layout.Bounds(m, out r, out hz);
            string res = known ? r.Width + " × " + r.Height + (hz > 0 ? "  ·  " + hz + " Hz" : "") : "";
            string d = m.Active ? res + (m.Primary ? "  ·  principal" : "") : "Éteint" + (known ? "  ·  " + res : "");
            return m.HasCustomName ? m.Model + "  ·  " + d : d;
        }

        public bool Matches(Profile p)
        {
            bool any = false;
            foreach (var m in Mons)
            {
                bool[] v;
                if (!p.Screens.TryGetValue(m.DevicePath, out v)) continue;
                any = true;
                if (v[0] != m.Active) return false;
                if (m.Active && m.HdrSupported && v[1] != m.HdrOn) return false;
            }
            return any;
        }

        /// Ordre de lecture : rangee du haut d'abord, puis de gauche a droite (positions memorisees si eteint).
        static List<Monitor> ReadingOrder(List<Monitor> list)
        {
            var withR = list.Select(m =>
            {
                Rectangle r; int hz;
                bool k = Layout.Bounds(m, out r, out hz);
                return Tuple.Create(m, k ? r : new Rectangle(int.MaxValue / 4, 0, 1, 1));
            }).OrderBy(t => t.Item2.Y + t.Item2.Height / 2).ToList();
            var result = new List<Monitor>();
            var row = new List<Tuple<Monitor, Rectangle>>();
            int rowCenter = 0, rowHalf = 0;
            foreach (var t in withR)
            {
                int cy = t.Item2.Y + t.Item2.Height / 2;
                if (row.Count > 0 && cy - rowCenter > rowHalf) { result.AddRange(row.OrderBy(x => x.Item2.X).Select(x => x.Item1)); row.Clear(); }
                if (row.Count == 0) { rowCenter = cy; rowHalf = Math.Max(1, t.Item2.Height / 2); }
                row.Add(t);
            }
            result.AddRange(row.OrderBy(x => x.Item2.X).Select(x => x.Item1));
            return result;
        }

        // ------------------------------------------------ actions (executees hors du thread de l'interface)
        public void Run(string doing, Action a, string done)
        {
            if (Busy) return;
            Busy = true; SetStatus(doing, false);
            new Thread(() =>
            {
                string err = null;
                try { a(); } catch (Exception ex) { err = ex.Message; }
                Post(() =>
                {
                    Busy = false;
                    Refresh();
                    if (err != null)
                    {
                        SetStatus(err, true);
                        if (form == null) tray.ShowBalloonTip(4000, Brand.Name, err, ToolTipIcon.Warning);
                    }
                    else
                    {
                        SetStatus(done, false);
                        if (form == null && Settings.NotifyChanges) tray.ShowBalloonTip(2000, Brand.Name, done, ToolTipIcon.None);
                    }
                    if (form == null) Win.Trim();
                });
            }) { IsBackground = true }.Start();
        }

        public void ToggleScreen(Monitor m, bool on)
        {
            var target = Mons.Where(x => x.Active).Select(x => x.DevicePath).ToList();
            if (on) { if (!target.Contains(m.DevicePath)) target.Add(m.DevicePath); }
            else target.Remove(m.DevicePath);
            if (target.Count == 0) { SetStatus("Il faut garder au moins un écran allumé.", true); Refresh(); return; }
            string path = m.DevicePath, name = m.Name;
            Run((on ? "Allumage de " : "Extinction de ") + name + "…",
                () => { Displays.SetActive(target); Displays.WaitFor(l => l.Any(x => x.DevicePath == path && x.Active == on)); },
                name + (on ? " allumé." : " éteint."));
        }

        public void SetHdr(Monitor m, bool on)
        {
            string name = m.Name;
            Run("HDR…", () => { Displays.SetHdr(m, on); Thread.Sleep(300); }, "HDR " + (on ? "activé" : "coupé") + " sur " + name + ".");
        }

        public void ApplyProfile(Profile p) { Run("Profil « " + p.Name + " »…", () => Profiles.Apply(p), "Profil « " + p.Name + " » appliqué."); }

        public void AllOn() { Run("Allumage de tous les écrans…", () => { Displays.AllOn(); Thread.Sleep(1500); }, "Tous les écrans sont allumés."); }

        public void AllHdrOff()
        {
            Run("Coupure du HDR…", () => { foreach (var m in Displays.List().Where(x => x.Active && x.HdrOn)) Displays.SetHdr(m, false); Thread.Sleep(300); }, "HDR coupé partout.");
        }

        public void Identify(Monitor only)
        {
            Refresh();
            foreach (var m in Mons)
            {
                if (!m.Active || (only != null && m.DevicePath != only.DevicePath)) continue;
                var sc = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == m.GdiName);
                if (sc != null) new IdentifyForm(sc.Bounds, Number(m), m.HasCustomName ? m.Name + "  (" + m.Model + ")" : m.Name).Show();
            }
        }

        IWin32Window Owner { get { return form != null && form.Visible ? form : null; } }

        public void RenameScreen(Monitor m)
        {
            if (m == null) return;
            string n = Dialogs.Prompt(Owner, "Renommer l'écran", "Nom pour « " + m.Model + " »  (vide = nom d'origine) :", m.HasCustomName ? m.Name : "");
            if (n == null) return;
            try { Names.Set(m.DevicePath, n); Refresh(); SetStatus(n.Length == 0 ? m.Model + " a repris son nom d'origine." : "Écran renommé en « " + n + " ».", false); }
            catch (Exception ex) { SetStatus("Nom non enregistré : " + ex.Message, true); }
        }

        public void ResetScreenName(Monitor m) { try { Names.Set(m.DevicePath, ""); Refresh(); } catch (Exception ex) { SetStatus(ex.Message, true); } }

        // ------------------------------------------------ profils
        public void NewProfile()
        {
            string name = Dialogs.Prompt(Owner, "Nouveau profil", "Nom du profil (l'état actuel des écrans est enregistré) :", ProfileList.Count == 0 ? "Jeu" : "");
            if (string.IsNullOrEmpty(name)) return;
            name = name.Replace("|", "");
            var all = Profiles.Load();
            int i = all.FindIndex(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            var np = Profiles.FromCurrent(name);
            if (i >= 0) all[i] = np; else all.Add(np);
            Profiles.Save(all);
            Refresh(); SetStatus("Profil « " + name + " » enregistré.", false);
        }

        public void ReplaceProfile(Profile p)
        {
            var all = Profiles.Load(); int i = all.FindIndex(x => x.Name == p.Name);
            if (i >= 0) { all[i] = Profiles.FromCurrent(p.Name); Profiles.Save(all); }
            Refresh(); SetStatus("Profil « " + p.Name + " » mis à jour.", false);
        }

        public void RenameProfile(Profile p)
        {
            string n = Dialogs.Prompt(Owner, "Renommer le profil", "Nouveau nom :", p.Name);
            if (string.IsNullOrEmpty(n)) return;
            var all = Profiles.Load(); var x = all.FirstOrDefault(q => q.Name == p.Name);
            if (x != null) { x.Name = n.Replace("|", ""); Profiles.Save(all); }
            Refresh();
        }

        public void DeleteProfile(Profile p)
        {
            if (!Dialogs.Confirm(Owner, "Supprimer le profil", "Supprimer le profil « " + p.Name + " » ?", "Supprimer")) return;
            var all = Profiles.Load(); all.RemoveAll(x => x.Name == p.Name); Profiles.Save(all);
            Refresh(); SetStatus("Profil supprimé.", false);
        }

        public void MakeShortcut(string profile) { CreateShortcut("LNK - " + profile, "--profile \"" + profile + "\"", Brand.Name + " - profil " + profile); }
        public void MakePlayShortcut(Game g) { CreateShortcut("Jouer en HDR - " + g.Name, "--play \"" + g.Key + "\"", "Active le HDR puis lance " + g.Name); }

        void CreateShortcut(string title, string args, string desc)
        {
            object shell = null, sc = null;
            try
            {
                var t = Type.GetTypeFromProgID("WScript.Shell");
                shell = Activator.CreateInstance(t);
                string safe = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
                string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), safe + ".lnk");
                sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                var st = sc.GetType();
                Action<string, object> set = (name, val) => st.InvokeMember(name, BindingFlags.SetProperty, null, sc, new[] { val });
                set("TargetPath", Application.ExecutablePath);
                set("Arguments", args);
                set("WorkingDirectory", Path.GetDirectoryName(Application.ExecutablePath));
                set("IconLocation", Application.ExecutablePath + ",0");
                set("Description", desc);
                st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
                SetStatus("Raccourci « " + safe + " » créé sur le bureau.", false);
            }
            catch (Exception ex) { SetStatus("Raccourci impossible : " + ex.Message, true); }
            finally
            {
                if (sc != null) Marshal.FinalReleaseComObject(sc);
                if (shell != null) Marshal.FinalReleaseComObject(shell);
            }
        }

        // ------------------------------------------------ HDR auto pour les jeux
        public void ApplyGameSettings()
        {
            if (Settings.AutoHdrGames) watcher.Start(); else watcher.Stop();
            watcher.Reset();
        }

        public void GamesChanged() { watcher.Reset(); if (GamesUpdated != null) GamesUpdated(); }
        public int GameSessions { get { return watcher.ActiveCount; } }

        /// Recherche en arriere-plan des infos HDR manquantes (PCGamingWiki).
        public void LookupGames()
        {
            if (lookingUp || !Settings.WikiLookup) return;
            lookingUp = true;
            new Thread(() =>
            {
                int n = 0;
                try { n = Games.LookupMissing(); } catch { }
                Post(() =>
                {
                    lookingUp = false;
                    if (n > 0) GamesChanged();
                    if (form == null) Win.Trim();
                });
            }) { IsBackground = true, Priority = ThreadPriority.BelowNormal }.Start();
        }
        public bool LookingUp { get { return lookingUp; } }

        public void RescanGames() { Games.Reload(); GamesChanged(); LookupGames(); }

        /// Bascule HDR declenchee par un jeu (independante des autres actions en cours).
        public void HdrAuto(Monitor m, bool on, string msg)
        {
            new Thread(() =>
            {
                string err = null;
                try { Displays.SetHdr(m, on); } catch (Exception ex) { err = ex.Message; }
                Post(() =>
                {
                    Refresh();
                    SetStatus(err ?? msg, err != null);
                    if (form == null && (err != null || Settings.NotifyChanges)) tray.ShowBalloonTip(2000, Brand.Name, err ?? msg, ToolTipIcon.None);
                    if (form == null) Win.Trim();
                });
            }) { IsBackground = true }.Start();
        }

        // ------------------------------------------------ menu pres de l'heure
        void BuildTrayMenu()
        {
            Menus.Clear(menu);
            menu.Items.Add(Menus.Header(Brand.Name));

            if (ProfileList.Count > 0)
            {
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(Menus.Header("Profils"));
                foreach (var p0 in ProfileList)
                {
                    var p = p0;
                    var it = Menus.Item(p.Name, (s, e) => ApplyProfile(p));
                    it.Checked = Matches(p);
                    menu.Items.Add(it);
                }
            }

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Menus.Header("Écrans"));
            foreach (var m0 in Mons)
            {
                var m = m0;
                var it = Menus.Item(Number(m) + "   " + m.Name, (s, e) => ToggleScreen(m, !m.Active));
                it.Checked = m.Active;
                menu.Items.Add(it);
            }

            var hdr = Mons.Where(m => m.Active && m.HdrSupported).ToList();
            if (hdr.Count > 0)
            {
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(Menus.Header("HDR"));
                foreach (var m0 in hdr)
                {
                    var m = m0;
                    var it = Menus.Item(Number(m) + "   " + m.Name, (s, e) => SetHdr(m, !m.HdrOn));
                    it.Checked = m.HdrOn;
                    menu.Items.Add(it);
                }
            }

            menu.Items.Add(new ToolStripSeparator());
            var auto = Menus.Item("HDR auto pour les jeux", (s, e) => { Settings.AutoHdrGames = !Settings.AutoHdrGames; ApplyGameSettings(); if (Changed != null) Changed(); });
            auto.Checked = Settings.AutoHdrGames;
            menu.Items.Add(auto);
            menu.Items.Add(Menus.Item("Tout allumer", (s, e) => AllOn()));
            menu.Items.Add(Menus.Item("Identifier les écrans", (s, e) => Identify(null)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Menus.Item("Écrans", (s, e) => ShowMain(Page.Screens)));
            menu.Items.Add(Menus.Item("Jeux", (s, e) => ShowMain(Page.Games)));
            menu.Items.Add(Menus.Item("Réglages", (s, e) => ShowMain(Page.Settings)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Menus.Item("Quitter", (s, e) => Exit()));
        }

        // ------------------------------------------------ fenetre
        public void ShowMain(Page page)
        {
            if (Exiting) return;
            if (form == null)
            {
                Refresh();
                form = new MainForm(this, page);
                form.FormClosed += (s, e) =>
                {
                    form = null; debounce.Stop();
                    if (!Exiting) Post(Win.Trim);
                };
                form.Show();
            }
            else
            {
                form.ShowPage(page);
                if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
            }
            form.Activate();
        }

        /// Appele quand l'utilisateur ferme la fenetre (X).
        public void OnUserClose()
        {
            if (!Settings.TrayOnClose) { Exit(); return; }
            if (!Settings.TrayHintShown)
            {
                tray.ShowBalloonTip(5000, Brand.Name, "Toujours disponible près de l'heure (clic droit : profils, écrans, HDR). Désactivable dans Réglages.", ToolTipIcon.None);
                Settings.TrayHintShown = true;
            }
        }

        public void Exit()
        {
            if (Exiting) return;
            Exiting = true;
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            SystemEvents.UserPreferenceChanged -= OnUserPreference;
            watcher.Stop();
            tray.Visible = false;
            if (form != null) form.Close();
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { tray.Dispose(); menu.Dispose(); debounce.Dispose(); sync.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // ------------------------------------------------------------------ Point d'entree
    static class Program
    {
        const string ShowEvent = "LNKScreenManager.Show", RefreshEvent = "LNKScreenManager.Refresh";

        [STAThread]
        static int Main(string[] args)
        {
            Win.SetProcessDPIAware();
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) Theme.S = g.DpiX / 96f;
            string a0 = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            try
            {
                if (a0 == "--make-icon")
                {
                    string dir = args.Length > 1 ? args[1] : ".";
                    Directory.CreateDirectory(dir);
                    LogoArt.SaveIco(Path.Combine(dir, "app.ico"));
                    using (var b = LogoArt.Render(512)) b.Save(Path.Combine(dir, "logo.png"), ImageFormat.Png);
                    return 0;
                }
                if (a0 == "--profile" && args.Length >= 2)
                {
                    var p = Profiles.Load().FirstOrDefault(x => string.Equals(x.Name, args[1], StringComparison.OrdinalIgnoreCase));
                    if (p == null) { MessageBox.Show("Profil introuvable : " + args[1], Brand.Name); return 1; }
                    Profiles.Apply(p); Signal(RefreshEvent); return 0;
                }
                if (a0 == "--all-on") { Displays.AllOn(); Signal(RefreshEvent); return 0; }
                if (a0 == "--play" && args.Length >= 2) { int r = Player.Play(args[1]); Signal(RefreshEvent); return r; }
                if (a0 == "--list")
                {
                    Console.WriteLine(string.Join("\n", Displays.List().Select(m => m.Label + "   " + m.Hz + " Hz   HDR " + (m.HdrSupported ? (m.HdrOn ? "ON" : "off") : "n/a"))));
                    return 0;
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, Brand.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning); return 1; }

            // une seule instance : relancer l'exe rouvre la fenetre de l'instance deja lancee
            bool created;
            using (var mutex = new Mutex(true, "LNKScreenManager.SingleInstance", out created))
            {
                if (!created) { Signal(ShowEvent); return 0; }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Theme.ApplyMode(Settings.ThemeMode);
                Startup.RefreshPath();

                using (var app = new TrayApp(a0 == "--tray"))
                using (var evShow = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEvent))
                using (var evRefresh = new EventWaitHandle(false, EventResetMode.AutoReset, RefreshEvent))
                {
                    // thread endormi en permanence (0 % CPU) : se reveille seulement sur signal d'une autre instance
                    new Thread(() =>
                    {
                        var hs = new WaitHandle[] { evShow, evRefresh };
                        while (true)
                        {
                            int i;
                            try { i = WaitHandle.WaitAny(hs); } catch { return; }
                            if (i == 0) app.Post(() => app.ShowMain(Page.Screens)); else app.Post(app.RefreshIfOpen);
                        }
                    }) { IsBackground = true }.Start();

                    Application.Run(app);
                }
            }
            return 0;
        }

        static void Signal(string name)
        {
            try { using (var ev = EventWaitHandle.OpenExisting(name)) ev.Set(); } catch { }
        }
    }
}
