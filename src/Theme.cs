using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text;
using System.Windows.Forms;

namespace AlapSB
{
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(15, 15, 19);
        public static readonly Color Panel = Color.FromArgb(22, 22, 28);
        public static readonly Color Card = Color.FromArgb(27, 27, 34);
        public static readonly Color CardHover = Color.FromArgb(33, 33, 42);
        public static readonly Color Field = Color.FromArgb(34, 34, 43);
        public static readonly Color Border = Color.FromArgb(46, 46, 58);
        public static readonly Color Text = Color.FromArgb(242, 242, 247);
        public static readonly Color Muted = Color.FromArgb(146, 146, 164);
        public static readonly Color Accent = Color.FromArgb(124, 92, 255);
        public static readonly Color AccentHover = Color.FromArgb(143, 116, 255);
        public static readonly Color AccentSoft = Color.FromArgb(190, 172, 255);
        public static readonly Color Pink = Color.FromArgb(236, 72, 153);
        public static readonly Color Danger = Color.FromArgb(239, 68, 68);
        public static readonly Color Green = Color.FromArgb(34, 197, 94);

        public static float Scale = 1f;
        public static int S(float v) { return (int)Math.Round(v * Scale); }

        static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();
        static string iconFamily;

        public static Font UI(float size) { return Get("Segoe UI", size, FontStyle.Regular); }
        public static Font UI(float size, FontStyle st) { return Get("Segoe UI", size, st); }
        public static Font Semi(float size) { return Get("Segoe UI Semibold", size, FontStyle.Regular); }
        public static Font Icons(float size)
        {
            if (iconFamily == null)
            {
                iconFamily = "Segoe MDL2 Assets";
                using (var f = new Font("Segoe Fluent Icons", 10f))
                    if (f.Name == "Segoe Fluent Icons") iconFamily = f.Name;
            }
            return Get(iconFamily, size, FontStyle.Regular);
        }

        static Font Get(string family, float size, FontStyle st)
        {
            string k = family + "|" + size + "|" + (int)st;
            Font f;
            if (!cache.TryGetValue(k, out f)) { f = new Font(family, size, st); cache[k] = f; }
            return f;
        }

