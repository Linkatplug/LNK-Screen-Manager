// LNK Screen Manager - theme sobre, logo et controles.
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

namespace LnkScreenManager
{
    static class Brand
    {
        public const string Name = "LNK Screen Manager";
        public const string By = "By LNK Co © 2026";
        public const string Version = "1.1";
    }

    // ------------------------------------------------------------------ Theme (plat, une seule couleur d'accent)
    static class Theme
    {
        public static float S = 1f;
        public static int D(float px) { return (int)Math.Round(px * S); }

        public static readonly Color
            Bg = Color.FromArgb(17, 19, 23), Card = Color.FromArgb(25, 28, 33), CardHover = Color.FromArgb(32, 36, 42),
            Border = Color.FromArgb(44, 48, 56), Text = Color.FromArgb(232, 234, 237), Dim = Color.FromArgb(154, 160, 166),
            Faint = Color.FromArgb(98, 103, 110), Accent = Color.FromArgb(79, 140, 255), Danger = Color.FromArgb(242, 110, 110),
            Brand1 = Color.FromArgb(124, 92, 255), Brand2 = Color.FromArgb(34, 193, 238); // logo uniquement

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
            if (!fonts.TryGetValue(k, out f)) { f = new Font(family, pt, FontStyle.Regular, GraphicsUnit.Point); fonts[k] = f; }
            return f;
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

        public static void HQ(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        public static void Draw(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, s, f, r, c, flags | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        public static Size Measure(string s, Font f)
        {
            return TextRenderer.MeasureText(s, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        public const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
    }

    static class Glyph
    {
        public const string Settings = "", Identify = "", Check = "", Edit = "";
    }

    static class Win
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] static extern bool SetProcessWorkingSetSize(IntPtr p, IntPtr min, IntPtr max);

        /// Barre de titre sombre + coins arrondis (Windows 11).
        public static void Style(IntPtr h, Color caption)
        {
            try
            {
                int on = 1; DwmSetWindowAttribute(h, 20, ref on, 4);
                int c = caption.R | (caption.G << 8) | (caption.B << 16); DwmSetWindowAttribute(h, 35, ref c, 4);
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

        /// Icone integree a l'exe (taille choisie), ou dessinee a la volee si absente.
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

    // ------------------------------------------------------------------ Controles (dessines a la main, sans animation)
    class Smooth : Control
    {
        public Smooth()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.StandardDoubleClick | ControlStyles.Selectable, false);
            TabStop = false;
        }
        protected Color ParentBg { get { return Parent != null ? Parent.BackColor : Theme.Bg; } }
    }

    class Toggle : Smooth
    {
        bool on;
        public event EventHandler Toggled;
        public Toggle() { Size = new Size(Theme.D(38), Theme.D(20)); Cursor = Cursors.Hand; }

        public bool Checked { get { return on; } set { if (on != value) { on = value; Invalidate(); } } }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (!Enabled) return;
            on = !on; Invalidate();
            if (Toggled != null) Toggled(this, EventArgs.Empty);
        }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.HQ(g); g.Clear(BackColor);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            bool lit = on && Enabled;
            if (lit) Theme.Fill(g, r, r.Height / 2, Theme.Accent);
            else Theme.Stroke(g, r, r.Height / 2, Enabled ? Theme.Faint : Theme.Border, 1f, false);
            float k = r.Height - Theme.D(8), y = r.Y + (r.Height - k) / 2;
            float x = lit ? r.Right - Theme.D(4) - k : r.X + Theme.D(4);
            using (var b = new SolidBrush(lit ? Color.White : (Enabled ? Theme.Dim : Theme.Border))) g.FillEllipse(b, x, y, k, k);
        }
    }

    class IconButton : Smooth
    {
        readonly string glyph; bool hover;
        public IconButton(string glyph) { this.glyph = glyph; Size = new Size(Theme.D(34), Theme.D(34)); Cursor = Cursors.Hand; }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.HQ(g); g.Clear(ParentBg);
            if (hover) Theme.Fill(g, new RectangleF(0, 0, Width, Height), Theme.D(6), Theme.Card);
            Theme.Draw(g, glyph, Theme.IconF(11f), ClientRectangle, hover ? Theme.Text : Theme.Dim, Theme.Center);
        }
    }

