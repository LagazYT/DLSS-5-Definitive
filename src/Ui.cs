using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Dlss5Injector
{
    /// <summary>Paleta e fontes do tema escuro.</summary>
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(14, 16, 20);
        public static readonly Color Card = Color.FromArgb(23, 26, 33);
        public static readonly Color Field = Color.FromArgb(31, 35, 44);
        public static readonly Color FieldHover = Color.FromArgb(39, 44, 55);
        public static readonly Color Border = Color.FromArgb(44, 49, 60);
        public static readonly Color Text = Color.FromArgb(232, 234, 237);
        public static readonly Color Muted = Color.FromArgb(150, 157, 170);
        public static readonly Color Dim = Color.FromArgb(98, 105, 118);
        public static readonly Color Accent = Color.FromArgb(118, 185, 0);       // verde NVIDIA
        public static readonly Color AccentHover = Color.FromArgb(139, 209, 0);
        public static readonly Color AccentDark = Color.FromArgb(36, 52, 12);
        public static readonly Color Warn = Color.FromArgb(245, 165, 36);
        public static readonly Color WarnBg = Color.FromArgb(48, 38, 18);
        public static readonly Color Danger = Color.FromArgb(240, 80, 110);
        public static readonly Color Info = Color.FromArgb(76, 154, 255);
        public static readonly Color LogBg = Color.FromArgb(11, 13, 17);

        static string Family(params string[] names)
        {
            using (InstalledFontCollection c = new InstalledFontCollection())
            {
                foreach (string n in names)
                    foreach (FontFamily f in c.Families)
                        if (string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase)) return n;
            }
            return "Segoe UI";
        }

        public static readonly string UiFamily = Family("Segoe UI Variable Text", "Segoe UI");
        public static readonly string TitleFamily = Family("Segoe UI Variable Display", "Segoe UI Semibold", "Segoe UI");
        public static readonly string IconFamily = Family("Segoe Fluent Icons", "Segoe MDL2 Assets");

        public static Font F(float size, FontStyle st = FontStyle.Regular) { return new Font(UiFamily, size, st); }
        public static Font Title(float size) { return new Font(TitleFamily, size, FontStyle.Bold); }
        public static Font Icon(float size) { return new Font(IconFamily, size); }

        // Glifos Segoe Fluent Icons / MDL2
        public const string GFolder = "", GCheck = "", GWarn = "", GDownload = "",
            GDelete = "", GOpen = "", GGame = "", GChip = "", GInfo = "",
            GError = "", GSync = "", GEdit = "";

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        public static void FillRound(Graphics g, Color c, RectangleF r, float rad)
        {
            using (GraphicsPath p = Round(r, rad))
            using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
        }

        public static void DrawRound(Graphics g, Color c, RectangleF r, float rad, float width = 1f, DashStyle dash = DashStyle.Solid)
        {
            using (GraphicsPath p = Round(r, rad))
            using (Pen pen = new Pen(c, width))
            {
                pen.DashStyle = dash;
                g.DrawPath(pen, p);
            }
        }

        public static void Draw(Graphics g, string s, Font f, Color c, Rectangle r, TextFormatFlags fl)
        {
            TextRenderer.DrawText(g, s, f, r, c, fl | TextFormatFlags.NoPrefix);
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        /// <summary>Barras de rolagem / dropdowns escuros do próprio Windows 10/11.</summary>
        public static void DarkNative(Control c, string theme)
        {
            try { SetWindowTheme(c.Handle, theme, null); } catch { }
        }

        public static void DarkTitleBar(Form f)
        {
            try
            {
                int on = 1;
                DwmSetWindowAttribute(f.Handle, 20, ref on, 4);           // DWMWA_USE_IMMERSIVE_DARK_MODE
                int caption = ColorTranslator.ToWin32(Bg);
                DwmSetWindowAttribute(f.Handle, 35, ref caption, 4);      // DWMWA_CAPTION_COLOR (Win11)
            }
            catch { }
        }

        public static System.Drawing.Icon AppIcon()
        {
            using (Bitmap b = new Bitmap(64, 64))
            {
                using (Graphics g = Graphics.FromImage(b))
                {
                    Smooth(g);
                    FillRound(g, Accent, new RectangleF(2, 2, 60, 60), 14);
                    using (Font f = new Font(TitleFamily, 30, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString("5", f, new SolidBrush(Bg), new RectangleF(0, 2, 64, 64), sf);
                    }
                }
                return System.Drawing.Icon.FromHandle(b.GetHicon());
            }
        }
    }

    /// <summary>Base para controles desenhados à mão (sem flicker, fundo do pai).</summary>
    public class PaintedControl : Control
    {
        protected bool hover, down;

        public PaintedControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            ForeColor = Theme.Text;
        }

        protected Color ParentBack { get { return Parent != null ? Parent.BackColor : Theme.Bg; } }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    }

    /// <summary>Cartão com cantos arredondados, número da etapa e título.</summary>
    public class Card : Panel
    {
        public string Title;
        public string Step;

        public Card(string step, string title)
        {
            Step = step;
            Title = title;
            BackColor = Theme.Card;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            Theme.Smooth(g);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.FillRound(g, Theme.Card, r, 12);
            Theme.DrawRound(g, Theme.Border, r, 12);
            if (Title == null) return;
            int x = 18;
            if (Step != null)
            {
                Theme.FillRound(g, Theme.AccentDark, new RectangleF(16, 15, 24, 24), 12);
                using (Font f = Theme.F(9f, FontStyle.Bold))
                    Theme.Draw(g, Step, f, Theme.Accent, new Rectangle(16, 15, 24, 24), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                x = 48;
            }
            using (Font f = Theme.F(11f, FontStyle.Bold))
                Theme.Draw(g, Title, f, Theme.Text, new Rectangle(x, 13, Width - x - 16, 28), TextFormatFlags.VerticalCenter);
        }
    }

    public enum ButtonKind { Primary, Secondary, Ghost }

    public class ModernButton : PaintedControl
    {
        public ButtonKind Kind = ButtonKind.Secondary;
        public string Glyph;

        public ModernButton(string text, string glyph, ButtonKind kind)
        {
            Text = text;
            Glyph = glyph;
            Kind = kind;
            Cursor = Cursors.Hand;
            Font = Theme.F(kind == ButtonKind.Primary ? 11f : 9.5f, kind == ButtonKind.Primary ? FontStyle.Bold : FontStyle.Regular);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(ParentBack);
            Theme.Smooth(g);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color bg, fg, border = Color.Empty;
            if (Kind == ButtonKind.Primary)
            {
                bg = !Enabled ? Theme.Field : down ? Theme.Accent : hover ? Theme.AccentHover : Theme.Accent;
                fg = Enabled ? Theme.Bg : Theme.Dim;
            }
            else if (Kind == ButtonKind.Secondary)
            {
                bg = !Enabled ? Theme.Card : hover ? Theme.FieldHover : Theme.Field;
                fg = Enabled ? Theme.Text : Theme.Dim;
                border = Theme.Border;
            }
            else
            {
                bg = hover && Enabled ? Theme.Field : ParentBack;
                fg = Enabled ? (hover ? Theme.AccentHover : Theme.Accent) : Theme.Dim;
            }
            float rad = Math.Min(10, Height / 2f);
            Theme.FillRound(g, bg, r, rad);
            if (border != Color.Empty) Theme.DrawRound(g, border, r, rad);

            Size ts = TextRenderer.MeasureText(Text, Font);
            int gw = 0;
            Font gf = null;
            if (!string.IsNullOrEmpty(Glyph))
            {
                gf = Theme.Icon(Font.Size);
                gw = TextRenderer.MeasureText(Glyph, gf).Width;
            }
            int total = ts.Width + (gw > 0 ? gw : 0);
            int x = (Width - total) / 2;
            if (gf != null)
            {
                Theme.Draw(g, Glyph, gf, fg, new Rectangle(x, 0, gw, Height), TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
                x += gw - 4;
                gf.Dispose();
            }
            Theme.Draw(g, Text, Font, fg, new Rectangle(x, 0, ts.Width + 4, Height), TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
    }

    public class ToggleSwitch : PaintedControl
    {
        bool on;
        public event EventHandler CheckedChanged;

        public ToggleSwitch(string text, bool value)
        {
            Text = text;
            on = value;
            Cursor = Cursors.Hand;
            Font = Theme.F(9.5f);
            Height = 30;
        }

        public bool Checked
        {
            get { return on; }
            set
            {
                if (on == value) return;
                on = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            if (Enabled) Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(ParentBack);
            Theme.Smooth(g);
            float th = 20, tw = 38, ty = (Height - th) / 2f;
            RectangleF track = new RectangleF(1, ty, tw, th);
            Color tc = !Enabled ? Theme.Field : on ? (hover ? Theme.AccentHover : Theme.Accent) : (hover ? Theme.FieldHover : Theme.Field);
            Theme.FillRound(g, tc, track, th / 2);
            if (!on) Theme.DrawRound(g, Theme.Border, track, th / 2);
            float d = th - 8;
            float kx = on ? track.Right - d - 4 : track.X + 4;
            using (SolidBrush b = new SolidBrush(on ? Theme.Bg : (Enabled ? Theme.Muted : Theme.Dim)))
                g.FillEllipse(b, kx, ty + 4, d, d);
            Theme.Draw(g, Text, Font, Enabled ? Theme.Text : Theme.Dim, new Rectangle(48, 0, Width - 48, Height),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Seletor segmentado (ex.: 1 | 2 | 3).</summary>
    public class Segmented : PaintedControl
    {
        public string[] Items;
        int sel;
        int hot = -1;
        public event EventHandler SelectedChanged;

        public Segmented(params string[] items)
        {
            Items = items;
            Cursor = Cursors.Hand;
            Font = Theme.F(9.5f, FontStyle.Bold);
            Height = 34;
        }

        public int Selected
        {
            get { return sel; }
            set
            {
                if (value == sel) return;
                sel = value;
                Invalidate();
                if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
            }
        }

        int At(int x) { return Math.Max(0, Math.Min(Items.Length - 1, x * Items.Length / Math.Max(1, Width))); }

        protected override void OnMouseMove(MouseEventArgs e) { int h = At(e.X); if (h != hot) { hot = h; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = -1; base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e) { if (Enabled) Selected = At(e.X); base.OnMouseClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(ParentBack);
            Theme.Smooth(g);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.FillRound(g, Theme.Field, r, 9);
            Theme.DrawRound(g, Theme.Border, r, 9);
            float w = (Width - 6) / (float)Items.Length;
            for (int i = 0; i < Items.Length; i++)
            {
                RectangleF c = new RectangleF(3 + i * w, 3, w, Height - 6);
                if (i == sel) Theme.FillRound(g, Enabled ? Theme.Accent : Theme.Dim, c, 7);
                else if (i == hot) Theme.FillRound(g, Theme.FieldHover, c, 7);
                Theme.Draw(g, Items[i], Font, i == sel ? Theme.Bg : Theme.Muted, Rectangle.Round(c),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    /// <summary>Área de arrastar-e-soltar / clicar para escolher a pasta do jogo.</summary>
    public class DropZone : PaintedControl
    {
        public string Headline = "Arraste a pasta do jogo aqui";
        public string Sub = "ou clique para procurar · também aceita o .exe";
        public bool Filled;
        public bool IsDragOver;

        public DropZone()
        {
            Cursor = Cursors.Hand;
            AllowDrop = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(ParentBack);
            Theme.Smooth(g);
            RectangleF r = new RectangleF(1, 1, Width - 3, Height - 3);
            bool hot = hover || IsDragOver;
            Theme.FillRound(g, hot ? Theme.FieldHover : Theme.Field, r, 10);
            if (Filled) Theme.DrawRound(g, hot ? Theme.Accent : Theme.Border, r, 10);
            else Theme.DrawRound(g, hot ? Theme.Accent : Theme.Dim, r, 10, 1.5f, DashStyle.Dash);

            int icon = Height - 36;
            icon = Math.Min(icon, 52);
            Rectangle ir = new Rectangle(18, (Height - icon) / 2, icon, icon);
            Theme.FillRound(g, Filled ? Theme.AccentDark : Theme.Card, ir, 10);
            using (Font f = Theme.Icon(icon * 0.36f))
                Theme.Draw(g, Filled ? Theme.GGame : Theme.GFolder, f, Filled || hot ? Theme.Accent : Theme.Muted, ir,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            int tx = ir.Right + 16;
            using (Font h = Theme.F(11.5f, FontStyle.Bold))
            using (Font s = Theme.F(9f))
            {
                Theme.Draw(g, Headline, h, Theme.Text, new Rectangle(tx, Height / 2 - 26, Width - tx - 14, 26),
                    TextFormatFlags.Bottom | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                Theme.Draw(g, Sub, s, Theme.Muted, new Rectangle(tx, Height / 2 + 2, Width - tx - 14, 22),
                    TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.PathEllipsis);
            }
        }
    }

    /// <summary>Bloco de informação da análise (legenda + valor + indicador colorido).</summary>
    public class InfoTile : PaintedControl
    {
        public string Caption, Value, Glyph;
        public Color Accent = Theme.Muted;

        public InfoTile(string caption, string glyph)
        {
            Caption = caption;
            Glyph = glyph;
            Value = "—";
        }

        public void Set(string value, Color accent)
        {
            Value = value;
            Accent = accent;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(ParentBack);
            Theme.Smooth(g);
            Theme.FillRound(g, Theme.Field, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 9);
            using (SolidBrush b = new SolidBrush(Accent))
                Theme.FillRound(g, Accent, new RectangleF(0.5f, 10, 3, Height - 20), 1.5f);
            using (Font gf = Theme.Icon(12f))
                Theme.Draw(g, Glyph, gf, Accent, new Rectangle(14, 0, 22, Height), TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            using (Font cf = Theme.F(8f))
            using (Font vf = Theme.F(10f, FontStyle.Bold))
            {
                Theme.Draw(g, Caption.ToUpperInvariant(), cf, Theme.Dim, new Rectangle(44, 7, Width - 52, 16), TextFormatFlags.Left | TextFormatFlags.Top);
                Theme.Draw(g, Value, vf, Theme.Text, new Rectangle(44, 23, Width - 52, Height - 26),
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
            }
        }
    }

    /// <summary>Linha de status de arquivo (ícone + título + detalhe + botão "Alterar").</summary>
    public class StatusRow : PaintedControl
    {
        public string Caption, Detail, Glyph = Theme.GInfo;
        public Color Accent = Theme.Muted;

        public StatusRow(string caption) { Caption = caption; Detail = "—"; Height = 46; }

        public void Set(string glyph, Color accent, string detail)
        {
            Glyph = glyph;
            Accent = accent;
            Detail = detail;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(ParentBack);
            Theme.Smooth(g);
            Rectangle ir = new Rectangle(0, (Height - 30) / 2, 30, 30);
            Theme.FillRound(g, Color.FromArgb(40, Accent), ir, 15);
            using (Font gf = Theme.Icon(10f))
                Theme.Draw(g, Glyph, gf, Accent, ir, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            using (Font cf = Theme.F(9.5f, FontStyle.Bold))
            using (Font df = Theme.F(8.5f))
            {
                Theme.Draw(g, Caption, cf, Theme.Text, new Rectangle(40, 3, Width - 40, 20), TextFormatFlags.Left | TextFormatFlags.Top);
                Theme.Draw(g, Detail, df, Theme.Muted, new Rectangle(40, 23, Width - 40, Height - 23),
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
            }
        }
    }

    /// <summary>Lista de passos numerados e avisos, com rolagem pela roda do mouse.</summary>
    public class StepsView : PaintedControl
    {
        List<string> steps = new List<string>();
        List<string> warns = new List<string>();
        string header, empty = "Selecione a pasta do jogo para ver o plano.";
        int scroll, contentH;

        public StepsView() { Font = Theme.F(9f); }

        public void SetContent(string header, List<string> steps, List<string> warns, string emptyText)
        {
            this.header = header;
            this.steps = steps ?? new List<string>();
            this.warns = warns ?? new List<string>();
            if (emptyText != null) empty = emptyText;
            scroll = 0;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int max = Math.Max(0, contentH - Height);
            scroll = Math.Max(0, Math.Min(max, scroll - e.Delta / 3));
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override void OnMouseEnter(EventArgs e) { Focus(); base.OnMouseEnter(e); }

        int Measure(Graphics g, string s, Font f, int w)
        {
            return TextRenderer.MeasureText(g, s, f, new Size(w, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(ParentBack);
            Theme.Smooth(g);
            if (header == null && steps.Count == 0)
            {
                Theme.Draw(g, empty, Font, Theme.Dim, ClientRectangle, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                return;
            }
            int y = -scroll;
            int w = Width - 14;
            if (header != null)
            {
                using (Font hf = Theme.F(9f, FontStyle.Bold))
                {
                    int h = Measure(g, header, hf, w);
                    Theme.Draw(g, header, hf, Theme.Accent, new Rectangle(0, y, w, h), TextFormatFlags.WordBreak);
                    y += h + 8;
                }
            }
            using (Font nf = Theme.F(8f, FontStyle.Bold))
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    int h = Math.Max(22, Measure(g, steps[i], Font, w - 32));
                    Rectangle c = new Rectangle(0, y + 1, 20, 20);
                    Theme.DrawRound(g, Theme.Accent, new RectangleF(c.X + 0.5f, c.Y + 0.5f, 19, 19), 10);
                    Theme.Draw(g, (i + 1).ToString(), nf, Theme.Accent, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    Theme.Draw(g, steps[i], Font, Theme.Text, new Rectangle(30, y + 2, w - 32, h), TextFormatFlags.WordBreak);
                    y += h + 6;
                }
            }
            if (warns.Count > 0) y += 4;
            using (Font gf = Theme.Icon(10f))
            {
                foreach (string wn in warns)
                {
                    int h = Measure(g, wn, Font, w - 44) + 14;
                    Theme.FillRound(g, Theme.WarnBg, new RectangleF(0, y, w, h), 8);
                    Theme.Draw(g, Theme.GWarn, gf, Theme.Warn, new Rectangle(10, y, 20, h), TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
                    Theme.Draw(g, wn, Font, Color.FromArgb(250, 214, 150), new Rectangle(36, y + 7, w - 44, h - 14), TextFormatFlags.WordBreak);
                    y += h + 6;
                }
            }
            contentH = y + scroll;
            if (contentH > Height)
            {
                float th = Math.Max(24, Height * (float)Height / contentH);
                float ty = (Height - th) * scroll / Math.Max(1f, contentH - Height);
                Theme.FillRound(g, Theme.Border, new RectangleF(Width - 5, ty, 4, th), 2);
            }
        }
    }

    public class ThinProgress : PaintedControl
    {
        int value;
        public int Value { get { return value; } set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); } }

        public ThinProgress() { Height = 6; }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(ParentBack);
            Theme.Smooth(g);
            Theme.FillRound(g, Theme.Field, new RectangleF(0, 0, Width - 1, Height - 1), Height / 2f);
            if (value > 0)
                Theme.FillRound(g, Theme.Accent, new RectangleF(0, 0, Math.Max(Height, (Width - 1) * value / 100f), Height - 1), Height / 2f);
        }
    }

    /// <summary>Dropdown escuro desenhado à mão; a lista abre num menu com o mesmo tema.</summary>
    public class DarkCombo : PaintedControl
    {
        public readonly List<string> Items = new List<string>();
        int sel = -1;
        bool open;
        public event EventHandler SelectedIndexChanged;

        public DarkCombo()
        {
            Cursor = Cursors.Hand;
            Font = Theme.F(9.5f);
            Height = 36;
            TabStop = true;
        }

        public int SelectedIndex
        {
            get { return sel; }
            set
            {
                int v = value < -1 || value >= Items.Count ? -1 : value;
                if (v == sel) return;
                sel = v;
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        public string SelectedItem { get { return sel >= 0 && sel < Items.Count ? Items[sel] : null; } }

        public void Clear()
        {
            Items.Clear();
            sel = -1;
            Invalidate();
        }

        // Um único menu por dropdown, reaproveitado. NÃO descartar no evento Closed: o WinForms ainda usa o
        // menu depois disso (era a causa do ObjectDisposedException que fechava o app).
        ContextMenuStrip menu;
        Font boldFont;

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (!Enabled || Items.Count == 0) return;
            if (menu == null)
            {
                menu = new ContextMenuStrip();
                menu.Renderer = new DarkMenuRenderer();
                menu.ShowImageMargin = false;
                menu.ShowCheckMargin = false;
                menu.Padding = new Padding(4);
                menu.BackColor = Theme.Field;
                menu.Font = Font;
                menu.Closed += delegate { open = false; Invalidate(); };
                menu.ItemClicked += delegate(object s, ToolStripItemClickedEventArgs a)
                {
                    int idx = menu.Items.IndexOf(a.ClickedItem);
                    BeginInvoke(new Action(delegate { SelectedIndex = idx; }));
                };
                boldFont = new Font(Font, FontStyle.Bold);
            }
            menu.Items.Clear();
            for (int i = 0; i < Items.Count; i++)
            {
                ToolStripMenuItem it = new ToolStripMenuItem(Items[i]);
                it.AutoSize = false;
                it.Size = new Size(Width - 10, 32);
                it.ForeColor = i == sel ? Theme.AccentHover : Theme.Text;
                it.Font = i == sel ? boldFont : Font;
                menu.Items.Add(it);
            }
            menu.AutoSize = false;
            menu.Size = new Size(Width, Math.Min(Items.Count, 12) * 32 + 10);
            open = true;
            Invalidate();
            menu.Show(this, new Point(0, Height + 4));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && menu != null) menu.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(ParentBack);
            Theme.Smooth(g);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.FillRound(g, hover && Enabled ? Theme.FieldHover : Theme.Field, r, 9);
            Theme.DrawRound(g, open ? Theme.Accent : Theme.Border, r, 9);
            string s = SelectedItem ?? "";
            Theme.Draw(g, s, Font, Enabled ? Theme.Text : Theme.Dim, new Rectangle(12, 0, Width - 44, Height),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            using (Font gf = Theme.Icon(8f))
                Theme.Draw(g, open ? "" : "", gf, Enabled ? Theme.Muted : Theme.Dim, new Rectangle(Width - 32, 0, 22, Height),
                    TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        }
    }

    public class DarkMenuRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(Theme.Field)) e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (Pen p = new Pen(Theme.Border))
                e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected) return;
            Theme.Smooth(e.Graphics);
            Theme.FillRound(e.Graphics, Theme.AccentDark, new RectangleF(2, 1, e.Item.Width - 4, e.Item.Height - 2), 6);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            Rectangle r = new Rectangle(10, 0, e.Item.Width - 16, e.Item.Height);
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, r, e.Item.Selected ? Theme.AccentHover : e.Item.ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>Abas do cabeçalho (pílulas).</summary>
    public class TabBar : PaintedControl
    {
        public string[] Items;
        public string[] Glyphs;
        int sel, hot = -1;
        int[] xs;
        public event EventHandler SelectedChanged;

        public TabBar(string[] items, string[] glyphs)
        {
            Items = items;
            Glyphs = glyphs;
            Cursor = Cursors.Hand;
            Font = Theme.F(9.5f, FontStyle.Bold);
            Height = 40;
        }

        public int Selected
        {
            get { return sel; }
            set { if (value == sel) return; sel = value; Invalidate(); if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty); }
        }

        int At(int x)
        {
            if (xs == null) return -1;
            for (int i = 0; i < Items.Length; i++) if (x >= xs[i] && x < xs[i + 1]) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e) { int h = At(e.X); if (h != hot) { hot = h; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = -1; base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e) { int i = At(e.X); if (i >= 0) Selected = i; base.OnMouseClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(ParentBack);
            Theme.Smooth(g);
            Theme.FillRound(g, Theme.Card, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Height / 2f);
            Theme.DrawRound(g, Theme.Border, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Height / 2f);
            float w = (Width - 8) / (float)Items.Length;
            xs = new int[Items.Length + 1];
            using (Font gf = Theme.Icon(10f))
            {
                for (int i = 0; i < Items.Length; i++)
                {
                    xs[i] = (int)(4 + i * w);
                    RectangleF c = new RectangleF(4 + i * w, 4, w, Height - 8);
                    if (i == sel) Theme.FillRound(g, Theme.Accent, c, (Height - 8) / 2f);
                    else if (i == hot) Theme.FillRound(g, Theme.Field, c, (Height - 8) / 2f);
                    Color fg = i == sel ? Theme.Bg : Theme.Muted;
                    int tw = TextRenderer.MeasureText(Items[i], Font).Width;
                    int total = tw + 22;
                    int x = (int)(c.X + (c.Width - total) / 2);
                    Theme.Draw(g, Glyphs[i], gf, fg, new Rectangle(x, (int)c.Y, 20, (int)c.Height), TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
                    Theme.Draw(g, Items[i], Font, fg, new Rectangle(x + 22, (int)c.Y, tw + 4, (int)c.Height), TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                }
            }
            xs[Items.Length] = Width;
        }
    }

    /// <summary>Campo de texto escuro com ícone de busca.</summary>
    public class SearchBox : Panel
    {
        public readonly TextBox Box = new TextBox();

        public SearchBox(string placeholder)
        {
            BackColor = Theme.Field;
            Height = 36;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Box.BorderStyle = BorderStyle.None;
            Box.BackColor = Theme.Field;
            Box.ForeColor = Theme.Text;
            Box.Font = Theme.F(10f);
            Box.HandleCreated += delegate { SendMessage(Box.Handle, 0x1501, (IntPtr)1, placeholder); }; // EM_SETCUEBANNER
            Box.GotFocus += delegate { Invalidate(); };
            Box.LostFocus += delegate { Invalidate(); };
            Controls.Add(Box);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            Box.SetBounds(36, (Height - Box.PreferredHeight) / 2 + 1, Width - 50, Box.PreferredHeight);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            Theme.Smooth(g);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.FillRound(g, Theme.Field, r, Height / 2f);
            Theme.DrawRound(g, Box.Focused ? Theme.Accent : Theme.Border, r, Height / 2f);
            using (Font gf = Theme.Icon(10f))
                Theme.Draw(g, "", gf, Theme.Muted, new Rectangle(8, 0, 24, Height), TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        }
    }

    /// <summary>Card de jogo da biblioteca (capa 2:3, nome, origem e selo do DLSS 5).</summary>
    public class GameCard : PaintedControl
    {
        public readonly GameEntry Game;
        Bitmap cover;
        public const int CoverW = 168, CoverH = 252;

        public GameCard(GameEntry g)
        {
            Game = g;
            Size = new Size(CoverW, CoverH + 52);
            Cursor = Cursors.Hand;
            Margin = new Padding(8, 8, 8, 10);
        }

        public void SetCover(Bitmap b)
        {
            if (cover != null) cover.Dispose();
            cover = b;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && cover != null) cover.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(ParentBack);
            Theme.Smooth(g);
            RectangleF cr = new RectangleF(1, 1, CoverW - 2, CoverH - 2);
            using (GraphicsPath p = Theme.Round(cr, 10))
            {
                if (cover != null)
                {
                    g.SetClip(p);
                    g.DrawImage(cover, cr);
                    if (hover)
                        using (SolidBrush sh = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.FillPath(sh, p);
                    g.ResetClip();
                }
                else
                {
                    // Capa provisória: degradê + iniciais
                    using (LinearGradientBrush lg = new LinearGradientBrush(cr, Color.FromArgb(38, 46, 30), Color.FromArgb(22, 25, 32), 60f))
                        g.FillPath(lg, p);
                    using (Font f = Theme.Title(26f))
                        Theme.Draw(g, Initials(Game.Name), f, Color.FromArgb(160, Theme.Accent), Rectangle.Round(cr),
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                Theme.DrawRound(g, hover ? Theme.Accent : Theme.Border, cr, 10, hover ? 2f : 1f);
            }
            using (Font bf = Theme.F(7.5f, FontStyle.Bold))
            {
                string src = Game.Source ?? "";
                int sw = TextRenderer.MeasureText(src, bf).Width + 12;
                RectangleF sb = new RectangleF(8, 8, sw, 20);
                Theme.FillRound(g, Color.FromArgb(210, 14, 16, 20), sb, 10);
                Theme.Draw(g, src, bf, Theme.Text, Rectangle.Round(sb), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                if (Game.Installed)
                {
                    string t = "DLSS 5";
                    int w = TextRenderer.MeasureText(t, bf).Width + 26;
                    RectangleF ib = new RectangleF(CoverW - w - 8, 8, w, 20);
                    Theme.FillRound(g, Theme.Accent, ib, 10);
                    using (Font gf = Theme.Icon(7.5f))
                        Theme.Draw(g, Theme.GCheck, gf, Theme.Bg, new Rectangle((int)ib.X + 4, 8, 14, 20), TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
                    Theme.Draw(g, t, bf, Theme.Bg, new Rectangle((int)ib.X + 18, 8, w - 18, 20), TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                }
            }
            if (hover)
            {
                using (Font f = Theme.F(10f, FontStyle.Bold))
                using (Font gf = Theme.Icon(20f))
                {
                    Theme.Draw(g, "", gf, Theme.Accent, new Rectangle(0, CoverH / 2 - 40, CoverW, 36), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    Theme.Draw(g, "Configurar DLSS 5", f, Theme.Text, new Rectangle(0, CoverH / 2, CoverW, 24), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
            using (Font nf = Theme.F(9.5f, FontStyle.Bold))
                Theme.Draw(g, Game.Name, nf, hover ? Theme.AccentHover : Theme.Text, new Rectangle(2, CoverH + 6, CoverW - 4, 40),
                    TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.Top);
        }

        static string Initials(string n)
        {
            if (string.IsNullOrEmpty(n)) return "?";
            StringBuilder sb = new StringBuilder();
            foreach (string w in n.Split(new[] { ' ', '-', ':' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (char.IsLetterOrDigit(w[0])) sb.Append(char.ToUpperInvariant(w[0]));
                if (sb.Length >= 3) break;
            }
            return sb.Length == 0 ? "?" : sb.ToString();
        }
    }

    /// <summary>Linha do tempo mostrando quadros reais e gerados para o multiplicador escolhido.</summary>
    public class FrameTimeline : PaintedControl
    {
        int mult = 2;
        bool dynamic;

        public void Set(int multiplier, bool dyn)
        {
            mult = multiplier;
            dynamic = dyn;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(ParentBack);
            Theme.Smooth(g);
            int m = Math.Max(1, mult);
            int cycles = m == 1 ? 12 : Math.Max(2, 12 / m);
            int total = cycles * m;
            float gap = 4, w = (Width - gap * (total - 1)) / total, h = Height - 26;
            using (Font f = Theme.F(8f))
            {
                for (int i = 0; i < total; i++)
                {
                    bool real = i % m == 0;
                    RectangleF r = new RectangleF(i * (w + gap), 0, w, h);
                    if (real) Theme.FillRound(g, Theme.Accent, r, 6);
                    else
                    {
                        Theme.FillRound(g, Color.FromArgb(40, Theme.Info), r, 6);
                        Theme.DrawRound(g, Theme.Info, r, 6, 1.5f, DashStyle.Dash);
                    }
                }
                Theme.FillRound(g, Theme.Accent, new RectangleF(0, Height - 16, 12, 12), 3);
                Theme.Draw(g, "Quadro real (renderizado pelo jogo)", f, Theme.Muted, new Rectangle(18, Height - 20, 230, 20), TextFormatFlags.VerticalCenter);
                if (m > 1 || dynamic)
                {
                    Theme.FillRound(g, Color.FromArgb(40, Theme.Info), new RectangleF(256, Height - 16, 12, 12), 3);
                    Theme.DrawRound(g, Theme.Info, new RectangleF(256, Height - 16, 12, 12), 3);
                    Theme.Draw(g, dynamic ? "Gerado pela IA (varia até 6X)" : "Quadro gerado pela IA (DLSS-G)", f, Theme.Muted,
                        new Rectangle(274, Height - 20, 260, 20), TextFormatFlags.VerticalCenter);
                }
            }
        }
    }

    public class FieldLabel : Label
    {
        public FieldLabel(string text)
        {
            Text = text;
            AutoSize = true;
            ForeColor = Theme.Muted;
            Font = Theme.F(8.5f);
            BackColor = Color.Transparent;
        }
    }
}
