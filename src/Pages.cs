// LNK Screen Manager - fenetre principale (menu lateral) et pages Ecrans / Jeux / Reglages.
// By LNK Co (c) 2026
//
// Chaque page est dimensionnee pour tenir dans la fenetre sans defilement
// (la grille des jeux adapte son nombre de colonnes ; defilement seulement en dernier recours).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace LnkScreenManager
{
    enum Page { Screens, Games, Settings }

    // ------------------------------------------------------------------ Fenetre principale
    class MainForm : Form
    {
        readonly TrayApp app;
        readonly Sidebar side;
        readonly Panel host = new Panel();
        readonly StatusLine status;
        readonly Dictionary<Page, PageBase> pages = new Dictionary<Page, PageBase>();
        Page current = (Page)(-1);

        public MainForm(TrayApp app, Page page)
        {
            this.app = app;
            Text = Brand.Name; Font = Theme.F(10f, false); AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen; KeyPreview = true;
            Icon = LogoArt.AppIcon(Theme.D(32));
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            side = new Sidebar(this) { Dock = DockStyle.Left };
            status = new StatusLine(app) { Dock = DockStyle.Bottom };
            host.Dock = DockStyle.Fill;
            Controls.Add(host); Controls.Add(status); Controls.Add(side);

            pages[Page.Screens] = new ScreensPage(app);
            pages[Page.Games] = new GamesPage(app);
            pages[Page.Settings] = new SettingsPage(app);
            foreach (var p in pages.Values) { p.Dock = DockStyle.Fill; p.Visible = false; host.Controls.Add(p); }

            ApplyColors();
            foreach (var p in pages.Values) p.Render();
            SizeWindow();

            app.Changed += OnChanged;
            app.GamesUpdated += OnGames;
            Theme.Changed += OnTheme;
            Activated += (s, e) => app.RefreshIfStale();
            ShowPage(page);
        }

        /// Taille de fenetre pour que chaque page tienne sans defilement.
        void SizeWindow()
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            int sideW = side.Width, statusH = status.Height;
            int maxW = (int)(wa.Width * 0.85) - sideW, maxH = wa.Height - Theme.D(110) - statusH;
            int w = Theme.D(780);
            int h = Math.Max(Theme.D(600), Math.Max(pages[Page.Screens].Needed(w, 0), pages[Page.Settings].Needed(w, 0)));
            var games = (GamesPage)pages[Page.Games];
            // jeux : on agrandit d'abord en hauteur (jusqu'a 860 px), puis en largeur
            int capH = Math.Min(maxH, Theme.D(860));
            while (h < capH && games.Needed(w, h) > h) h = Math.Min(capH, h + Theme.D(20));
            while (w < maxW && games.Needed(w, h) > h) w = Math.Min(maxW, w + Theme.D(20));
            w += Theme.D(4); // marge de securite (arrondis)
            h = Math.Min(h, maxH);
            ClientSize = new Size(sideW + w, h + statusH);
            MinimumSize = new Size(sideW + Theme.D(560) + (Width - ClientSize.Width), Theme.D(480));
        }

        public void ShowPage(Page p)
        {
            if (p == current) return;
            current = p;
            var page = pages[p];
            page.Render();
            page.Visible = true;
            page.BringToFront();
            foreach (var kv in pages) if (kv.Key != p) kv.Value.Visible = false;
            side.Select(p);
        }

        void OnChanged() { if (IsDisposed) return; status.Invalidate(); if (current >= 0) pages[current].Render(); }
        void OnGames() { if (!IsDisposed) pages[Page.Games].Render(); }

        void OnTheme()
        {
            if (IsDisposed) return;
            SuspendLayout();
            ApplyColors();
            foreach (var p in pages.Values) p.OnThemeChanged();
            Win.Style(Handle);
            ResumeLayout();
            Invalidate(true);
        }

        void ApplyColors() { BackColor = Theme.Bg; host.BackColor = Theme.Bg; side.BackColor = Theme.Side; status.BackColor = Theme.Bg; side.UpdateTheme(); }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Win.Style(Handle); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Control && e.KeyCode == Keys.D1) ShowPage(Page.Screens);
            if (e.Control && e.KeyCode == Keys.D2) ShowPage(Page.Games);
            if (e.Control && e.KeyCode == Keys.D3) ShowPage(Page.Settings);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !app.Exiting) app.OnUserClose();
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            app.Changed -= OnChanged; app.GamesUpdated -= OnGames; Theme.Changed -= OnTheme;
            base.OnFormClosed(e);
        }

        public void ToggleTheme()
        {
            Settings.ThemeMode = Theme.Dark ? "light" : "dark";
            Theme.ApplyMode(Settings.ThemeMode);
        }
    }

    // ------------------------------------------------------------------ Menu lateral
    class Sidebar : Smooth
    {
        readonly MainForm form;
        readonly NavButton[] nav;
        readonly NavButton themeBtn;

        public Sidebar(MainForm form)
        {
            this.form = form; Width = Theme.D(224);
            nav = new[] { new NavButton(Glyph.Monitor, "Écrans"), new NavButton(Glyph.Game, "Jeux"), new NavButton(Glyph.Settings, "Réglages") };
            for (int i = 0; i < nav.Length; i++)
            {
                var p = (Page)i;
                nav[i].Click += (s, e) => form.ShowPage(p);
                Controls.Add(nav[i]);
            }
            themeBtn = new NavButton(Glyph.Sun, "Mode clair");
            themeBtn.Click += (s, e) => form.ToggleTheme();
            Controls.Add(themeBtn);
        }

        public void Select(Page p) { for (int i = 0; i < nav.Length; i++) nav[i].SetSelected(i == (int)p); }

        public void UpdateTheme()
        {
            themeBtn.Glyph = Theme.Dark ? Glyph.Sun : Glyph.Moon;
            themeBtn.Text = Theme.Dark ? "Mode clair" : "Mode sombre";
            Invalidate(true);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (nav == null || themeBtn == null) return;
            int x = Theme.D(12), w = Width - Theme.D(24), h = Theme.D(44);
            for (int i = 0; i < nav.Length; i++) nav[i].SetBounds(x, Theme.D(100) + i * (h + Theme.D(4)), w, h);
            themeBtn.SetBounds(x, Height - Theme.D(112), w, h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.HQ(g); g.Clear(Theme.Side);
            int ls = Theme.D(36);
            g.DrawImage(LogoArt.Small(ls), Theme.D(22), Theme.D(28), ls, ls);
            int tx = Theme.D(22) + ls + Theme.D(12);
            Theme.Draw(g, "LNK", Theme.F(8.5f, true), new Rectangle(tx, Theme.D(27), Width - tx, Theme.D(16)), Theme.Accent, TextFormatFlags.Left);
            Theme.Draw(g, "Screen Manager", Theme.F(12f, true), new Rectangle(tx, Theme.D(42), Width - tx, Theme.D(24)), Theme.Text, TextFormatFlags.Left);
            Theme.Draw(g, Brand.By, Theme.F(8.5f, false), new Rectangle(Theme.D(24), Height - Theme.D(56), Width, Theme.D(18)), Theme.Faint, TextFormatFlags.Left);
            Theme.Draw(g, "Version " + Brand.Version, Theme.F(8.5f, false), new Rectangle(Theme.D(24), Height - Theme.D(38), Width, Theme.D(18)), Theme.Faint, TextFormatFlags.Left);
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, Width - 1, 0, Width - 1, Height);
        }
    }

    class NavButton : Smooth
    {
        public string Glyph;
        bool selected; float sel; bool anim;

        public NavButton(string glyph, string text) { Glyph = glyph; Text = text; Cursor = Cursors.Hand; Hoverable = true; }

        public void SetSelected(bool v)
        {
            if (selected == v) return;
            selected = v;
            if (!IsHandleCreated || !Visible) { sel = v ? 1 : 0; Invalidate(); return; }
            if (anim) return;
            anim = true;
            Anim.Run(() =>
            {
                if (IsDisposed) { anim = false; return true; }
                bool done = Anim.Approach(ref sel, selected ? 1 : 0, 0.3f);
                Invalidate();
                if (done) anim = false;
                return done;
            });
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.HQ(g); g.Clear(ParentBg);
            float bgT = Math.Max(sel, Hover * 0.7f);
            var r = new RectangleF(0, 0, Width, Height);
            if (bgT > 0) Theme.Fill(g, r, Theme.D(10), Color.FromArgb((int)(255 * bgT), Theme.Card));
            if (sel > 0)
            {
                float bh = Height * 0.45f * sel;
                Theme.Fill(g, new RectangleF(Theme.D(2), (Height - bh) / 2, Theme.D(3), bh), Theme.D(2), Theme.Accent);
            }
            var fg = Theme.Mix(Theme.Mix(Theme.Dim, Theme.Text, Hover), Theme.Text, sel);
            Theme.Draw(g, Glyph, Theme.IconF(13f), new Rectangle(Theme.D(14), 0, Theme.D(24), Height), sel > 0.5f ? Theme.Accent : fg, Theme.Center);
            Theme.Draw(g, Text, Theme.F(10.5f, selected), new Rectangle(Theme.D(50), 0, Width - Theme.D(56), Height), fg, Theme.Left);
        }
    }

    class StatusLine : Smooth
    {
        readonly TrayApp app;
        public StatusLine(TrayApp app) { this.app = app; Height = Theme.D(38); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Theme.Bg);
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, 0, 0, Width, 0);
            int pad = Theme.D(28);
            string left = string.IsNullOrEmpty(app.Status) ? "Prêt" : app.Status;
            string right = app.Summary + (Settings.AutoHdrGames ? "   ·   HDR auto jeux actif" : "");
            var f = Theme.F(9f, false);
            int rw = Theme.Measure(right, f).Width;
            Theme.Draw(g, left, f, new Rectangle(pad, 0, Width - 2 * pad - rw - Theme.D(20), Height), app.StatusError ? Theme.Danger : Theme.Dim, Theme.Left);
            Theme.Draw(g, right, f, new Rectangle(Width - pad - rw, 0, rw + 2, Height), Theme.Faint, Theme.Left);
        }
    }

    // ------------------------------------------------------------------ Base des pages
    abstract class PageBase : Panel
    {
        protected readonly TrayApp app;
        protected PageBase(TrayApp app)
        {
            this.app = app;
            DoubleBuffered = true; BackColor = Theme.Bg; AutoScroll = true;
        }

        public abstract void Render();
        /// Place les controles (si apply) et renvoie la hauteur du contenu.
        protected abstract int Arrange(int width, int height, bool apply);
        public int Needed(int width, int height) { return Arrange(width, height, false); }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Relayout(); }

        bool arranging;
        public void Relayout()
        {
            if (arranging || ClientSize.Width <= 0) return;
            arranging = true;
            SuspendLayout();
            // largeur pleine d'abord ; la barre de defilement n'est retiree de la largeur que si elle est vraiment necessaire
            int width = Width, height = Height;
            bool scroll = Arrange(width, height, false) > height;
            if (scroll) width -= SystemInformation.VerticalScrollBarWidth;
            AutoScrollMinSize = Size.Empty;
            int h = Arrange(width, height, true);
            AutoScrollMinSize = new Size(0, scroll ? h : 0);
            ResumeLayout();
            arranging = false;
        }

        protected void Place(Control c, int x, int y, int w, int h, bool apply) { if (apply) c.SetBounds(x, y + AutoScrollPosition.Y, w, h); }

        /// Zone de contenu centree, largeur plafonnee.
        protected static int Content(int width, int maxW, out int w)
        {
            int pad = Theme.D(28);
            w = Math.Max(Theme.D(200), Math.Min(width - 2 * pad, Theme.D(maxW)));
            return Math.Max(pad, (width - w) / 2);
        }

        public virtual void OnThemeChanged() { BackColor = Theme.Bg; }
    }

    /// Ligne de reglage : libelle, explication, controle a droite.
    class OptionRow : Smooth
    {
        public string Sub;
        public readonly Control Side;
        public OptionRow(string text, string sub, Control side)
        {
            Text = text; Sub = sub; Side = side; BackColor = Theme.Card;
            Height = Theme.D(sub == null ? 52 : 60);
            Controls.Add(side);
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            Side.Location = new Point(Width - Theme.D(16) - Side.Width, (Height - Side.Height) / 2);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.HQ(g); g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.Fill(g, r, Theme.D(10), Theme.Card);
            Theme.Stroke(g, r, Theme.D(10), Theme.Border, 1f, false);
            int w = Side.Left - Theme.D(34);
            if (Sub == null) Theme.Draw(g, Text, Theme.F(10.5f, false), new Rectangle(Theme.D(18), 0, w, Height), Theme.Text, Theme.Left);
            else
            {
                Theme.Draw(g, Text, Theme.F(10.5f, false), new Rectangle(Theme.D(18), Theme.D(10), w, Theme.D(20)), Theme.Text, TextFormatFlags.EndEllipsis);
                Theme.Draw(g, Sub, Theme.F(9f, false), new Rectangle(Theme.D(18), Theme.D(32), w, Theme.D(18)), Theme.Dim, TextFormatFlags.EndEllipsis);
            }
        }
        public void OnThemeChanged() { BackColor = Theme.Card; Invalidate(true); }
    }

    // ------------------------------------------------------------------ Page Ecrans
    class ScreensPage : PageBase
    {
        readonly PageTitle title = new PageTitle("Écrans", null);
        readonly Pill btnIdentify = new Pill("Identifier", Glyph.Identify), btnAllOn = new Pill("Tout allumer", Glyph.Power);
        readonly LayoutMap map = new LayoutMap();
        readonly SectionHeader secList = new SectionHeader("Moniteurs") { Columns = true }, secProfiles = new SectionHeader("Profils");
        readonly List<MonitorCard> cards = new List<MonitorCard>();
        readonly List<Pill> pills = new List<Pill>();
        readonly ContextMenuStrip menu = Menus.Create();
        readonly ToolTip tips = new ToolTip();
        string pillsSig;

        public ScreensPage(TrayApp app) : base(app)
        {
            Controls.AddRange(new Control[] { title, btnIdentify, btnAllOn, map, secList, secProfiles });
            btnIdentify.Click += (s, e) => app.Identify(null);
            btnAllOn.Click += (s, e) => app.AllOn();
            tips.SetToolTip(map, "Double-clic : allumer / éteindre");
            map.HoverChanged += (s, e) => { foreach (var c in cards) c.SetHighlight(c.Number == map.Hot); };
            map.ItemDoubleClicked += n => { var c = cards.FirstOrDefault(x => x.Number == n); if (c != null) app.ToggleScreen(c.M, !c.M.Active); };
            menu.Closed += (s, e) => BeginInvoke((Action)(() => Menus.Clear(menu)));
        }

        protected override void Dispose(bool disposing) { if (disposing) { menu.Dispose(); tips.Dispose(); } base.Dispose(disposing); }

        public override void Render()
        {
            var mons = app.Mons;
            if (string.Join("|", mons.Select(m => m.DevicePath)) != string.Join("|", cards.Select(c => c.M == null ? "" : c.M.DevicePath)))
            {
                foreach (var c in cards) { Controls.Remove(c); c.Dispose(); }
                cards.Clear();
                foreach (var m0 in mons)
                {
                    var card = new MonitorCard();
                    card.TgOn.Toggled += (s, e) => app.ToggleScreen(card.M, card.TgOn.Checked);
                    card.TgHdr.Toggled += (s, e) => app.SetHdr(card.M, card.TgHdr.Checked);
                    card.HoverChanged += (s, e) => map.SetHot(card.IsHover ? card.Number : -1);
                    card.RenameRequested += (s, e) => app.RenameScreen(card.M);
                    card.MenuRequested += pt => ShowScreenMenu(card, pt);
                    cards.Add(card); Controls.Add(card);
                }
            }

            var items = new List<LayoutMap.Item>();
            for (int i = 0; i < mons.Count; i++)
            {
                var m = mons[i];
                cards[i].SetData(m, i + 1, TrayApp.Detail(m));
                Rectangle r; int hz;
                if (LnkScreenManager.Layout.Bounds(m, out r, out hz)) items.Add(new LayoutMap.Item { M = m, Number = i + 1, R = r });
            }
            map.SetItems(items);

            string sig = string.Join("|", app.ProfileList.Select(p => p.Name + (app.Matches(p) ? "*" : "")));
            if (sig != pillsSig)
            {
                pillsSig = sig;
                foreach (var p in pills) { Controls.Remove(p); p.Dispose(); }
                pills.Clear();
                foreach (var p0 in app.ProfileList)
                {
                    var p = p0;
                    var pill = new Pill(p.Name) { Active = app.Matches(p) };
                    pill.Fit();
                    pill.MouseUp += (s, e) =>
                    {
                        if (e.Button == MouseButtons.Left) app.ApplyProfile(p);
                        else if (e.Button == MouseButtons.Right) ShowProfileMenu(pill, p, e.Location);
                    };
                    tips.SetToolTip(pill, "Clic : appliquer · clic droit : options");
                    pills.Add(pill); Controls.Add(pill);
                }
                var add = new Pill("Nouveau profil", Glyph.Add) { Dashed = true };
                add.Click += (s, e) => app.NewProfile();
                tips.SetToolTip(add, "Enregistrer l'état actuel des écrans comme profil");
                pills.Add(add); Controls.Add(add);
            }
            Relayout();
        }

        protected override int Arrange(int width, int height, bool apply)
        {
            int w; int x = Content(width, 900, out w);
            int y = Theme.D(20);
            Place(title, x, y, w - btnAllOn.Width - btnIdentify.Width - Theme.D(16), title.Height, apply);
            int by = y + (title.Height - btnAllOn.Height) / 2;
            Place(btnAllOn, x + w - btnAllOn.Width, by, btnAllOn.Width, btnAllOn.Height, apply);
            Place(btnIdentify, x + w - btnAllOn.Width - Theme.D(8) - btnIdentify.Width, by, btnIdentify.Width, btnIdentify.Height, apply);
            y += title.Height + Theme.D(12);

            if (apply) map.Visible = Settings.ShowMap;
            if (Settings.ShowMap) { Place(map, x, y, w, map.Height, apply); y += map.Height + Theme.D(20); }

            Place(secList, x, y, w, secList.Height, apply); y += secList.Height + Theme.D(4);
            foreach (var c in cards) { Place(c, x, y, w, c.Height, apply); y += c.Height + Theme.D(8); }
            y += Theme.D(14);

            Place(secProfiles, x, y, w, secProfiles.Height, apply); y += secProfiles.Height + Theme.D(6);
            int px = x, gap = Theme.D(8), ph = Theme.D(34);
            foreach (var p in pills)
            {
                if (px > x && px + p.Width > x + w) { px = x; y += ph + gap; }
                Place(p, px, y, p.Width, p.Height, apply); px += p.Width + gap;
            }
            y += ph + Theme.D(24);
            return y;
        }

        public override void OnThemeChanged() { base.OnThemeChanged(); Invalidate(true); }

        void ShowScreenMenu(MonitorCard card, Point pt)
        {
            var m = card.M;
            Menus.Clear(menu);
            menu.Items.Add(Menus.Header(m.Model));
            menu.Items.Add(Menus.Item("Renommer…", (s, e) => app.RenameScreen(m)));
            if (m.HasCustomName) menu.Items.Add(Menus.Item("Reprendre le nom d'origine", (s, e) => app.ResetScreenName(m)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Menus.Item(m.Active ? "Éteindre" : "Allumer", (s, e) => app.ToggleScreen(m, !m.Active)));
            if (m.Active && m.HdrSupported) menu.Items.Add(Menus.Item(m.HdrOn ? "Couper le HDR" : "Activer le HDR", (s, e) => app.SetHdr(m, !m.HdrOn)));
            if (m.Active) menu.Items.Add(Menus.Item("Identifier", (s, e) => app.Identify(m)));
            menu.Show(card, pt);
        }

        void ShowProfileMenu(Control anchor, Profile p, Point pt)
        {
            Menus.Clear(menu);
            menu.Items.Add(Menus.Header(p.Name));
            menu.Items.Add(Menus.Item("Appliquer", (s, e) => app.ApplyProfile(p)));
            menu.Items.Add(Menus.Item("Remplacer par l'état actuel", (s, e) => app.ReplaceProfile(p)));
            menu.Items.Add(Menus.Item("Raccourci sur le bureau", (s, e) => app.MakeShortcut(p.Name)));
            menu.Items.Add(Menus.Item("Renommer…", (s, e) => app.RenameProfile(p)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Menus.Danger("Supprimer", (s, e) => app.DeleteProfile(p)));
            menu.Show(anchor, pt);
        }
    }

    // ------------------------------------------------------------------ Page Jeux
    class LabeledToggle : Smooth
    {
        public readonly Toggle Tg = new Toggle();
        public LabeledToggle(string text)
        {
            Text = text; Height = Theme.D(34);
            Width = Theme.Measure(text, Theme.F(10.5f, true)).Width + Theme.D(14) + Tg.Width;
            Controls.Add(Tg);
        }
        protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); Tg.Location = new Point(Width - Tg.Width, (Height - Tg.Height) / 2); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(ParentBg);
            Theme.Draw(g, Text, Theme.F(10.5f, true), new Rectangle(0, 0, Tg.Left - Theme.D(10), Height), Theme.Text, Theme.Left);
        }
    }

    class GameTile : Smooth
    {
        public readonly Game G;
        public readonly Toggle Tg = new Toggle();
        public event Action<Point> MenuRequested;

        public GameTile(Game g)
        {
            G = g; Hoverable = true; Height = Theme.D(GamesPage.TileH);
            Tg.Checked = g.AutoHdr;
            Tg.Under = () => TileColor;
            Controls.Add(Tg);
            TrackChild(Tg);
        }

        Color TileColor { get { return Theme.Mix(Theme.Card, Theme.CardHover, Hover); } }

        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (e.Button == MouseButtons.Right && MenuRequested != null) MenuRequested(e.Location); }
        protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); Tg.Location = new Point(Width - Theme.D(14) - Tg.Width, (Height - Tg.Height) / 2); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.HQ(g); g.Clear(ParentBg);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.Fill(g, r, Theme.D(10), TileColor);
            Theme.Stroke(g, r, Theme.D(10), G.AutoHdr ? Theme.Mix(Theme.Border, Theme.Accent, 0.55f) : Theme.Border, 1f, false);
            int w = Tg.Left - Theme.D(26);
            Theme.Draw(g, G.Name, Theme.F(10f, true), new Rectangle(Theme.D(14), Theme.D(9), w, Theme.D(20)), Theme.Text, TextFormatFlags.EndEllipsis);
            string sub = G.Source + "  ·  " + G.WikiLabel + (G.User >= 0 ? "  ·  manuel" : "");
            Theme.Draw(g, sub, Theme.F(8.5f, false), new Rectangle(Theme.D(14), Theme.D(30), w, Theme.D(18)), G.WikiHdr ? Theme.Accent : Theme.Dim, TextFormatFlags.EndEllipsis);
        }
    }

    class GamesPage : PageBase
    {
        public const int TileH = 56, TileMinW = 220, Gap = 8;
        readonly PageTitle title = new PageTitle("Jeux", "Le HDR s'active au lancement d'un jeu compatible et revient comme avant à sa fermeture.");
        readonly LabeledToggle master = new LabeledToggle("HDR auto");
        readonly Segmented filter = new Segmented("Tous (00)", "HDR auto (00)", "À vérifier (00)");
        readonly Pill add = new Pill("Ajouter un jeu", Glyph.Add) { Dashed = true };
        readonly IconButton rescan = new IconButton(Glyph.Refresh);
        readonly List<GameTile> tiles = new List<GameTile>();
        readonly ContextMenuStrip menu = Menus.Create();
        readonly ToolTip tips = new ToolTip();
        string sig;

        public GamesPage(TrayApp app) : base(app)
        {
            Controls.AddRange(new Control[] { title, master, filter, add, rescan });
            master.Tg.Toggled += (s, e) => { Settings.AutoHdrGames = master.Tg.Checked; app.ApplyGameSettings(); app.SetStatus(master.Tg.Checked ? "HDR auto activé pour les jeux." : "HDR auto désactivé.", false); };
            filter.SelectedChanged += (s, e) => Relayout();
            add.Click += (s, e) => AddGame();
            rescan.Click += (s, e) => { app.RescanGames(); app.SetStatus("Jeux actualisés" + (Settings.WikiLookup ? ", recherche des infos HDR…" : "."), false); };
            tips.SetToolTip(rescan, "Rechercher de nouveaux jeux");
            menu.Closed += (s, e) => BeginInvoke((Action)(() => Menus.Clear(menu)));
        }

        protected override void Dispose(bool disposing) { if (disposing) { menu.Dispose(); tips.Dispose(); } base.Dispose(disposing); }

        static bool ToCheck(Game g) { return !g.AutoHdr && g.User < 0 && !(g.Wiki == "false" || g.Wiki == "hackable" || g.Wiki == "n/a"); }

        public override void Render()
        {
            var all = Games.All;
            master.Tg.Checked = Settings.AutoHdrGames;
            string s = string.Join("|", all.Select(g => g.Key + g.AutoHdr + g.Wiki + g.User));
            if (s != sig)
            {
                sig = s;
                foreach (var t in tiles) { Controls.Remove(t); t.Dispose(); }
                tiles.Clear();
                foreach (var g0 in all.OrderByDescending(x => x.AutoHdr).ThenByDescending(x => x.WikiHdr).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    var g = g0;
                    var tile = new GameTile(g);
                    tile.Tg.Toggled += (o, e) => { g.User = tile.Tg.Checked ? 1 : 0; Games.Save(); app.GamesChanged(); };
                    tile.MenuRequested += pt => ShowGameMenu(tile, pt);
                    tips.SetToolTip(tile, g.Name + (g.Folder != null ? "\n" + g.Folder : g.Exe != null ? "\n" + g.Exe : ""));
                    tiles.Add(tile); Controls.Add(tile);
                }
                filter.SetLabel(0, "Tous (" + all.Count + ")");
                filter.SetLabel(1, "HDR auto (" + all.Count(x => x.AutoHdr) + ")");
                filter.SetLabel(2, "À vérifier (" + all.Count(ToCheck) + ")");
            }
            Relayout();
        }

        IEnumerable<GameTile> Filtered()
        {
            if (filter.Selected == 1) return tiles.Where(t => t.G.AutoHdr);
            if (filter.Selected == 2) return tiles.Where(t => ToCheck(t.G));
            return tiles;
        }

        protected override int Arrange(int width, int height, bool apply)
        {
            int w; int x = Content(width, 4000, out w);
            int y = Theme.D(20);
            Place(title, x, y, w - master.Width - Theme.D(16), title.Height, apply);
            Place(master, x + w - master.Width, y + Theme.D(4), master.Width, master.Height, apply);
            y += title.Height + Theme.D(10);
            Place(filter, x, y, filter.Width, filter.Height, apply);
            Place(rescan, x + w - rescan.Width, y, rescan.Width, rescan.Height, apply);
            Place(add, x + w - rescan.Width - Theme.D(8) - add.Width, y, add.Width, add.Height, apply);
            y += filter.Height + Theme.D(16);

            var vis = Filtered().ToList();
            int n = apply ? vis.Count : tiles.Count;
            int gap = Theme.D(Gap), th = Theme.D(TileH);
            int cols = Math.Max(1, Math.Min(Math.Max(1, (w + gap) / (Theme.D(TileMinW) + gap)), Math.Max(n, 1)));
            int colW = (w - (cols - 1) * gap) / cols;
            int rows = (n + cols - 1) / cols;
            if (apply)
            {
                foreach (var t in tiles) t.Visible = false;
                for (int i = 0; i < vis.Count; i++)
                {
                    var t = vis[i];
                    Place(t, x + (i % cols) * (colW + gap), y + (i / cols) * (th + gap), colW, th, true);
                    t.Visible = true;
                }
            }
            if (n == 0 && apply) { }
            y += Math.Max(0, rows * (th + gap) - gap) + Theme.D(22);
            return y;
        }

        public override void OnThemeChanged() { base.OnThemeChanged(); Invalidate(true); }

        void ShowGameMenu(GameTile tile, Point pt)
        {
            var g = tile.G;
            Menus.Clear(menu);
            menu.Items.Add(Menus.Header(g.Name));
            menu.Items.Add(Menus.Item("Raccourci « Jouer en HDR » sur le bureau", (s, e) => app.MakePlayShortcut(g)));
            string dir = g.Folder ?? (g.Exe != null ? Path.GetDirectoryName(g.Exe) : null);
            if (dir != null) menu.Items.Add(Menus.Item("Ouvrir le dossier du jeu", (s, e) => { try { Process.Start("explorer.exe", "\"" + dir + "\""); } catch { } }));
            if (g.User >= 0 && g.Wiki != null)
                menu.Items.Add(Menus.Item("Suivre PCGamingWiki (" + g.WikiLabel + ")", (s, e) => { g.User = -1; Games.Save(); app.GamesChanged(); }));
            if (g.Manual)
            {
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(Menus.Danger("Retirer de la liste", (s, e) => { Games.Remove(g); app.GamesChanged(); }));
            }
            menu.Show(tile, pt);
        }

        void AddGame()
        {
            using (var d = new OpenFileDialog { Title = "Choisir l'exécutable du jeu", Filter = "Programmes (*.exe)|*.exe" })
            {
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                var g = Games.AddManual(d.FileName);
                app.GamesChanged();
                app.SetStatus("« " + g.Name + " » ajouté en HDR auto.", false);
            }
        }
    }

    // ------------------------------------------------------------------ Page Reglages
    class SettingsPage : PageBase
    {
        readonly PageTitle title = new PageTitle("Réglages", null);
        readonly List<Tuple<SectionHeader, List<OptionRow>>> left = new List<Tuple<SectionHeader, List<OptionRow>>>(), right = new List<Tuple<SectionHeader, List<OptionRow>>>();
        readonly Segmented segTheme = new Segmented("Système", "Sombre", "Clair");
        readonly Toggle tgTray = new Toggle(), tgBoot = new Toggle(), tgNotif = new Toggle(), tgMap = new Toggle(), tgAuto = new Toggle(), tgWiki = new Toggle();
        readonly Pill pScreen = new Pill("Écran du jeu");
        readonly ContextMenuStrip menu = Menus.Create();

        public SettingsPage(TrayApp app) : base(app)
        {
            Controls.Add(title);
            Group(left, "Apparence", Row("Thème", "Système = comme Windows", segTheme));
            Group(left, "Fenêtre et démarrage",
                Row("Réduire près de l'heure", "Le bouton X garde l'appli près de l'horloge", tgTray),
                Row("Lancer avec Windows", "Démarre discrètement près de l'horloge", tgBoot),
                Row("Notifications", "Bulle lors d'un changement fait hors fenêtre", tgNotif),
                Row("Mini-carte des écrans", "Affiche la disposition sur la page Écrans", tgMap));
            Group(right, "HDR des jeux",
                Row("HDR auto pour les jeux", "Au lancement d'un jeu compatible", tgAuto),
                Row("Écran à passer en HDR", null, pScreen),
                Row("Infos HDR en ligne", "PCGamingWiki · seul l'identifiant Steam est envoyé", tgWiki));
            var pAll = new Pill("Allumer"); var pHdr = new Pill("Couper"); var pData = new Pill("Ouvrir"); var pQuit = new Pill("Quitter");
            Group(right, "Actions",
                Row("Allumer tous les écrans", null, pAll),
                Row("Couper le HDR partout", null, pHdr),
                Row("Dossier des données", "Profils, noms, réglages", pData));
            Group(right, "À propos", Row(Brand.Name + " " + Brand.Version, Brand.By + " · Windows 10 / 11", pQuit));

            segTheme.SelectedChanged += (s, e) =>
            {
                Settings.ThemeMode = segTheme.Selected == 1 ? "dark" : segTheme.Selected == 2 ? "light" : "system";
                Theme.ApplyMode(Settings.ThemeMode);
            };
            tgTray.Toggled += (s, e) => Settings.TrayOnClose = tgTray.Checked;
            tgBoot.Toggled += (s, e) =>
            {
                try { Startup.Enabled = tgBoot.Checked; app.SetStatus(tgBoot.Checked ? "Lancement avec Windows activé." : "Lancement avec Windows désactivé.", false); }
                catch (Exception ex) { app.SetStatus("Impossible : " + ex.Message, true); tgBoot.Checked = Startup.Enabled; }
            };
            tgNotif.Toggled += (s, e) => Settings.NotifyChanges = tgNotif.Checked;
            tgMap.Toggled += (s, e) => { Settings.ShowMap = tgMap.Checked; app.SetStatus(tgMap.Checked ? "Mini-carte affichée." : "Mini-carte masquée.", false); };
            tgAuto.Toggled += (s, e) => { Settings.AutoHdrGames = tgAuto.Checked; app.ApplyGameSettings(); app.SetStatus(tgAuto.Checked ? "HDR auto activé pour les jeux." : "HDR auto désactivé.", false); };
            tgWiki.Toggled += (s, e) => { Settings.WikiLookup = tgWiki.Checked; if (tgWiki.Checked) app.LookupGames(); };
            pScreen.Click += (s, e) => ShowScreenMenu();
            pAll.Click += (s, e) => app.AllOn();
            pHdr.Click += (s, e) => app.AllHdrOff();
            pData.Click += (s, e) => { try { Process.Start("explorer.exe", "\"" + AppData.Dir + "\""); } catch { } };
            pQuit.Click += (s, e) => app.Exit();
            menu.Closed += (s, e) => BeginInvoke((Action)(() => Menus.Clear(menu)));
        }

        protected override void Dispose(bool disposing) { if (disposing) menu.Dispose(); base.Dispose(disposing); }

        OptionRow Row(string text, string sub, Control side) { var r = new OptionRow(text, sub, side); Controls.Add(r); return r; }
        void Group(List<Tuple<SectionHeader, List<OptionRow>>> col, string name, params OptionRow[] rows)
        {
            var h = new SectionHeader(name); Controls.Add(h);
            col.Add(Tuple.Create(h, rows.ToList()));
        }

        public override void Render()
        {
            string mode = Settings.ThemeMode;
            segTheme.Selected = mode == "dark" ? 1 : mode == "light" ? 2 : 0;
            tgTray.Checked = Settings.TrayOnClose; tgBoot.Checked = Startup.Enabled; tgNotif.Checked = Settings.NotifyChanges;
            tgMap.Checked = Settings.ShowMap; tgAuto.Checked = Settings.AutoHdrGames; tgWiki.Checked = Settings.WikiLookup;
            string t = "Écran du jeu";
            if (!string.IsNullOrEmpty(Settings.HdrTarget))
            {
                var m = app.Mons.FirstOrDefault(x => x.DevicePath == Settings.HdrTarget);
                t = m != null ? m.Name : "Écran débranché";
            }
            if (pScreen.Text != t) { pScreen.Text = t; pScreen.Fit(); if (pScreen.Parent != null) pScreen.Parent.PerformLayout(); }
            Relayout();
        }

        protected override int Arrange(int width, int height, bool apply)
        {
            int w; int x = Content(width, 1000, out w);
            int y = Theme.D(20);
            Place(title, x, y, w, title.Height, apply);
            y += title.Height + Theme.D(10);
            int gap = Theme.D(20);
            if (w >= Theme.D(700))
            {
                int cw = (w - gap) / 2;
                int yl = Column(left, x, y, cw, apply), yr = Column(right, x + cw + gap, y, cw, apply);
                return Math.Max(yl, yr) + Theme.D(10);
            }
            y = Column(left, x, y, w, apply);
            return Column(right, x, y, w, apply) + Theme.D(10);
        }

        int Column(List<Tuple<SectionHeader, List<OptionRow>>> groups, int x, int y, int w, bool apply)
        {
            foreach (var gr in groups)
            {
                Place(gr.Item1, x, y, w, gr.Item1.Height, apply); y += gr.Item1.Height + Theme.D(4);
                foreach (var r in gr.Item2) { Place(r, x, y, w, r.Height, apply); y += r.Height + Theme.D(6); }
                y += Theme.D(14);
            }
            return y;
        }

        public override void OnThemeChanged()
        {
            base.OnThemeChanged();
            foreach (var gr in left.Concat(right)) foreach (var r in gr.Item2) r.OnThemeChanged();
            Render();
            Invalidate(true);
        }

        void ShowScreenMenu()
        {
            Menus.Clear(menu);
            var it = Menus.Item("Écran où s'affiche le jeu", (s, e) => { Settings.HdrTarget = ""; Render(); });
            it.Checked = string.IsNullOrEmpty(Settings.HdrTarget);
            menu.Items.Add(it);
            menu.Items.Add(new ToolStripSeparator());
            foreach (var m0 in app.Mons.Where(m => m.HdrSupported || m.DevicePath == Settings.HdrTarget))
            {
                var m = m0;
                var i = Menus.Item(app.Number(m) + "   " + m.Name, (s, e) => { Settings.HdrTarget = m.DevicePath; Render(); });
                i.Checked = m.DevicePath == Settings.HdrTarget;
                menu.Items.Add(i);
            }
            menu.Show(pScreen, new Point(0, pScreen.Height + Theme.D(2)));
        }
    }
}