    /// Bouton texte (profils, dialogues).
    class Pill : Smooth
    {
        bool hover;
        public bool Active, Dashed, Primary;
        public Pill(string text) { Text = text; Cursor = Cursors.Hand; Fit(); }

        public void Fit()
        {
            int w = Theme.Measure(Text, Theme.F(9.5f, false)).Width + Theme.D(28);
            Size = new Size(Math.Max(w, Theme.D(64)), Theme.D(30));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.HQ(g); g.Clear(ParentBg);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float rad = Theme.D(6);
            if (Primary) Theme.Fill(g, r, rad, hover ? Color.FromArgb(104, 157, 255) : Theme.Accent);
            else if (Dashed) { if (hover) Theme.Fill(g, r, rad, Theme.Card); Theme.Stroke(g, r, rad, Theme.Border, 1f, true); }
            else { Theme.Fill(g, r, rad, hover ? Theme.CardHover : Theme.Card); if (Active) Theme.Stroke(g, r, rad, Theme.Accent, 1f, false); }
            Color tc = Primary ? Color.White : Active ? Theme.Accent : Dashed ? (hover ? Theme.Text : Theme.Dim) : Theme.Text;
            Theme.Draw(g, Text, Theme.F(9.5f, false), ClientRectangle, tc, Theme.Center | TextFormatFlags.EndEllipsis);
        }
    }

    /// Titre de section ; pour la liste des ecrans, affiche aussi les colonnes ECRAN / HDR une seule fois.
    class SectionHeader : Smooth
    {
        public bool Columns;
        public SectionHeader(string text) { Text = text.ToUpperInvariant(); Height = Theme.D(26); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(ParentBg);
            var f = Theme.F(8f, true);
            Theme.Draw(g, Text, f, new Rectangle(0, 0, Width, Height), Theme.Faint, TextFormatFlags.VerticalCenter);
            if (!Columns) return;
            int tw = Theme.D(38);
            Theme.Draw(g, "ÉCRAN", f, new Rectangle(Width - Theme.D(MonitorCard.ColOn) - Theme.D(10), 0, tw + Theme.D(20), Height), Theme.Faint, Theme.Center);
            Theme.Draw(g, "HDR", f, new Rectangle(Width - Theme.D(MonitorCard.ColHdr) - Theme.D(10), 0, tw + Theme.D(20), Height), Theme.Faint, Theme.Center);
        }
    }

    /// Ligne d'un ecran : numero, nom, infos, interrupteurs.
    class MonitorCard : Smooth
    {
        public const int ColOn = 124, ColHdr = 58; // distance bord droit -> interrupteur (px a 100 %)
        public Monitor M; public int Number; public bool Highlight;
        string detail = "", sig;
        bool hover;
        Rectangle nameZone = Rectangle.Empty;
        public readonly Toggle TgOn = new Toggle(), TgHdr = new Toggle();
        public event EventHandler HoverChanged, RenameRequested;
        public event Action<Point> MenuRequested;

        public MonitorCard()
        {
            Height = Theme.D(54);
            Controls.Add(TgOn); Controls.Add(TgHdr);
            foreach (var t in new[] { TgOn, TgHdr })
            {
                t.MouseEnter += (s, e) => SetHover(true);
                t.MouseLeave += (s, e) => CheckLeave();
            }
        }