        public static void HQ(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            if (d <= 0) { p.AddRectangle(r); return p; }
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Fill(Graphics g, RectangleF r, float rad, Color c)
        {
            using (var p = Round(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }

        public static void Stroke(Graphics g, RectangleF r, float rad, Color c, float width)
        {
            using (var p = Round(r, rad)) using (var pen = new Pen(c, width)) g.DrawPath(pen, p);
        }

        public static void Stroke(Graphics g, RectangleF r, float rad, Color c) { Stroke(g, r, rad, c, 1f); }

        public static void DrawText(Graphics g, string s, Font f, Color c, Rectangle r, TextFormatFlags fl)
        {
            TextRenderer.DrawText(g, s, f, r, c, fl | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        public static void Glyph(Graphics g, string glyph, Font f, Color c, Rectangle r)
        {
            DrawText(g, glyph, f, c, r, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        public static Size Measure(Graphics g, string s, Font f)
        {
            return TextRenderer.MeasureText(g, s, f, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        public static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static ContextMenuStrip NewMenu()
        {
            var m = new ContextMenuStrip();
            m.Renderer = new DarkRenderer();
            m.Font = UI(9.5f);
            m.ShowImageMargin = true;
            m.Padding = new Padding(S(2), S(4), S(2), S(4));
            return m;
        }
    }

    /// <summary>Glyph dari font ikon bawaan Windows (Segoe MDL2 / Fluent Icons).</summary>
    static class G
    {
        public const string Add = "\uE710";
        public const string Edit = "\uE70F";
        public const string Delete = "\uE74D";
        public const string Settings = "\uE713";
        public const string Volume = "\uE767";
        public const string Stop = "\uE71A";
        public const string Play = "\uE768";
        public const string Check = "\uE73E";
        public const string Chevron = "\uE70D";
        public const string Bulb = "\uEA80";
        public const string Folder = "\uE838";
        public const string Library = "\uE8F1";
        public const string Warning = "\uE7BA";
        public const string Save = "\uE74E";
        public const string Keyboard = "\uE765";
        public const string Music = "\uE8D6";
        public const string Power = "\uE7E8";
        public const string Back = "\uE72B";
        public const string Pause = "\uE769";
        public const string Next = "\uE893";
        public const string Prev = "\uE892";
        public const string Shuffle = "\uE8B1";
        public const string Repeat = "\uE8EE";
    }

    static class Keyb
    {
        public const int Ctrl = 1, Shift = 2, Alt = 4;

        public static bool IsModifier(int vk)
        {
            return vk == 0x10 || vk == 0x11 || vk == 0x12 || (vk >= 0xA0 && vk <= 0xA5) || vk == 0x5B || vk == 0x5C;
        }

        public static int FromKeys(Keys modifiers)
        {
            int m = 0;
            if ((modifiers & Keys.Control) != 0) m |= Ctrl;
            if ((modifiers & Keys.Shift) != 0) m |= Shift;
            if ((modifiers & Keys.Alt) != 0) m |= Alt;
            return m;
        }

        public static string Prefix(int mods)
        {
            var sb = new StringBuilder();
            if ((mods & Ctrl) != 0) sb.Append("Ctrl + ");
            if ((mods & Shift) != 0) sb.Append("Shift + ");
            if ((mods & Alt) != 0) sb.Append("Alt + ");
            return sb.ToString();
        }

        public static string Combo(int vk, int mods)
        {
            if (vk == 0) return "";
            return Prefix(mods) + Name(vk);
        }

        public static string Name(int vk)
        {
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
            if (vk >= 0x60 && vk <= 0x69) return "Num " + (vk - 0x60);
            switch ((Keys)vk)
            {
                case Keys.PageUp: return "PageUp";
                case Keys.PageDown: return "PageDown";
                case Keys.Up: return "ArrowUp";
                case Keys.Down: return "ArrowDown";
                case Keys.Left: return "ArrowLeft";
                case Keys.Right: return "ArrowRight";
                case Keys.Enter: return "Enter";
                case Keys.Escape: return "Esc";
                case Keys.Back: return "Backspace";
                case Keys.CapsLock: return "CapsLock";
                case Keys.Oemtilde: return "`";
                case Keys.OemMinus: return "-";
                case Keys.Oemplus: return "=";
                case Keys.OemOpenBrackets: return "[";
                case Keys.OemCloseBrackets: return "]";
                case Keys.OemPipe: return "\\";
                case Keys.OemSemicolon: return ";";
                case Keys.OemQuotes: return "'";
                case Keys.Oemcomma: return ",";
                case Keys.OemPeriod: return ".";
                case Keys.OemQuestion: return "/";
                case Keys.Multiply: return "Num *";
                case Keys.Add: return "Num +";
                case Keys.Subtract: return "Num -";
                case Keys.Divide: return "Num /";
                case Keys.Decimal: return "Num .";
                case Keys.PrintScreen: return "PrtSc";
                case Keys.Scroll: return "ScrollLock";
                case Keys.NumLock: return "NumLock";
                case Keys.Space: return "Space";
                case Keys.MediaPlayPause: return "Media Play";
                case Keys.MediaNextTrack: return "Media Next";
                case Keys.MediaPreviousTrack: return "Media Prev";
                case Keys.MediaStop: return "Media Stop";
                case Keys.VolumeUp: return "Vol +";
                case Keys.VolumeDown: return "Vol -";
                case Keys.VolumeMute: return "Mute";
            }
            return ((Keys)vk).ToString();
        }
    }

    class DarkColors : ProfessionalColorTable
    {
        static readonly Color Sel = Color.FromArgb(44, 44, 56);
        public override Color MenuItemSelected { get { return Sel; } }
        public override Color MenuItemBorder { get { return Sel; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color ToolStripDropDownBackground { get { return Theme.Panel; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Panel; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Panel; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Panel; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Panel; } }
        public override Color MenuItemSelectedGradientBegin { get { return Sel; } }
        public override Color MenuItemSelectedGradientEnd { get { return Sel; } }
        public override Color MenuItemPressedGradientBegin { get { return Sel; } }
        public override Color MenuItemPressedGradientMiddle { get { return Sel; } }
        public override Color MenuItemPressedGradientEnd { get { return Sel; } }
        public override Color CheckBackground { get { return Theme.Panel; } }
        public override Color CheckSelectedBackground { get { return Sel; } }
        public override Color CheckPressedBackground { get { return Sel; } }
    }

    class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (!e.Item.Enabled) e.TextColor = Theme.Muted;
            else if ("danger".Equals(e.Item.Tag)) e.TextColor = Theme.Danger;
            else if ("accent".Equals(e.Item.Tag)) e.TextColor = Theme.AccentSoft;
            else e.TextColor = Theme.Text;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Theme.Muted;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Theme.Glyph(e.Graphics, G.Check, Theme.Icons(9f), Theme.AccentSoft, e.ImageRectangle);
        }
    }
}
