// LNK Screen Manager - icone pres de l'heure (controleur), fenetre, options, demarrage.
// By LNK Co (c) 2026
//
// Sobriete des ressources :
//  - au repos l'appli n'est qu'une icone pres de l'heure : aucun minuteur, aucune tache de fond qui tourne ;
//  - la fenetre n'existe que quand elle est ouverte ; fermee, elle est detruite et la memoire rendue a Windows ;
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
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

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

        static bool Get(string k, bool def) { Load(); string v; return d.TryGetValue(k, out v) ? v == "1" : def; }
        static void Set(string k, bool v) { Load(); d[k] = v ? "1" : "0"; Save(); }
        static void Save() { try { File.WriteAllLines(FilePath, d.Select(kv => kv.Key + "=" + kv.Value)); } catch { } }

        public static bool TrayOnClose { get { return Get("TrayOnClose", true); } set { Set("TrayOnClose", value); } }
        public static bool TrayHintShown { get { return Get("TrayHintShown", false); } set { Set("TrayHintShown", value); } }
        public static bool NotifyChanges { get { return Get("NotifyChanges", true); } set { Set("NotifyChanges", value); } }
        public static bool ShowMap { get { return Get("ShowMap", true); } set { Set("ShowMap", value); } }
        public static bool AutoHdrGames { get { return Get("AutoHdrGames", true); } set { Set("AutoHdrGames", value); } }
        public static bool WikiLookup { get { return Get("WikiLookup", true); } set { Set("WikiLookup", value); } }
        /// Ecran a passer en HDR pour les jeux ("" = ecran ou s'affiche le jeu).
        public static string HdrTarget { get { Load(); string v; return d.TryGetValue("HdrTarget", out v) ? v : ""; } set { Load(); d["HdrTarget"] = value ?? ""; Save(); } }
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
        MainForm form;
        GamesForm gamesForm;
        readonly GameWatcher watcher;
        DateTime lastRefresh = DateTime.MinValue;

        public List<Monitor> Mons = new List<Monitor>();
        public List<Profile> ProfileList = new List<Profile>();
        public bool Busy, StatusError, Exiting;
        public string Status = "";
        public event Action Changed;

        public TrayApp(bool hidden)
        {
            var h = sync.Handle; // file de messages pour revenir sur le thread de l'interface
            tray.Icon = LogoArt.AppIcon(SystemInformation.SmallIconSize.Width);
            tray.Text = Brand.Name;
            tray.ContextMenuStrip = menu;
            menu.Opening += (s, e) => { Refresh(); BuildTrayMenu(); e.Cancel = false; };
            menu.Closed += (s, e) => { if (form == null) Post(() => { Menus.Clear(menu); Win.Trim(); }); };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowMain(); };
            tray.BalloonTipClicked += (s, e) => ShowMain();
            tray.Visible = true;
            debounce.Tick += (s, e) => { debounce.Stop(); Refresh(); };
            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;

            watcher = new GameWatcher(this);
            ApplyGameSettings();
            if (Settings.AutoHdrGames && Settings.WikiLookup)
                new Thread(() => { Games.LookupMissing(); Post(() => { if (form == null && gamesForm == null) Win.Trim(); }); }) { IsBackground = true, Priority = ThreadPriority.BelowNormal }.Start();

            if (hidden) Post(Win.Trim); else ShowMain(); // Post : apres l'initialisation de la boucle de messages
        }

        public void Post(Action a) { try { if (sync.IsHandleCreated) sync.BeginInvoke(a); } catch { } }

        // fenetre fermee = rien a mettre a jour : on ne fait rien du tout
        void OnDisplayChanged(object s, EventArgs e) { if (form != null) Post(() => { debounce.Stop(); debounce.Start(); }); }

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
                return on + " / " + Mons.Count + " écrans allumés" + (hdr > 0 ? " · HDR " + hdr : "");
            }
        }

        public void SetStatus(string s, bool error) { Status = s; StatusError = error; if (Changed != null) Changed(); }

        public int Number(Monitor m) { return Mons.IndexOf(m) + 1; }

        public static string Detail(Monitor m)
        {
            Rectangle r; int hz;
            bool known = Layout.Bounds(m, out r, out hz);
            string res = known ? r.Width + " × " + r.Height + (hz > 0 ? " · " + hz + " Hz" : "") : "";
            string d = m.Active ? res + (m.Primary ? " · principal" : "") : "éteint" + (known ? " · " + res : "");
            return m.HasCustomName ? m.Model + " · " + d : d;
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
                        if (form != null) MessageBox.Show(form, err, Brand.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        else tray.ShowBalloonTip(4000, Brand.Name, err, ToolTipIcon.Warning);
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
            if (MessageBox.Show(Owner, "Supprimer le profil « " + p.Name + " » ?", Brand.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
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

        public void GamesChanged() { watcher.Reset(); }
        public int GameSessions { get { return watcher.ActiveCount; } }

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
                    if (form == null && gamesForm == null) Win.Trim();
                });
            }) { IsBackground = true }.Start();
        }

        public void ShowGames()
        {
            if (Exiting) return;
            if (gamesForm == null)
            {
                Refresh();
                gamesForm = new GamesForm(this);
                gamesForm.FormClosed += (s, e) => { gamesForm = null; if (form == null && !Exiting) Post(Win.Trim); };
                gamesForm.Show();
            }
            else if (gamesForm.WindowState == FormWindowState.Minimized) gamesForm.WindowState = FormWindowState.Normal;
            gamesForm.Activate();
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
            var ren = Menus.Sub("Renommer un écran");
            foreach (var m0 in Mons)
            {
                var m = m0;
                ren.DropDownItems.Add(Menus.Item(Number(m) + "   " + m.Name + (m.HasCustomName ? "   (" + m.Model + ")" : ""), (s, e) => RenameScreen(m)));
            }
            menu.Items.Add(ren);
            var auto = Menus.Item("HDR auto pour les jeux", (s, e) => { Settings.AutoHdrGames = !Settings.AutoHdrGames; ApplyGameSettings(); });
            auto.Checked = Settings.AutoHdrGames;
            menu.Items.Add(auto);
            menu.Items.Add(Menus.Item("Jeux…", (s, e) => ShowGames()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Menus.Item("Tout allumer", (s, e) => AllOn()));
            menu.Items.Add(Menus.Item("Identifier les écrans", (s, e) => Identify(null)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Menus.Item("Ouvrir", (s, e) => ShowMain()));
            menu.Items.Add(Menus.Item("Quitter", (s, e) => Exit()));
        }

        // ------------------------------------------------ fenetre
        public void ShowMain()
        {
            if (Exiting) return;
            if (form == null)
            {
                Refresh();
                form = new MainForm(this);
                form.FormClosed += (s, e) =>
                {
                    form = null; debounce.Stop();
                    if (!Exiting) Post(Win.Trim);
                };
                form.Show();
            }
            else
            {
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
                tray.ShowBalloonTip(5000, Brand.Name, "Toujours disponible près de l'heure (clic droit : profils, écrans, HDR). Désactivable dans Options.", ToolTipIcon.None);
                Settings.TrayHintShown = true;
            }
        }

        public void Exit()
        {
            if (Exiting) return;
            Exiting = true;
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            watcher.Stop();
            tray.Visible = false;
            if (gamesForm != null) gamesForm.Close();
            if (form != null) form.Close();
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { tray.Dispose(); menu.Dispose(); debounce.Dispose(); sync.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // ------------------------------------------------------------------ Fenetre principale (simple vue de l'etat du controleur)
    class MainForm : Form
    {
        readonly TrayApp app;
        readonly Panel header = new Panel(), body = new Panel(), footer = new Panel();
        readonly SectionHeader secMap = new SectionHeader("Disposition"), secScreens = new SectionHeader("Écrans") { Columns = true }, secProfiles = new SectionHeader("Profils");
        readonly LayoutMap map = new LayoutMap();
        readonly List<MonitorCard> cards = new List<MonitorCard>();
        readonly List<Pill> pills = new List<Pill>();
        readonly ContextMenuStrip optionsMenu = Menus.Create(), itemMenu = Menus.Create();
        readonly ToolTip tips = new ToolTip();
        string pillsSig;

        public MainForm(TrayApp app)
        {
            this.app = app;
            Text = Brand.Name; BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.F(9.5f, false);
            AutoScaleMode = AutoScaleMode.None; StartPosition = FormStartPosition.CenterScreen;
            Icon = LogoArt.AppIcon(Theme.D(32));
            ClientSize = new Size(Theme.D(560), Theme.D(560));
            MinimumSize = new Size(Theme.D(470), Theme.D(300));

            header.Dock = DockStyle.Top; header.Height = Theme.D(52); header.BackColor = Theme.Bg;
            header.Paint += PaintHeader;
            var bOpt = new IconButton(Glyph.Settings);
            var bId = new IconButton(Glyph.Identify);
            var bGames = new IconButton(Glyph.Game);
            tips.SetToolTip(bOpt, "Options"); tips.SetToolTip(bId, "Identifier les écrans"); tips.SetToolTip(bGames, "Jeux et HDR auto");
            bGames.Click += (s, e) => app.ShowGames();
            header.Controls.Add(bGames);
            bOpt.Click += (s, e) => optionsMenu.Show(bOpt, new Point(bOpt.Width - optionsMenu.PreferredSize.Width, bOpt.Height + Theme.D(2)));
            bId.Click += (s, e) => app.Identify(null);
            header.Controls.Add(bOpt); header.Controls.Add(bId);
            header.Resize += (s, e) =>
            {
                int y = (header.Height - bOpt.Height) / 2;
                bOpt.Location = new Point(header.Width - Theme.D(14) - bOpt.Width, y);
                bId.Location = new Point(bOpt.Left - Theme.D(2) - bId.Width, y);
                bGames.Location = new Point(bId.Left - Theme.D(2) - bGames.Width, y);
            };

            body.Dock = DockStyle.Fill; body.AutoScroll = true; body.BackColor = Theme.Bg;
            map.HoverChanged += (s, e) => { foreach (var c in cards) { bool h = c.Number == map.Hover; if (c.Highlight != h) { c.Highlight = h; c.Repaint(); } } };
            map.ItemDoubleClicked += n => { var c = cards.FirstOrDefault(x => x.Number == n); if (c != null) app.ToggleScreen(c.M, !c.M.Active); };
            tips.SetToolTip(map, "Double-clic : allumer / éteindre");
            body.Controls.AddRange(new Control[] { secMap, map, secScreens, secProfiles });
            body.Resize += (s, e) => Relayout();

            footer.Dock = DockStyle.Bottom; footer.Height = Theme.D(28); footer.BackColor = Theme.Bg;
            footer.Paint += PaintFooter;

            Controls.Add(body); Controls.Add(footer); Controls.Add(header);

            BuildOptionsMenu();
            itemMenu.Closed += (s, e) => BeginInvoke((Action)(() => Menus.Clear(itemMenu)));

            app.Changed += Render;
            Activated += (s, e) => app.RefreshIfStale();
            Render();
            FitHeight();
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Win.Style(Handle, Theme.Bg); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !app.Exiting) app.OnUserClose();
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            app.Changed -= Render;
            optionsMenu.Dispose(); itemMenu.Dispose(); tips.Dispose();
            base.OnFormClosed(e);

        }

        // ------------------------------------------------ dessin
        void PaintHeader(object s, PaintEventArgs e)
        {
            var g = e.Graphics;
            int ls = Theme.D(22), y = (header.Height - ls) / 2;
            g.DrawImage(LogoArt.Small(ls), Theme.D(18), y, ls, ls);
            Theme.Draw(g, Brand.Name, Theme.F(11f, true), new Rectangle(Theme.D(18) + ls + Theme.D(10), 0, Theme.D(300), header.Height), Theme.Text, TextFormatFlags.VerticalCenter);
        }

        void PaintFooter(object s, PaintEventArgs e)
        {
            var g = e.Graphics; var f = Theme.F(8.5f, false);
            int pad = Theme.D(18), w = footer.Width - 2 * pad;
            string left = app.Busy ? app.Status : (string.IsNullOrEmpty(app.Status) ? app.Summary : app.Status);
            Theme.Draw(g, left, f, new Rectangle(pad, 0, w - Theme.D(130), footer.Height), app.StatusError ? Theme.Danger : Theme.Faint, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            Theme.Draw(g, Brand.By, f, new Rectangle(pad, 0, w, footer.Height), Theme.Faint, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        // ------------------------------------------------ synchronisation avec l'etat
        void Render()
        {
            if (IsDisposed) return;
            var mons = app.Mons;
            UseWaitCursor = app.Busy;

            // lignes d'ecrans : recreees seulement si les ecrans branches changent
            if (string.Join("|", mons.Select(m => m.DevicePath)) != string.Join("|", cards.Select(c => c.M.DevicePath)))
            {
                foreach (var c in cards) { body.Controls.Remove(c); c.Dispose(); }
                cards.Clear();
                foreach (var m0 in mons)
                {
                    var card = new MonitorCard();
                    card.TgOn.Toggled += (s, e) => app.ToggleScreen(card.M, card.TgOn.Checked);
                    card.TgHdr.Toggled += (s, e) => app.SetHdr(card.M, card.TgHdr.Checked);
                    card.HoverChanged += (s, e) => map.SetHover(card.IsHover ? card.Number : -1);
                    card.RenameRequested += (s, e) => app.RenameScreen(card.M);
                    card.MenuRequested += pt => ShowScreenMenu(card, pt);
                    cards.Add(card); body.Controls.Add(card);
                }
            }

            var items = new List<LayoutMap.Item>();
            for (int i = 0; i < mons.Count; i++)
            {
                var m = mons[i];
                cards[i].SetData(m, i + 1, TrayApp.Detail(m));
                Rectangle r; int hz;
                if (Settings.ShowMap && LnkScreenManager.Layout.Bounds(m, out r, out hz)) items.Add(new LayoutMap.Item { M = m, Number = i + 1, R = r });
            }
            if (Settings.ShowMap) map.SetItems(items);

            // profils : reconstruits seulement si la liste ou le profil actif change
            string sig = string.Join("|", app.ProfileList.Select(p => p.Name + (app.Matches(p) ? "*" : "")));
            if (sig != pillsSig)
            {
                pillsSig = sig;
                foreach (var p in pills) { body.Controls.Remove(p); p.Dispose(); }
                pills.Clear();
                foreach (var p0 in app.ProfileList)
                {
                    var p = p0;
                    var pill = new Pill(p.Name) { Active = app.Matches(p) };
                    pill.MouseUp += (s, e) =>
                    {
                        if (e.Button == MouseButtons.Left) app.ApplyProfile(p);
                        else if (e.Button == MouseButtons.Right) ShowProfileMenu(pill, p, e.Location);
                    };
                    tips.SetToolTip(pill, "Clic : appliquer · clic droit : options");
                    pills.Add(pill); body.Controls.Add(pill);
                }
                var add = new Pill("+  Nouveau") { Dashed = true };
                add.Click += (s, e) => app.NewProfile();
                tips.SetToolTip(add, "Enregistrer l'état actuel des écrans comme profil");
                pills.Add(add); body.Controls.Add(add);
            }

            footer.Invalidate();
            Relayout();
        }

        void Relayout()
        {
            int pad = Theme.D(18), w = body.ClientSize.Width - pad * 2, y = Theme.D(8), sy = body.AutoScrollPosition.Y;
            body.SuspendLayout();
            secMap.Visible = map.Visible = Settings.ShowMap;
            if (Settings.ShowMap)
            {
                secMap.SetBounds(pad, y + sy, w, secMap.Height); y += secMap.Height + Theme.D(2);
                map.SetBounds(pad, y + sy, w, map.Height); y += map.Height + Theme.D(16);
            }
            secScreens.SetBounds(pad, y + sy, w, secScreens.Height); y += secScreens.Height + Theme.D(2);
            foreach (var c in cards) { c.SetBounds(pad, y + sy, w, c.Height); y += c.Height + Theme.D(6); }
            y += Theme.D(10);
            secProfiles.SetBounds(pad, y + sy, w, secProfiles.Height); y += secProfiles.Height + Theme.D(4);
            int x = pad, gap = Theme.D(8);
            foreach (var p in pills)
            {
                if (x > pad && x + p.Width > pad + w) { x = pad; y += p.Height + gap; }
                p.Location = new Point(x, y + sy); x += p.Width + gap;
            }
            if (pills.Count > 0) y += pills[0].Height;
            y += Theme.D(14);
            body.AutoScrollMinSize = new Size(0, y);
            body.ResumeLayout();
        }

        /// La fenetre prend la hauteur exacte de son contenu (plafonnee a l'ecran).
        void FitHeight()
        {
            Relayout();
            int h = header.Height + footer.Height + body.AutoScrollMinSize.Height;
            ClientSize = new Size(ClientSize.Width, Math.Min(h, Screen.FromControl(this).WorkingArea.Height - Theme.D(80)));
        }

        // ------------------------------------------------ menus
        void ShowScreenMenu(MonitorCard card, Point pt)
        {
            var m = card.M;
            Menus.Clear(itemMenu);
            itemMenu.Items.Add(Menus.Header(m.Model));
            itemMenu.Items.Add(Menus.Item("Renommer…", (s, e) => app.RenameScreen(m)));
            if (m.HasCustomName) itemMenu.Items.Add(Menus.Item("Reprendre le nom d'origine", (s, e) => app.ResetScreenName(m)));
            itemMenu.Items.Add(new ToolStripSeparator());
            itemMenu.Items.Add(Menus.Item(m.Active ? "Éteindre" : "Allumer", (s, e) => app.ToggleScreen(m, !m.Active)));
            if (m.Active && m.HdrSupported) itemMenu.Items.Add(Menus.Item(m.HdrOn ? "Couper le HDR" : "Activer le HDR", (s, e) => app.SetHdr(m, !m.HdrOn)));
            if (m.Active) itemMenu.Items.Add(Menus.Item("Identifier", (s, e) => app.Identify(m)));
            itemMenu.Show(card, pt);
        }

        void ShowProfileMenu(Control anchor, Profile p, Point pt)
        {
            Menus.Clear(itemMenu);
            itemMenu.Items.Add(Menus.Item("Appliquer", (s, e) => app.ApplyProfile(p)));
            itemMenu.Items.Add(Menus.Item("Remplacer par l'état actuel", (s, e) => app.ReplaceProfile(p)));
            itemMenu.Items.Add(Menus.Item("Raccourci sur le bureau", (s, e) => app.MakeShortcut(p.Name)));
            itemMenu.Items.Add(Menus.Item("Renommer…", (s, e) => app.RenameProfile(p)));
            itemMenu.Items.Add(new ToolStripSeparator());
            var del = Menus.Item("Supprimer", (s, e) => app.DeleteProfile(p)); del.ForeColor = Theme.Danger;
            itemMenu.Items.Add(del);
            itemMenu.Show(anchor, pt);
        }

        void BuildOptionsMenu()
        {
            var tray1 = Menus.Item("Réduire près de l'heure à la fermeture", (s, e) => Settings.TrayOnClose = !Settings.TrayOnClose);
            var mapItem = Menus.Item("Afficher la disposition", (s, e) => { Settings.ShowMap = !Settings.ShowMap; Render(); FitHeight(); });
            var notif = Menus.Item("Notifications", (s, e) => Settings.NotifyChanges = !Settings.NotifyChanges);
            var boot = Menus.Item("Lancer au démarrage de Windows", (s, e) =>
            {
                try { Startup.Enabled = !Startup.Enabled; app.SetStatus(Startup.Enabled ? "Lancement au démarrage activé." : "Lancement au démarrage désactivé.", false); }
                catch (Exception ex) { app.SetStatus("Impossible : " + ex.Message, true); }
            });
            optionsMenu.Items.AddRange(new ToolStripItem[] { tray1, mapItem, notif, boot, new ToolStripSeparator() });
            optionsMenu.Items.Add(Menus.Item("Tout allumer", (s, e) => app.AllOn()));
            optionsMenu.Items.Add(Menus.Item("Couper le HDR partout", (s, e) => app.AllHdrOff()));
            optionsMenu.Items.Add(Menus.Item("Jeux et HDR auto…", (s, e) => app.ShowGames()));
            optionsMenu.Items.Add(Menus.Item("Ouvrir le dossier des données", (s, e) => { try { System.Diagnostics.Process.Start("explorer.exe", "\"" + AppData.Dir + "\""); } catch { } }));
            optionsMenu.Items.Add(new ToolStripSeparator());
            optionsMenu.Items.Add(Menus.Item("À propos", (s, e) => Dialogs.About(this)));
            optionsMenu.Items.Add(Menus.Item("Quitter", (s, e) => app.Exit()));
            optionsMenu.Opening += (s, e) => { tray1.Checked = Settings.TrayOnClose; mapItem.Checked = Settings.ShowMap; notif.Checked = Settings.NotifyChanges; boot.Checked = Startup.Enabled; };
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
                            if (i == 0) app.Post(app.ShowMain); else app.Post(app.RefreshIfOpen);
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