        public bool IsHover { get { return hover; } }
        void SetHover(bool v) { if (hover == v) return; hover = v; Repaint(); if (HoverChanged != null) HoverChanged(this, EventArgs.Empty); }
        void CheckLeave() { if (!ClientRectangle.Contains(PointToClient(Cursor.Position))) SetHover(false); }
        protected override void OnMouseEnter(EventArgs e) { SetHover(true); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { CheckLeave(); base.OnMouseLeave(e); }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); Cursor = nameZone.Contains(e.Location) ? Cursors.Hand : Cursors.Default; }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && nameZone.Contains(e.Location) && RenameRequested != null) RenameRequested(this, EventArgs.Empty);
            if (e.Button == MouseButtons.Right && MenuRequested != null) MenuRequested(e.Location);
        }

        public void Repaint()
        {
            var bg = (hover || Highlight) ? Theme.CardHover : Theme.Card;
            TgOn.BackColor = bg; TgHdr.BackColor = bg;
            Invalidate(true);
        }

        /// Met a jour la ligne ; ne redessine que si quelque chose a change.
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
            bool hl = hover || Highlight;
            Theme.Fill(g, new RectangleF(0, 0, Width, Height), Theme.D(8), hl ? Theme.CardHover : Theme.Card);
            if (Highlight) Theme.Stroke(g, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Theme.D(8), Theme.Accent, 1f, false);

            Theme.Draw(g, Number.ToString(), Theme.F(10f, true), new Rectangle(Theme.D(8), 0, Theme.D(26), Height), M.Active ? Theme.Dim : Theme.Faint, Theme.Center);

            int x = Theme.D(42), maxRight = Width - Theme.D(ColOn) - Theme.D(14);
            var nf = Theme.F(10.5f, true);
            var ns = Theme.Measure(M.Name, nf);
            int penW = Theme.D(22);
            int nameW = Math.Max(0, Math.Min(ns.Width, maxRight - x - penW));
            int ny = Theme.D(8);
            Theme.Draw(g, M.Name, nf, new Rectangle(x, ny, nameW + 2, ns.Height + 2), M.Active ? Theme.Text : Theme.Dim, TextFormatFlags.EndEllipsis);
            var penRect = new Rectangle(x + nameW + Theme.D(4), ny, Theme.D(18), ns.Height);
            nameZone = Rectangle.Union(new Rectangle(x, ny, nameW, ns.Height), penRect);
            if (hl) Theme.Draw(g, Glyph.Edit, Theme.IconF(8.5f), penRect, Theme.Faint, Theme.Center);

            Theme.Draw(g, detail, Theme.F(8.5f, false), new Rectangle(x, Theme.D(30), maxRight - x, Theme.D(18)), Theme.Faint, TextFormatFlags.EndEllipsis);

            if (M.Active && !M.HdrSupported)
                Theme.Draw(g, "—", Theme.F(9f, false), new Rectangle(Width - Theme.D(ColHdr), 0, TgHdr.Width, Height), Theme.Faint, Theme.Center);
        }
    }

    /// Mini-carte de la disposition des ecrans.
    class LayoutMap : Smooth
    {
        public class Item { public Monitor M; public int Number; public Rectangle R; }
        List<Item> items = new List<Item>();
        string sig = "";
        public int Hover = -1;
        public event EventHandler HoverChanged;
        public event Action<int> ItemDoubleClicked;
        readonly List<KeyValuePair<int, RectangleF>> drawn = new List<KeyValuePair<int, RectangleF>>();

        public LayoutMap() { Height = Theme.D(112); }

        public void SetItems(List<Item> list)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var it in list) sb.Append(it.Number).Append(it.R).Append(it.M.Active).Append(it.M.HdrOn).Append('|');
            items = list;
            if (sb.ToString() != sig) { sig = sb.ToString(); Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.HQ(g); g.Clear(ParentBg);
            Theme.Fill(g, new RectangleF(0, 0, Width, Height), Theme.D(8), Theme.Card);
            drawn.Clear();
            if (items.Count == 0) return;

            var bb = items[0].R;
            foreach (var it in items) bb = Rectangle.Union(bb, it.R);
            float pad = Theme.D(14);
            float sc = Math.Min((Width - 2 * pad) / bb.Width, (Height - 2 * pad) / bb.Height);
            float ox = (Width - bb.Width * sc) / 2 - bb.X * sc, oy = (Height - bb.Height * sc) / 2 - bb.Y * sc;
            float gap = Theme.D(2);
            var nf = Theme.F(9.5f, true);

            foreach (var it in items)
            {
                var rf = new RectangleF(ox + it.R.X * sc + gap, oy + it.R.Y * sc + gap, it.R.Width * sc - 2 * gap, it.R.Height * sc - 2 * gap);
                drawn.Add(new KeyValuePair<int, RectangleF>(it.Number, rf));
                bool hv = it.Number == Hover;
                float rad = Theme.D(4);
                if (it.M.Active) Theme.Fill(g, rf, rad, hv ? Theme.Faint : Theme.Border);
                else Theme.Stroke(g, rf, rad, hv ? Theme.Dim : Theme.Faint, 1f, true);
                if (hv) Theme.Stroke(g, rf, rad, Theme.Accent, Theme.D(1.5f), false);
                Theme.Draw(g, it.Number.ToString(), nf, Rectangle.Round(rf), it.M.Active ? Theme.Text : Theme.Faint, Theme.Center);
                if (it.M.Active && it.M.HdrOn)
                    using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, rf.Right - Theme.D(10), rf.Y + Theme.D(5), Theme.D(5), Theme.D(5));
            }
        }

        int HitTest(Point pt)
        {
            for (int i = drawn.Count - 1; i >= 0; i--) if (drawn[i].Value.Contains(pt)) return drawn[i].Key;
            return -1;
        }

        public void SetHover(int n) { if (Hover != n) { Hover = n; Invalidate(); } }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitTest(e.Location);
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
            if (h != Hover) { Hover = h; Invalidate(); if (HoverChanged != null) HoverChanged(this, EventArgs.Empty); }
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (Hover != -1) { Hover = -1; Invalidate(); if (HoverChanged != null) HoverChanged(this, EventArgs.Empty); }
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int h = HitTest(e.Location);
            if (h >= 0 && ItemDoubleClicked != null) ItemDoubleClicked(h);
        }
    }

    // ------------------------------------------------------------------ Menus sombres
    class DarkColors : ProfessionalColorTable
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

    class DarkRenderer : ToolStripProfessionalRenderer
    {
        public static readonly DarkRenderer Instance = new DarkRenderer();
        DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = (e.Item.Tag as string) == "header" ? Theme.Faint : (e.Item.Enabled ? (e.Item.ForeColor == Theme.Danger ? Theme.Danger : Theme.Text) : Theme.Faint);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor = Theme.Dim; base.OnRenderArrow(e); }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var r = e.ImageRectangle; r.Inflate(Theme.D(3), Theme.D(3));
            Theme.Draw(e.Graphics, Glyph.Check, Theme.IconF(8.5f), r, Theme.Accent, Theme.Center);
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
            var m = new ContextMenuStrip { Renderer = DarkRenderer.Instance, Font = Theme.F(9.5f, false), ShowImageMargin = true, ShowCheckMargin = false };
            m.ImageScalingSize = new Size(Theme.D(16), Theme.D(16));
            return m;
        }

        public static ToolStripMenuItem Item(string text, EventHandler click)
        {
            var i = new ToolStripMenuItem(text) { Padding = new Padding(0, Theme.D(2), 0, Theme.D(2)) };
            if (click != null) i.Click += click;
            return i;
        }

        public static ToolStripMenuItem Header(string text)
        {
            return new ToolStripMenuItem(text.ToUpperInvariant()) { Enabled = false, Tag = "header", Font = Theme.F(7.5f, true) };
        }

        public static ToolStripMenuItem Sub(string text)
        {
            var i = Item(text, null);
            i.DropDown.Renderer = DarkRenderer.Instance; i.DropDown.Font = Theme.F(9.5f, false);
            return i;
        }

        /// Vide un menu en liberant ses elements.
        public static void Clear(ToolStrip m)
        {
            var old = new List<ToolStripItem>();
            foreach (ToolStripItem i in m.Items) old.Add(i);
            m.Items.Clear();
            foreach (var i in old) i.Dispose();
        }
    }

    // ------------------------------------------------------------------ Dialogues
    class DarkForm : Form
    {
        public DarkForm()
        {
            BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.F(9.5f, false);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent; KeyPreview = true; AutoScaleMode = AutoScaleMode.None;
            Icon = LogoArt.AppIcon(Theme.D(16));
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Win.Style(Handle, Theme.Bg); }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } }
    }

    static class Dialogs
    {
        public static string Prompt(IWin32Window owner, string title, string text, string def)
        {
            using (var f = new DarkForm { Text = title, ClientSize = new Size(Theme.D(400), Theme.D(150)) })
            {
                if (owner == null) { f.StartPosition = FormStartPosition.CenterScreen; f.TopMost = true; f.ShowInTaskbar = true; }
                var l = new Label { Text = text, Location = new Point(Theme.D(20), Theme.D(18)), AutoSize = true, ForeColor = Theme.Dim };
                var t = new TextBox { Text = def ?? "", Location = new Point(Theme.D(20), Theme.D(46)), Width = Theme.D(360), BackColor = Theme.Card, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = Theme.F(10.5f, false) };
                var ok = new Pill("Enregistrer") { Primary = true };
                var cancel = new Pill("Annuler");
                ok.Location = new Point(f.ClientSize.Width - Theme.D(20) - ok.Width, Theme.D(100));
                cancel.Location = new Point(ok.Left - Theme.D(8) - cancel.Width, Theme.D(100));
                ok.Click += (s, e) => { f.DialogResult = DialogResult.OK; f.Close(); };
                cancel.Click += (s, e) => { f.DialogResult = DialogResult.Cancel; f.Close(); };
                t.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; f.DialogResult = DialogResult.OK; f.Close(); } };
                f.Controls.AddRange(new Control[] { l, t, ok, cancel });
                f.Shown += (s, e) => { t.Focus(); t.SelectAll(); };
                return f.ShowDialog(owner) == DialogResult.OK ? t.Text.Trim() : null;
            }
        }

        public static void About(IWin32Window owner)
        {
            using (var f = new DarkForm { Text = "À propos", ClientSize = new Size(Theme.D(380), Theme.D(250)) })
            {
                f.Paint += (s, e) =>
                {
                    var g = e.Graphics; Theme.HQ(g);
                    int ls = Theme.D(64), w = f.ClientSize.Width;
                    LogoArt.Draw(g, new RectangleF((w - ls) / 2f, Theme.D(22), ls, ls));
                    Theme.Draw(g, Brand.Name, Theme.F(13f, true), new Rectangle(0, Theme.D(98), w, Theme.D(28)), Theme.Text, TextFormatFlags.HorizontalCenter);
                    Theme.Draw(g, "Version " + Brand.Version + "  ·  " + Brand.By, Theme.F(9f, false), new Rectangle(0, Theme.D(128), w, Theme.D(20)), Theme.Dim, TextFormatFlags.HorizontalCenter);
                    Theme.Draw(g, "Écrans et HDR en un clic · Windows 10 / 11", Theme.F(9f, false), new Rectangle(0, Theme.D(150), w, Theme.D(20)), Theme.Faint, TextFormatFlags.HorizontalCenter);
                };
                var ok = new Pill("OK") { Primary = true };
                ok.Location = new Point((f.ClientSize.Width - ok.Width) / 2, Theme.D(196));
                ok.Click += (s, e) => f.Close();
                f.Controls.Add(ok);
                f.ShowDialog(owner);
            }
        }
    }

    /// Grand numero affiche au centre d'un ecran ("Identifier").
    class IdentifyForm : Form
    {
        readonly int number; readonly string name;
        readonly Timer timer = new Timer { Interval = 2500 };

        public IdentifyForm(Rectangle screen, int number, string name)
        {
            this.number = number; this.name = name;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Bg; AutoScaleMode = AutoScaleMode.None; DoubleBuffered = true;
            var sz = new Size(Theme.D(240), Theme.D(180));
            Bounds = new Rectangle(screen.X + (screen.Width - sz.Width) / 2, screen.Y + (screen.Height - sz.Height) / 2, sz.Width, sz.Height);
            timer.Tick += (s, e) => Close(); timer.Start();
            Click += (s, e) => Close();
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Win.Style(Handle, Theme.Bg); }
        protected override void OnFormClosed(FormClosedEventArgs e) { timer.Dispose(); base.OnFormClosed(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var pen = new Pen(Theme.Accent, Theme.D(2))) g.DrawRectangle(pen, Theme.D(1), Theme.D(1), Width - Theme.D(2) - 1, Height - Theme.D(2) - 1);
            Theme.Draw(g, number.ToString(), Theme.F(60f, true), new Rectangle(0, Theme.D(10), Width, Theme.D(120)), Theme.Text, Theme.Center);
            Theme.Draw(g, name, Theme.F(10.5f, false), new Rectangle(Theme.D(10), Theme.D(132), Width - Theme.D(20), Theme.D(30)), Theme.Dim, Theme.Center | TextFormatFlags.EndEllipsis);
        }
    }
}
