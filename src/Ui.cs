// LNK Screen Manager - theme (sombre / clair), animations legeres, logo et controles de base.
// By LNK Co (c) 2026
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LnkScreenManager
{
    static class Brand
    {
        public const string Name = "LNK Screen Manager";
        public const string By = "By LNK Co © 2026";
        public const string Version = "1.5";
    }

    // ------------------------------------------------------------------ Theme
    static class Theme
    {
        public static float S = 1f;
        public static int D(float px) { return (int)Math.Round(px * S); }

        /// Change l'echelle (100 % = 1). Les polices sont recreees a la nouvelle taille.
        public static void SetScale(float s)
        {
            if (s < 0.75f) s = 0.75f;
            if (Math.Abs(s - S) < 0.001f) return;
            S = s; fonts.Clear();
        }

        public static bool Dark = true;
        public static Color Bg, Side, Card, CardHover, Border, Text, Dim, Faint, Accent, AccentHover, Danger, Track;
        public static readonly Color Brand1 = Color.FromArgb(124, 92, 255), Brand2 = Color.FromArgb(34, 193, 238);
        public static event Action Changed;

        static Theme() { Set(true); }

        static void Set(bool dark)
        {
            Dark = dark;
            if (dark)
            {
                Bg = C(0x121418); Side = C(0x0D0F12); Card = C(0x1B1F25); CardHover = C(0x232830); Border = C(0x2F343D);
                Text = C(0xEDEFF2); Dim = C(0xA8AEB7); Faint = C(0x767D87); Accent = C(0x4F8CFF); AccentHover = C(0x6B9EFF);
                Danger = C(0xF27474); Track = C(0x8A909A);
            }
            else
            {
                Bg = C(0xF4F5F7); Side = C(0xE8EAEE); Card = C(0xFFFFFF); CardHover = C(0xEEF2F8); Border = C(0xD8DCE2);
                Text = C(0x181B1F); Dim = C(0x515965); Faint = C(0x7F8792); Accent = C(0x2F6FEB); AccentHover = C(0x4A82EE);
                Danger = C(0xD24343); Track = C(0x7F8792);
            }
        }

        public static void Apply(bool dark) { if (dark == Dark) return; Set(dark); if (Changed != null) Changed(); }

        /// Mode : "system" (suit Windows), "dark", "light".
        public static void ApplyMode(string mode) { Apply(mode == "light" ? false : mode == "dark" ? true : SystemDark); }

        public static bool SystemDark
        {
            get
            {
                try
                {
                    var v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0);
                    return !(v is int) || (int)v == 0;
                }
                catch { return true; }
            }
        }

        static Color C(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        static string iconFamily;
        public static string IconFamily
        {
            get
            {
                if (iconFamily == null)
                    using (var f = new Font("Segoe Fluent Icons", 10f)) iconFamily = f.Name == "Segoe Fluent Icons" ? f.Name : "Segoe MDL2 Assets";
                return iconFamily;
            }
        }

        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        public static Font F(float pt, bool semibold) { return Face(semibold ? "Segoe UI Semibold" : "Segoe UI", pt); }
        public static Font IconF(float pt) { return Face(IconFamily, pt); }
        public static Font Face(string family, float pt)
        {
            string k = family + "|" + pt; Font f;
            if (!fonts.TryGetValue(k, out f)) { f = new Font(family, pt * 96f / 72f * S, FontStyle.Regular, GraphicsUnit.Pixel); fonts[k] = f; }
            return f;
        }

        /// Police a une echelle donnee (non mise en cache : a liberer par l'appelant).
        public static Font FontAt(float pt, bool semibold, float scale)
        {
            return new Font(semibold ? "Segoe UI Semibold" : "Segoe UI", pt * 96f / 72f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Fill(Graphics g, RectangleF r, float rad, Color c) { using (var p = Round(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p); }
        public static void Stroke(Graphics g, RectangleF r, float rad, Color c, float w, bool dash)
        {
            using (var p = Round(r, rad)) using (var pen = new Pen(c, w)) { if (dash) pen.DashStyle = DashStyle.Dash; g.DrawPath(pen, p); }
        }

        public static void HQ(Graphics g) { g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; }

        public static void Draw(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, s, f, r, c, flags | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        public static Size Measure(string s, Font f)
        {
            return TextRenderer.MeasureText(s, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        public const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
        public const TextFormatFlags Left = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
    }

    static class Glyph
    {
        public const string Settings = "", Identify = "", Check = "", Edit = "", Game = "",
            Monitor = "", Sun = "", Moon = "", Refresh = "", Add = "", Power = "";
    }

    // ------------------------------------------------------------------ Animations : un seul minuteur, actif uniquement pendant un mouvement
    static class Anim
    {
        static readonly Timer timer = new Timer { Interval = 15 };
        static readonly List<Func<bool>> steps = new List<Func<bool>>();

        static Anim()
        {
            timer.Tick += (s, e) =>
            {
                for (int i = steps.Count - 1; i >= 0; i--)
                {
                    bool done;
                    try { done = steps[i](); } catch { done = true; }
                    if (done) steps.RemoveAt(i);
                }
                if (steps.Count == 0) timer.Stop();
            };
        }

        /// step() renvoie true quand l'animation est terminee.
        public static void Run(Func<bool> step) { steps.Add(step); if (!timer.Enabled) timer.Start(); }

        /// Rapproche une valeur de sa cible ; renvoie true quand elle est atteinte.
        public static bool Approach(ref float v, float target, float speed)
        {
            v += (target - v) * speed;
            if (Math.Abs(target - v) < 0.02f) { v = target; return true; }
            return false;
        }
    }

    static class Win
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("shcore.dll")] static extern int SetProcessDpiAwareness(int value);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr hmon, int type, out uint x, out uint y);
        [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point pt, uint flags);

        /// Net sur chaque ecran, meme a des echelles differentes (Per-Monitor v2, sinon v1, sinon systeme).
        public static void EnableDpiAwareness()
        {
            try { if (SetProcessDpiAwarenessContext((IntPtr)(-4))) return; } catch { }
            try { if (SetProcessDpiAwareness(2) == 0) return; } catch { }
            try { SetProcessDPIAware(); } catch { }
        }

        public static int SystemDpi { get { using (var g = Graphics.FromHwnd(IntPtr.Zero)) return (int)g.DpiX; } }

        public static int DpiAt(Point p)
        {
            try { uint x, y; if (GetDpiForMonitor(MonitorFromPoint(p, 2), 0, out x, out y) == 0 && x > 0) return (int)x; } catch { }
            return SystemDpi;
        }

        public static int DpiOf(Screen s) { var b = s.Bounds; return DpiAt(new Point(b.X + b.Width / 2, b.Y + b.Height / 2)); }

        public static int DpiOfWindow(IntPtr h)
        {
            try { uint d = GetDpiForWindow(h); if (d > 0) return (int)d; } catch { }
            return SystemDpi;
        }
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] static extern bool SetProcessWorkingSetSize(IntPtr p, IntPtr min, IntPtr max);

        /// Barre de titre assortie au theme + coins arrondis (Windows 11).
        public static void Style(IntPtr h)
        {
            try
            {
                int dark = Theme.Dark ? 1 : 0; DwmSetWindowAttribute(h, 20, ref dark, 4);
                var c = Theme.Bg; int col = c.R | (c.G << 8) | (c.B << 16); DwmSetWindowAttribute(h, 35, ref col, 4);
                int text = Theme.Text.R | (Theme.Text.G << 8) | (Theme.Text.B << 16); DwmSetWindowAttribute(h, 36, ref text, 4);
                int corner = 2; DwmSetWindowAttribute(h, 33, ref corner, 4);
            }
            catch { }
        }

        /// Rend la memoire inutilisee a Windows (quand l'appli n'est plus qu'une icone pres de l'heure).
        public static void Trim()
        {
            try
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                SetProcessWorkingSetSize(GetCurrentProcess(), (IntPtr)(-1), (IntPtr)(-1));
            }
            catch { }
        }
    }

    // ------------------------------------------------------------------ Logo (vectoriel ; sert aussi a generer l'icone)
    static class LogoArt
    {
        static readonly Dictionary<int, Bitmap> small = new Dictionary<int, Bitmap>();
        public static Bitmap Small(int s) { Bitmap b; if (!small.TryGetValue(s, out b)) { b = Render(s); small[s] = b; } return b; }

        public static Bitmap Render(int s)
        {
            var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                Theme.HQ(g); g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                Draw(g, new RectangleF(0, 0, s, s));
            }
            return bmp;
        }

        public static void Draw(Graphics g, RectangleF r)
        {
            float s = r.Width;
            var bg = RectangleF.Inflate(r, -s * 0.03f, -s * 0.03f);
            using (var path = Theme.Round(bg, s * 0.24f))
            {
                using (var br = new LinearGradientBrush(bg, Theme.Brand1, Theme.Brand2, 45f)) g.FillPath(br, path);
                var top = new RectangleF(bg.X, bg.Y, bg.Width, bg.Height * 0.55f);
                using (var hl = new LinearGradientBrush(new RectangleF(top.X, top.Y - 1, top.Width, top.Height + 2), Color.FromArgb(55, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                {
                    var old = g.Clip; g.SetClip(path); g.FillRectangle(hl, top); g.Clip = old;
                }
            }
            float sw = Math.Max(1.4f, s * 0.062f);
            var scr = new RectangleF(r.X + s * 0.2f, r.Y + s * 0.23f, s * 0.6f, s * 0.41f);
            using (var sp = Theme.Round(scr, s * 0.07f))
            {
                if (s < 30) g.FillPath(Brushes.White, sp);
                else
                {
                    using (var fill = new SolidBrush(Color.FromArgb(50, 255, 255, 255))) g.FillPath(fill, sp);
                    using (var pen = new Pen(Color.White, sw)) g.DrawPath(pen, sp);
                }
            }
            float nx = r.X + s * 0.5f, ty = scr.Bottom + (s < 30 ? 0 : sw * 0.5f);
            g.FillRectangle(Brushes.White, nx - s * 0.045f, ty, s * 0.09f, s * 0.085f);
            using (var bp = Theme.Round(new RectangleF(nx - s * 0.19f, ty + s * 0.08f, s * 0.38f, s * 0.065f), s * 0.032f)) g.FillPath(Brushes.White, bp);
            if (s >= 40)
                using (var f = new Font("Segoe UI Black", scr.Height * 0.40f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("LNK", f, Brushes.White, new RectangleF(scr.X, scr.Y + scr.Height * 0.04f, scr.Width, scr.Height), sf);
        }

        public static void SaveIco(string path)
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            var pngs = new List<byte[]>();
            foreach (int sz in sizes)
                using (var b = Render(sz)) using (var ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
            using (var fs = File.Create(path)) using (var w = new BinaryWriter(fs))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    byte d = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                    w.Write(d); w.Write(d); w.Write((byte)0); w.Write((byte)0);
                    w.Write((short)1); w.Write((short)32); w.Write(pngs[i].Length); w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (var p in pngs) w.Write(p);
            }
        }

        public static Icon AppIcon(int size)
        {
            try
            {
                using (var st = typeof(LogoArt).Assembly.GetManifestResourceStream("LnkScreenManager.app.ico"))
                    if (st != null) return new Icon(st, size, size);
            }
            catch { }
            using (var b = Render(size)) return Icon.FromHandle(b.GetHicon());
        }
    }

    // ------------------------------------------------------------------ Controles de base
    /// Controle dessine a la main, double tampon, avec survol anime (optionnel).
    class Smooth : Control
    {
        protected float Hover;
        bool hoverTarget, hoverAnim;
        protected bool Hoverable;

        public Smooth()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.StandardDoubleClick | ControlStyles.Selectable, false);
            TabStop = false;
        }

        protected Color ParentBg { get { return Parent != null ? Parent.BackColor : Theme.Bg; } }

        protected void AnimateHover(bool on)
        {
            hoverTarget = on;
            if (hoverAnim) return;
            hoverAnim = true;
            Anim.Run(() =>
            {
                if (IsDisposed) { hoverAnim = false; return true; }
                bool done = Anim.Approach(ref Hover, hoverTarget ? 1 : 0, 0.3f);
                Invalidate(true);
                if (done) hoverAnim = false;
                return done;
            });
        }

        /// Le survol d'un enfant (interrupteur...) compte comme survol du parent.
        protected void TrackChild(Control c)
        {
            c.MouseEnter += (s, e) => AnimateHover(true);
            c.MouseLeave += (s, e) => { if (!ClientRectangle.Contains(PointToClient(Cursor.Position))) AnimateHover(false); };
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); if (Hoverable) AnimateHover(true); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (Hoverable && !ClientRectangle.Contains(PointToClient(Cursor.Position))) AnimateHover(false); }
    }

    /// Interrupteur (style Windows 11), glissement anime.
    class Toggle : Smooth
    {
        bool on; float pos; bool anim;
        public Func<Color> Under;
        public event EventHandler Toggled;

        public Toggle() { Size = new Size(Theme.D(42), Theme.D(22)); Cursor = Cursors.Hand; Hoverable = true; }

        public bool Checked { get { return on; } set { if (on == value) return; on = value; Slide(); } }

        void Slide()
        {
            if (!IsHandleCreated || !Visible) { pos = on ? 1 : 0; Invalidate(); return; }
            if (anim) return;
            anim = true;
            Anim.Run(() =>
            {
                if (IsDisposed) { anim = false; return true; }
                bool done = Anim.Approach(ref pos, on ? 1 : 0, 0.35f);
                Invalidate();
                if (done) anim = false;
                return done;
            });
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (!Enabled) return;
            on = !on; Slide();
            if (Toggled != null) Toggled(this, EventArgs.Empty);
        }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.HQ(g); g.Clear(Under != null ? Under() : ParentBg);
            var r = new RectangleF(1, 1, Width - 2.5f, Height - 2.5f);
            float rad = r.Height / 2;
            if (!Enabled)
            {
                Theme.Stroke(g, r, rad, Theme.Border, 1f, false);
                float k0 = r.Height - Theme.D(10);
                using (var b = new SolidBrush(Theme.Border)) g.FillEllipse(b, r.X + Theme.D(5), r.Y + (r.Height - k0) / 2, k0, k0);
                return;
            }
            var acc = Hover > 0 ? Theme.Mix(Theme.Accent, Theme.AccentHover, Hover) : Theme.Accent;
            if (pos > 0) Theme.Fill(g, r, rad, Color.FromArgb((int)(255 * pos), acc));
            if (pos < 1) Theme.Stroke(g, r, rad, Color.FromArgb((int)(255 * (1 - pos)), Theme.Track), Theme.S, false);
            float k = r.Height - Theme.D(10) + Theme.D(2) * Math.Max(Hover, pos);
            float x = r.X + Theme.D(5) + (r.Width - k - Theme.D(10)) * pos;
            using (var b = new SolidBrush(Theme.Mix(Theme.Track, Color.White, pos))) g.FillEllipse(b, x, r.Y + (r.Height - k) / 2, k, k);
        }
    }

    /// Bouton icone (barre d'outils).
    class IconButton : Smooth
    {
        public string Glyph;
        public IconButton(string glyph) { Glyph = glyph; Size = new Size(Theme.D(36), Theme.D(36)); Cursor = Cursors.Hand; Hoverable = true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.HQ(g); g.Clear(ParentBg);
            if (Hover > 0) Theme.Fill(g, new RectangleF(0, 0, Width, Height), Theme.D(8), Color.FromArgb((int)(255 * Hover), Theme.CardHover));
            Theme.Draw(g, Glyph, Theme.IconF(12f), ClientRectangle, Theme.Mix(Theme.Dim, Theme.Text, Hover), Theme.Center);
        }
    }

    /// Bouton texte : normal, principal (accent), pointille (ajout), ou actif (selection).
    class Pill : Smooth
    {
        public bool Active, Dashed, Primary;
        public string Glyph;
        public Pill(string text) : this(text, null) { }
        public Pill(string text, string glyph) { Text = text; Glyph = glyph; Cursor = Cursors.Hand; Hoverable = true; Fit(); }

        public void Fit()
        {
            int w = Theme.Measure(Text, Theme.F(10f, false)).Width + Theme.D(32) + (Glyph != null ? Theme.D(22) : 0);
            Size = new Size(Math.Max(w, Theme.D(70)), Theme.D(34));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.HQ(g); g.Clear(ParentBg);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float rad = Theme.D(8);
            Color tc;
            if (Primary) { Theme.Fill(g, r, rad, Theme.Mix(Theme.Accent, Theme.AccentHover, Hover)); tc = Color.White; }
            else if (Dashed)
            {
                if (Hover > 0) Theme.Fill(g, r, rad, Color.FromArgb((int)(255 * Hover), Theme.Card));
                Theme.Stroke(g, r, rad, Theme.Mix(Theme.Border, Theme.Faint, Hover), Theme.S, true);
                tc = Theme.Mix(Theme.Dim, Theme.Text, Hover);
            }
            else
            {
                Theme.Fill(g, r, rad, Theme.Mix(Theme.Card, Theme.CardHover, Hover));
                Theme.Stroke(g, r, rad, Active ? Theme.Accent : Theme.Border, Active ? Theme.D(1.5f) : 1f, false);
                tc = Active ? Theme.Accent : Theme.Text;
            }
            int x = Theme.D(16), w = Width - Theme.D(32);
            if (Glyph != null)
            {
                var gs = Theme.Measure(Text, Theme.F(10f, false)).Width + Theme.D(22);
                x = (Width - gs) / 2;
                Theme.Draw(g, Glyph, Theme.IconF(10f), new Rectangle(x, 0, Theme.D(16), Height), tc, Theme.Center);
                x += Theme.D(22); w = Width - x - Theme.D(10);
                Theme.Draw(g, Text, Theme.F(10f, Active), new Rectangle(x, 0, w, Height), tc, Theme.Left);
            }
            else Theme.Draw(g, Text, Theme.F(10f, Active), new Rectangle(0, 0, Width, Height), tc, Theme.Center | TextFormatFlags.EndEllipsis);
        }
    }

    /// Choix exclusif (ex. Systeme / Sombre / Clair, filtres).
    class Segmented : Smooth
    {
        readonly string[] items;
        int selected, hot = -1;
        float slide; bool anim;
        public event EventHandler SelectedChanged;

        public Segmented(params string[] items)
        {
            this.items = items; Cursor = Cursors.Hand; Height = Theme.D(34);
            Recalc();
        }

        void Recalc()
        {
            int w = Theme.D(8);
            foreach (var s in items) w += Theme.Measure(s, Theme.F(9.5f, true)).Width + Theme.D(28);
            Width = w;
        }

        public string[] Items { get { return items; } }
        public int Selected
        {
            get { return selected; }
            set { if (value == selected) return; selected = value; Glide(); }
        }
        public void SetLabel(int i, string text) { if (items[i] == text) return; items[i] = text; Recalc(); Invalidate(); if (Parent != null) Parent.PerformLayout(); }

        void Glide()
        {
            if (!IsHandleCreated || !Visible) { slide = selected; Invalidate(); return; }
            if (anim) return;
            anim = true;
            Anim.Run(() =>
            {
                if (IsDisposed) { anim = false; return true; }
                bool done = Anim.Approach(ref slide, selected, 0.3f);
                Invalidate();
                if (done) anim = false;
                return done;
            });
        }

        RectangleF Seg(float i)
        {
            float w = (Width - Theme.D(8)) / (float)items.Length;
            return new RectangleF(Theme.D(4) + i * w, Theme.D(4), w, Height - Theme.D(8));
        }

        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int h = Hit(e.X); if (h != hot) { hot = h; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = -1; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            int h = Hit(e.X);
            if (e.Button == MouseButtons.Left && h >= 0 && h != selected) { Selected = h; if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty); }
        }
        int Hit(int x) { for (int i = 0; i < items.Length; i++) if (Seg(i).Contains(x, Height / 2)) return i; return -1; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.HQ(g); g.Clear(ParentBg);
            Theme.Fill(g, new RectangleF(0, 0, Width, Height), Theme.D(9), Theme.Dark ? Theme.Side : Theme.Side);
            var sel = Seg(slide);
            Theme.Fill(g, sel, Theme.D(7), Theme.Card);
            Theme.Stroke(g, sel, Theme.D(7), Theme.Border, 1f, false);
            for (int i = 0; i < items.Length; i++)
            {
                var r = Rectangle.Round(Seg(i));
                Color c = i == selected ? Theme.Text : (i == hot ? Theme.Text : Theme.Dim);
                Theme.Draw(g, items[i], Theme.F(9.5f, i == selected), r, c, Theme.Center | TextFormatFlags.EndEllipsis);
            }
        }
    }

    /// Titre de section ; pour la liste des ecrans, affiche aussi les colonnes ECRAN / HDR.
    class SectionHeader : Smooth
    {
        public bool Columns;
        public SectionHeader(string text) { Text = text.ToUpperInvariant(); Height = Theme.D(28); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(ParentBg);
            var f = Theme.F(8.5f, true);
            Theme.Draw(g, Text, f, new Rectangle(Theme.D(2), 0, Width, Height), Theme.Faint, TextFormatFlags.VerticalCenter);
            if (!Columns) return;
            int tw = Theme.D(42);
            Theme.Draw(g, L.T("ÉCRAN"), f, new Rectangle(Width - Theme.D(MonitorCard.ColOn) - Theme.D(10), 0, tw + Theme.D(20), Height), Theme.Faint, Theme.Center);
            Theme.Draw(g, "HDR", f, new Rectangle(Width - Theme.D(MonitorCard.ColHdr) - Theme.D(10), 0, tw + Theme.D(20), Height), Theme.Faint, Theme.Center);
        }
    }

    /// Titre de page (grand) + sous-titre.
    class PageTitle : Smooth
    {
        public string Sub;
        public PageTitle(string title, string sub) { Text = title; Sub = sub; Height = Theme.D(sub == null ? 48 : 66); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(ParentBg);
            Theme.Draw(g, Text, Theme.F(17f, true), new Rectangle(0, 0, Width, Theme.D(40)), Theme.Text, TextFormatFlags.VerticalCenter);
            if (Sub != null) Theme.Draw(g, Sub, Theme.F(9.5f, false), new Rectangle(Theme.D(1), Theme.D(40), Width, Theme.D(22)), Theme.Dim, Theme.Left);
        }
    }

    /// Ligne d'un ecran : numero, nom, infos, interrupteurs.
    class MonitorCard : Smooth
    {
        public const int ColOn = 132, ColHdr = 62; // distance bord droit -> interrupteur (px a 100 %)
        public Monitor M; public int Number;
        public bool Highlight;
        string detail = "", sig;
        Rectangle nameZone = Rectangle.Empty;
        public readonly Toggle TgOn = new Toggle(), TgHdr = new Toggle();
        public event EventHandler HoverChanged, RenameRequested;
        public event Action<Point> MenuRequested;
        bool over;

        public MonitorCard()
        {
            Height = Theme.D(64); Hoverable = true;
            TgOn.Under = TgHdr.Under = () => CardColor;
            Controls.Add(TgOn); Controls.Add(TgHdr);
            TrackChild(TgOn); TrackChild(TgHdr);
            MouseEnter += (s, e) => SetOver(true);
            MouseLeave += (s, e) => { if (!ClientRectangle.Contains(PointToClient(Cursor.Position))) SetOver(false); };
            foreach (var t in new[] { TgOn, TgHdr })
            {
                t.MouseEnter += (s, e) => SetOver(true);
                t.MouseLeave += (s, e) => { if (!ClientRectangle.Contains(PointToClient(Cursor.Position))) SetOver(false); };
            }
        }

        public bool IsHover { get { return over; } }
        void SetOver(bool v) { if (over == v) return; over = v; if (HoverChanged != null) HoverChanged(this, EventArgs.Empty); }
        public void SetHighlight(bool v) { if (Highlight == v) return; Highlight = v; AnimateHover(v || over); }

        Color CardColor { get { return Theme.Mix(Theme.Card, Theme.CardHover, Hover); } }

        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); Cursor = nameZone.Contains(e.Location) ? Cursors.Hand : Cursors.Default; }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && nameZone.Contains(e.Location) && RenameRequested != null) RenameRequested(this, EventArgs.Empty);
            if (e.Button == MouseButtons.Right && MenuRequested != null) MenuRequested(e.Location);
        }

        public void SetData(Monitor m, int number, string det)
        {
            string s = number + "|" + m.Name + "|" + det + "|" + m.Active + m.Primary + m.HdrSupported + m.HdrOn;
            M = m; Number = number; detail = det;
            TgOn.Checked = m.Active;
            TgHdr.Checked = m.Active && m.HdrOn;
            TgHdr.Enabled = m.Active && m.HdrSupported;
            TgHdr.Visible = !(m.Active && !m.HdrSupported);
            if (s != sig) { sig = s; Invalidate(); }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int y = (Height - TgOn.Height) / 2;
            TgOn.Location = new Point(Width - Theme.D(ColOn), y);
            TgHdr.Location = new Point(Width - Theme.D(ColHdr), y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (M == null) return;
            var g = e.Graphics; Theme.HQ(g); g.Clear(ParentBg);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.Fill(g, r, Theme.D(10), CardColor);
            Theme.Stroke(g, r, Theme.D(10), Highlight ? Theme.Accent : Theme.Border, Highlight ? Theme.D(1.5f) : 1f, false);

            // numero dans une pastille
            int bs = Theme.D(30);
            var br = new Rectangle(Theme.D(16), (Height - bs) / 2, bs, bs);
            Theme.Fill(g, br, Theme.D(8), M.Active ? Theme.Mix(Theme.Accent, CardColor, 0.82f) : Theme.Mix(Theme.Border, CardColor, 0.4f));
            Theme.Draw(g, Number.ToString(), Theme.F(10.5f, true), br, M.Active ? Theme.Accent : Theme.Faint, Theme.Center);

            int x = Theme.D(60), maxRight = Width - Theme.D(ColOn) - Theme.D(16);
            var nf = Theme.F(11f, true);
            var ns = Theme.Measure(M.Name, nf);
            int penW = Theme.D(22);
            int nameW = Math.Max(0, Math.Min(ns.Width, maxRight - x - penW));
            int ny = Height / 2 - Theme.D(21);
            Theme.Draw(g, M.Name, nf, new Rectangle(x, ny, nameW + 2, ns.Height + 2), M.Active ? Theme.Text : Theme.Dim, TextFormatFlags.EndEllipsis);
            var penRect = new Rectangle(x + nameW + Theme.D(4), ny, Theme.D(18), ns.Height);
            nameZone = Rectangle.Union(new Rectangle(x, ny, nameW, ns.Height), penRect);
            if (Hover > 0.05f) Theme.Draw(g, Glyph.Edit, Theme.IconF(9f), penRect, Color.FromArgb((int)(255 * Hover), Theme.Faint), Theme.Center);

            Theme.Draw(g, detail, Theme.F(9.5f, false), new Rectangle(x, Height / 2 + Theme.D(3), maxRight - x, Theme.D(20)), Theme.Dim, TextFormatFlags.EndEllipsis);

            if (M.Active && !M.HdrSupported)
                Theme.Draw(g, "—", Theme.F(10f, false), new Rectangle(Width - Theme.D(ColHdr), 0, TgHdr.Width, Height), Theme.Faint, Theme.Center);
        }
    }

    /// Mini-carte de la disposition des ecrans.
    class LayoutMap : Smooth
    {
        public class Item { public Monitor M; public int Number; public Rectangle R; }
        List<Item> items = new List<Item>();
        string sig = "";
        public int Hot = -1;
        public event EventHandler HoverChanged;
        public event Action<int> ItemDoubleClicked;
        readonly List<KeyValuePair<int, RectangleF>> drawn = new List<KeyValuePair<int, RectangleF>>();

        public LayoutMap() { Height = Theme.D(150); }

        public void SetItems(List<Item> list)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var it in list) sb.Append(it.Number).Append(it.R).Append(it.M.Active).Append(it.M.HdrOn).Append(it.M.Name).Append('|');
            items = list;
            if (sb.ToString() != sig) { sig = sb.ToString(); Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.HQ(g); g.Clear(ParentBg);
            var outer = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.Fill(g, outer, Theme.D(10), Theme.Card);
            Theme.Stroke(g, outer, Theme.D(10), Theme.Border, 1f, false);
            drawn.Clear();
            if (items.Count == 0) return;

            var bb = items[0].R;
            foreach (var it in items) bb = Rectangle.Union(bb, it.R);
            float pad = Theme.D(16);
            float sc = Math.Min((Width - 2 * pad) / bb.Width, (Height - 2 * pad) / bb.Height);
            float ox = (Width - bb.Width * sc) / 2 - bb.X * sc, oy = (Height - bb.Height * sc) / 2 - bb.Y * sc;
            float gap = Theme.D(3);
            Color screen = Theme.Dark ? Theme.Border : Theme.Side;

            foreach (var it in items)
            {
                var rf = new RectangleF(ox + it.R.X * sc + gap, oy + it.R.Y * sc + gap, it.R.Width * sc - 2 * gap, it.R.Height * sc - 2 * gap);
                drawn.Add(new KeyValuePair<int, RectangleF>(it.Number, rf));
                bool hv = it.Number == Hot;
                float rad = Theme.D(6);
                if (it.M.Active) Theme.Fill(g, rf, rad, hv ? Theme.Mix(screen, Theme.Accent, 0.18f) : screen);
                else Theme.Stroke(g, rf, rad, hv ? Theme.Dim : Theme.Faint, Theme.S, true);
                if (hv) Theme.Stroke(g, rf, rad, Theme.Accent, Theme.D(2), false);
                bool showName = rf.Height > Theme.D(46) && rf.Width > Theme.D(80);
                var numR = Rectangle.Round(new RectangleF(rf.X, rf.Y, rf.Width, showName ? rf.Height * 0.62f : rf.Height));
                Theme.Draw(g, it.Number.ToString(), Theme.F(13f, true), numR, it.M.Active ? Theme.Text : Theme.Faint, showName ? (TextFormatFlags.HorizontalCenter | TextFormatFlags.Bottom) : Theme.Center);
                if (showName)
                    Theme.Draw(g, it.M.Name, Theme.F(8.5f, false), Rectangle.Round(new RectangleF(rf.X + 4, rf.Y + rf.Height * 0.62f + 2, rf.Width - 8, Theme.D(18))), Theme.Dim, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
                if (it.M.Active && it.M.HdrOn)
                {
                    var tf = Theme.F(7f, true); var ts = Theme.Measure("HDR", tf);
                    var tr = new Rectangle((int)rf.Right - ts.Width - Theme.D(16), (int)rf.Y + Theme.D(6), ts.Width + Theme.D(10), Theme.D(16));
                    Theme.Fill(g, tr, tr.Height / 2f, Theme.Accent);
                    Theme.Draw(g, "HDR", tf, tr, Color.White, Theme.Center);
                }
            }
        }

        int HitTest(Point pt)
        {
            for (int i = drawn.Count - 1; i >= 0; i--) if (drawn[i].Value.Contains(pt)) return drawn[i].Key;
            return -1;
        }

        public void SetHot(int n) { if (Hot != n) { Hot = n; Invalidate(); } }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitTest(e.Location);
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
            if (h != Hot) { Hot = h; Invalidate(); if (HoverChanged != null) HoverChanged(this, EventArgs.Empty); }
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (Hot != -1) { Hot = -1; Invalidate(); if (HoverChanged != null) HoverChanged(this, EventArgs.Empty); }
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int h = HitTest(e.Location);
            if (h >= 0 && ItemDoubleClicked != null) ItemDoubleClicked(h);
        }
    }

    // ------------------------------------------------------------------ Menus assortis au theme
    class ThemeColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color MenuItemBorder { get { return Theme.CardHover; } }
        public override Color MenuItemSelected { get { return Theme.CardHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.CardHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.CardHover; } }
        public override Color MenuItemPressedGradientBegin { get { return Theme.CardHover; } }
        public override Color MenuItemPressedGradientEnd { get { return Theme.CardHover; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Card; } }
        public override Color CheckBackground { get { return Theme.Card; } }
        public override Color CheckSelectedBackground { get { return Theme.CardHover; } }
        public override Color CheckPressedBackground { get { return Theme.CardHover; } }
        public override Color ButtonSelectedBorder { get { return Theme.CardHover; } }
    }

    class ThemeRenderer : ToolStripProfessionalRenderer
    {
        public static readonly ThemeRenderer Instance = new ThemeRenderer();
        ThemeRenderer() : base(new ThemeColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var tag = e.Item.Tag as string;
            e.TextColor = tag == "header" ? Theme.Faint : tag == "danger" ? Theme.Danger : (e.Item.Enabled ? Theme.Text : Theme.Faint);
            base.OnRenderItemText(e);
        }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor = Theme.Dim; base.OnRenderArrow(e); }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var r = e.ImageRectangle; r.Inflate(Theme.D(3), Theme.D(3));
            Theme.Draw(e.Graphics, Glyph.Check, Theme.IconF(9f), r, Theme.Accent, Theme.Center);
        }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var pen = new Pen(Theme.Border)) e.Graphics.DrawLine(pen, Theme.D(8), y, e.Item.Width - Theme.D(8), y);
        }
    }

    static class Menus
    {
        public static ContextMenuStrip Create()
        {
            var m = new ContextMenuStrip { Renderer = ThemeRenderer.Instance, Font = Theme.F(10f, false), ShowImageMargin = true, ShowCheckMargin = false };
            m.ImageScalingSize = new Size(Theme.D(16), Theme.D(16));
            return m;
        }

        public static ToolStripMenuItem Item(string text, EventHandler click)
        {
            var i = new ToolStripMenuItem(text) { Padding = new Padding(0, Theme.D(3), 0, Theme.D(3)) };
            if (click != null) i.Click += click;
            return i;
        }

        public static ToolStripMenuItem Danger(string text, EventHandler click) { var i = Item(text, click); i.Tag = "danger"; return i; }

        public static ToolStripMenuItem Header(string text)
        {
            return new ToolStripMenuItem(text.ToUpperInvariant()) { Enabled = false, Tag = "header", Font = Theme.F(8f, true) };
        }

        public static ToolStripMenuItem Sub(string text)
        {
            var i = Item(text, null);
            i.DropDown.Renderer = ThemeRenderer.Instance; i.DropDown.Font = Theme.F(10f, false);
            return i;
        }

        public static void Clear(ToolStrip m)
        {
            var old = new List<ToolStripItem>();
            foreach (ToolStripItem i in m.Items) old.Add(i);
            m.Items.Clear();
            foreach (var i in old) i.Dispose();
        }
    }

    // ------------------------------------------------------------------ Dialogues
    class ThemedForm : Form
    {
        public ThemedForm()
        {
            BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.F(10f, false);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent; KeyPreview = true; AutoScaleMode = AutoScaleMode.None;
            Icon = LogoArt.AppIcon(Theme.D(16));
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Win.Style(Handle); }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } }
    }

    static class Dialogs
    {
        public static string Prompt(IWin32Window owner, string title, string text, string def)
        {
            using (var f = new ThemedForm { Text = title, ClientSize = new Size(Theme.D(440), Theme.D(170)) })
            {
                if (owner == null) { f.StartPosition = FormStartPosition.CenterScreen; f.TopMost = true; f.ShowInTaskbar = true; }
                var l = new Label { Text = text, Location = new Point(Theme.D(22), Theme.D(20)), AutoSize = true, ForeColor = Theme.Dim, BackColor = Theme.Bg };
                var t = new TextBox { Text = def ?? "", Location = new Point(Theme.D(22), Theme.D(52)), Width = Theme.D(396), BackColor = Theme.Card, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = Theme.F(11f, false) };
                var ok = new Pill(L.T("Enregistrer")) { Primary = true };
                var cancel = new Pill(L.T("Annuler"));
                ok.Location = new Point(f.ClientSize.Width - Theme.D(22) - ok.Width, Theme.D(114));
                cancel.Location = new Point(ok.Left - Theme.D(8) - cancel.Width, Theme.D(114));
                ok.Click += (s, e) => { f.DialogResult = DialogResult.OK; f.Close(); };
                cancel.Click += (s, e) => { f.DialogResult = DialogResult.Cancel; f.Close(); };
                t.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; f.DialogResult = DialogResult.OK; f.Close(); } };
                f.Controls.AddRange(new Control[] { l, t, ok, cancel });
                f.Shown += (s, e) => { t.Focus(); t.SelectAll(); };
                return f.ShowDialog(owner) == DialogResult.OK ? t.Text.Trim() : null;
            }
        }

        public static bool Confirm(IWin32Window owner, string title, string text, string yes)
        {
            using (var f = new ThemedForm { Text = title, ClientSize = new Size(Theme.D(420), Theme.D(140)) })
            {
                if (owner == null) { f.StartPosition = FormStartPosition.CenterScreen; f.TopMost = true; }
                var l = new Label { Text = text, Location = new Point(Theme.D(22), Theme.D(24)), Size = new Size(Theme.D(376), Theme.D(50)), ForeColor = Theme.Text, BackColor = Theme.Bg };
                var ok = new Pill(yes) { Primary = true };
                var cancel = new Pill(L.T("Annuler"));
                ok.Location = new Point(f.ClientSize.Width - Theme.D(22) - ok.Width, Theme.D(86));
                cancel.Location = new Point(ok.Left - Theme.D(8) - cancel.Width, Theme.D(86));
                ok.Click += (s, e) => { f.DialogResult = DialogResult.OK; f.Close(); };
                cancel.Click += (s, e) => { f.DialogResult = DialogResult.Cancel; f.Close(); };
                f.Controls.AddRange(new Control[] { l, ok, cancel });
                return f.ShowDialog(owner) == DialogResult.OK;
            }
        }
    }

    /// Grand numero affiche au centre d'un ecran ("Identifier").
    class IdentifyForm : Form
    {
        readonly int number; readonly string name; readonly float k;
        readonly Font big, small;
        readonly Timer timer = new Timer { Interval = 2500 };

        public IdentifyForm(Rectangle screen, int number, string name, float scale)
        {
            this.number = number; this.name = name; k = scale;
            big = Theme.FontAt(64f, true, k); small = Theme.FontAt(11f, false, k);
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Bg; AutoScaleMode = AutoScaleMode.None; DoubleBuffered = true;
            var sz = new Size((int)(260 * k), (int)(190 * k));
            Bounds = new Rectangle(screen.X + (screen.Width - sz.Width) / 2, screen.Y + (screen.Height - sz.Height) / 2, sz.Width, sz.Height);
            timer.Tick += (s, e) => Close(); timer.Start();
            Click += (s, e) => Close();
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Win.Style(Handle); }
        protected override void OnFormClosed(FormClosedEventArgs e) { timer.Dispose(); big.Dispose(); small.Dispose(); base.OnFormClosed(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            int b = (int)(3 * k);
            using (var pen = new Pen(Theme.Accent, b)) g.DrawRectangle(pen, b / 2, b / 2, Width - b, Height - b);
            Theme.Draw(g, number.ToString(), big, new Rectangle(0, (int)(8 * k), Width, (int)(130 * k)), Theme.Text, Theme.Center);
            Theme.Draw(g, name, small, new Rectangle((int)(10 * k), (int)(140 * k), Width - (int)(20 * k), (int)(32 * k)), Theme.Dim, Theme.Center | TextFormatFlags.EndEllipsis);
        }
    }
}
