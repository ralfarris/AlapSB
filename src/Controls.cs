using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace AlapSB
{
    class DarkControl : Control
    {
        protected bool Hover, Pressed;

        public DarkControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected static int S(float v) { return Theme.S(v); }

        protected override void OnMouseEnter(EventArgs e) { Hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hover = false; Pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { Pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    }

    class PaintPanel : Panel
    {
        public PaintPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }
    }

    class BufferedFlow : FlowLayoutPanel
    {
        public BufferedFlow()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
    }

    enum BtnKind { Primary, Secondary, Ghost, Danger }

    class FlatButton : DarkControl
    {
        string glyph;
        public BtnKind Kind;
        public bool Dropdown;

        public string Glyph { get { return glyph; } set { glyph = value; Invalidate(); } }

        public FlatButton(string text, string glyph, BtnKind kind)
        {
            Text = text ?? "";
            this.glyph = glyph;
            Kind = kind;
            Cursor = Cursors.Hand;
            Font = Theme.Semi(9f);
            Size = new Size(S(38), S(38));
        }

        public void FitWidth()
        {
            using (var g = CreateGraphics())
            {
                int w = S(28);
                bool hasG = !string.IsNullOrEmpty(glyph), hasT = Text.Length > 0;
                if (hasG) w += S(16);
                if (hasT) w += Theme.Measure(g, Text, Font).Width;
                if (hasG && hasT) w += S(8);
                Width = w;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.HQ(g);

            Color bg, fg, border = Color.Empty;
            bool hot = Hover && Enabled;
            switch (Kind)
            {
                case BtnKind.Primary:
                    bg = Pressed ? Theme.Blend(Theme.Accent, Color.Black, 0.15f) : hot ? Theme.AccentHover : Theme.Accent;
                    fg = Color.White;
                    break;
                case BtnKind.Danger:
                    bg = hot ? Theme.Blend(Theme.Field, Theme.Danger, 0.22f) : Theme.Field;
                    fg = Theme.Danger;
                    border = hot ? Theme.Blend(Theme.Border, Theme.Danger, 0.5f) : Theme.Border;
                    break;
                case BtnKind.Ghost:
                    bg = hot ? Theme.Field : BackColor;
                    fg = hot ? Theme.Text : Theme.Muted;
                    break;
                default:
                    bg = Pressed ? Theme.Blend(Theme.Field, Color.Black, 0.2f) : hot ? Theme.Blend(Theme.Field, Color.White, 0.06f) : Theme.Field;
                    fg = Theme.Text;
                    border = hot ? Theme.Blend(Theme.Border, Theme.Accent, 0.45f) : Theme.Border;
                    break;
            }
            if (!Enabled) { fg = Color.FromArgb(90, 90, 104); if (Kind == BtnKind.Primary) bg = Theme.Field; }

            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float rad = S(9);
            Theme.Fill(g, r, rad, bg);
            if (border != Color.Empty) Theme.Stroke(g, r, rad, border);

            bool hasG = !string.IsNullOrEmpty(glyph), hasT = Text.Length > 0;
            int gw = hasG ? S(16) : 0;
            int gap = hasG && hasT ? S(8) : 0;
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;

            if (Dropdown)
            {
                int x = S(12);
                int chev = S(26);
                if (hasG) { Theme.Glyph(g, glyph, Theme.Icons(10f), Theme.AccentSoft, new Rectangle(x, 0, gw, Height)); x += gw + gap; }
                Theme.DrawText(g, Text, Font, fg, new Rectangle(x, 0, Width - x - chev, Height), flags);
                Theme.Glyph(g, G.Chevron, Theme.Icons(8f), Theme.Muted, new Rectangle(Width - chev - S(4), 0, chev, Height));
                return;
            }

            int tw = hasT ? Theme.Measure(g, Text, Font).Width : 0;
            int total = gw + gap + tw;
            int sx = Math.Max(S(6), (Width - total) / 2);
            if (hasG) Theme.Glyph(g, glyph, Theme.Icons(10f), fg, new Rectangle(sx, 0, gw, Height));
            if (hasT) Theme.DrawText(g, Text, Font, fg, new Rectangle(sx + gw + gap, 0, Width - (sx + gw + gap), Height), flags);
        }
    }

    class Slider : DarkControl
    {
        int val = 100;
        bool drag;
        public event EventHandler ValueChanged;

        public int Value
        {
            get { return val; }
            set
            {
                int v = Math.Max(0, Math.Min(100, value));
                if (v == val) return;
                val = v;
                Invalidate();
                var h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public Slider() { Height = S(24); Cursor = Cursors.Hand; }

        int Pad { get { return S(9); } }

        void SetFromX(int x)
        {
            float t = (x - Pad) / (float)Math.Max(1, Width - 2 * Pad);
            Value = (int)Math.Round(t * 100);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) { drag = true; SetFromX(e.X); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (drag) SetFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e) { drag = false; base.OnMouseUp(e); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Value += e.Delta > 0 ? 5 : -5;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.HQ(g);
            int w = Width - 2 * Pad;
            int th = S(5);
            int cy = Height / 2;
            var track = new RectangleF(Pad, cy - th / 2f, w, th);
            Theme.Fill(g, track, th / 2f, Theme.Border);
            float fx = Pad + w * val / 100f;
            if (fx - Pad >= 1)
            {
                var fill = new RectangleF(Pad, cy - th / 2f, fx - Pad, th);
                using (var path = Theme.Round(fill, th / 2f))
                using (var b = new LinearGradientBrush(new RectangleF(Pad, 0, Math.Max(1, w), 1), Theme.Accent, Theme.Pink, 0f))
                    g.FillPath(b, path);
            }
            float kr = (Hover || drag) ? S(8) : S(7);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, fx - kr, cy - kr, kr * 2, kr * 2);
            using (var p = new Pen(Theme.Accent, S(2))) g.DrawEllipse(p, fx - kr, cy - kr, kr * 2, kr * 2);
        }
    }

    class Toggle : DarkControl
    {
        bool chk;
        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return chk; }
            set
            {
                if (chk == value) return;
                chk = value;
                Invalidate();
                var h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public Toggle() { Size = new Size(S(44), S(24)); Cursor = Cursors.Hand; }

        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.HQ(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.Fill(g, r, r.Height / 2, chk ? Theme.Accent : Theme.Border);
            float d = r.Height - S(6);
            float x = chk ? r.Right - d - S(3) : r.X + S(3);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, x, r.Y + S(3), d, d);
        }
    }

    /// <summary>Kotak input teks dengan sudut membulat.</summary>
    class InputBox : DarkControl
    {
        public readonly TextBox Box = new TextBox();

        public InputBox()
        {
            Height = S(38);
            Cursor = Cursors.IBeam;
            Box.BorderStyle = BorderStyle.None;
            Box.BackColor = Theme.Field;
            Box.ForeColor = Theme.Text;
            Box.Font = Theme.UI(10f);
            Box.GotFocus += delegate { Invalidate(); };
            Box.LostFocus += delegate { Invalidate(); };
            Controls.Add(Box);
        }

        public override string Text
        {
            get { return Box.Text; }
            set { Box.Text = value; }
        }

        protected override void OnClick(EventArgs e) { Box.Focus(); base.OnClick(e); }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            Box.Width = Width - S(24);
            Box.Location = new Point(S(12), (Height - Box.Height) / 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.HQ(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.Fill(g, r, S(9), Theme.Field);
            Theme.Stroke(g, r, S(9), Box.Focused ? Theme.Accent : (Hover ? Theme.Blend(Theme.Border, Theme.Accent, 0.4f) : Theme.Border));
        }
    }

    /// <summary>Klik lalu tekan kombinasi tombol untuk set shortcut.</summary>
    class KeyCaptureBox : DarkControl
    {
        public int Key, Mods;
        bool capturing;
        int pending;
        public event EventHandler Changed;
        public bool Capturing { get { return capturing; } }

        public KeyCaptureBox()
        {
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Height = S(42);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            capturing = true;
            pending = 0;
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData) { return capturing || base.IsInputKey(keyData); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!capturing)
            {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { capturing = true; e.Handled = true; Invalidate(); }
                base.OnKeyDown(e);
                return;
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
            Keys k = e.KeyCode;
            if (k == Keys.Escape) capturing = false;
            else if (k == Keys.Back) { Key = 0; Mods = 0; capturing = false; Fire(); }
            else if (Keyb.IsModifier((int)k)) pending = Keyb.FromKeys(e.Modifiers);
            else { Key = (int)k; Mods = Keyb.FromKeys(e.Modifiers); capturing = false; Fire(); }
            Invalidate();
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (capturing) { pending = Keyb.FromKeys(e.Modifiers); Invalidate(); }
            base.OnKeyUp(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { capturing = false; Invalidate(); base.OnLostFocus(e); }

        void Fire()
        {
            var h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.HQ(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.Fill(g, r, S(9), capturing ? Theme.Blend(Theme.Field, Theme.Accent, 0.12f) : Theme.Field);
            Theme.Stroke(g, r, S(9), capturing ? Theme.Accent : (Hover || Focused ? Theme.Blend(Theme.Border, Theme.Accent, 0.4f) : Theme.Border), capturing ? 1.5f : 1f);

            Theme.Glyph(g, G.Keyboard, Theme.Icons(12f), capturing ? Theme.AccentSoft : Theme.Muted, new Rectangle(S(10), 0, S(26), Height));
            var tr = new Rectangle(S(46), 0, Width - S(56), Height);
            var fl = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            if (capturing)
            {
                string t = pending != 0 ? Keyb.Prefix(pending) + "..." : "Tekan tombol...   (Esc = batal, Backspace = hapus)";
                Theme.DrawText(g, t, Theme.Semi(9.5f), Theme.AccentSoft, tr, fl);
            }
            else if (Key == 0)
                Theme.DrawText(g, "Klik di sini lalu tekan tombol shortcut", Theme.UI(9.5f), Theme.Muted, tr, fl);
            else
                Theme.DrawText(g, Keyb.Combo(Key, Mods).ToUpperInvariant(), Theme.Semi(10.5f), Theme.Text, tr, fl);
        }
    }

    enum CardPart { None, Toggle, Edit, Play }

    /// <summary>Kartu soundboard (mirip referensi): toggle aktif, nama, tombol play, badge shortcut.</summary>
    class SoundCard : DarkControl
    {
        public readonly SoundItem Item;
        readonly Func<string, bool> isPlaying;
        CardPart hot;
        Rectangle rToggle, rEdit, rPlay;
        bool missing;

        public event EventHandler PlayClicked, EditClicked, ToggleClicked;

        public SoundCard(SoundItem item, Func<string, bool> playing)
        {
            Item = item;
            isPlaying = playing;
            Size = new Size(S(212), S(172));
            Margin = new Padding(S(8));
            missing = !File.Exists(Storage.Resolve(item.FilePath));
            CalcRects();
        }

        void CalcRects()
        {
            rToggle = new Rectangle(S(10), S(10), S(96), S(26));
            rEdit = new Rectangle(Width - S(42), S(9), S(32), S(30));
            int d = S(58);
            rPlay = new Rectangle((Width - d) / 2, S(76), d, d);
        }

        protected override void OnResize(EventArgs e) { CalcRects(); base.OnResize(e); }

        CardPart PartAt(Point p)
        {
            if (rEdit.Contains(p)) return CardPart.Edit;
            if (rToggle.Contains(p)) return CardPart.Toggle;
            float cx = rPlay.X + rPlay.Width / 2f, cy = rPlay.Y + rPlay.Height / 2f, rad = rPlay.Width / 2f + S(2);
            if ((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy) <= rad * rad) return CardPart.Play;
            return CardPart.None;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var p = PartAt(e.Location);
            if (p == hot) return;
            hot = p;
            Cursor = p == CardPart.None ? Cursors.Default : Cursors.Hand;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e) { hot = CardPart.None; base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            EventHandler h = null;
            switch (PartAt(e.Location))
            {
                case CardPart.Play: h = PlayClicked; break;
                case CardPart.Edit: h = EditClicked; break;
                case CardPart.Toggle: h = ToggleClicked; break;
            }
            if (h != null) h(this, EventArgs.Empty);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left && PartAt(e.Location) == CardPart.None && EditClicked != null)
                EditClicked(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.HQ(g);
            bool playing = isPlaying != null && isPlaying(Item.Id);
            bool on = Item.Enabled;

            var r = new RectangleF(0.75f, 0.75f, Width - 2f, Height - 2f);
            float rad = S(14);
            Theme.Fill(g, r, rad, Hover ? Theme.CardHover : Theme.Card);
            Color border = playing ? Theme.Accent : (Hover ? Theme.Blend(Theme.Border, Theme.Accent, 0.45f) : Theme.Border);
            Theme.Stroke(g, r, rad, border, playing ? 1.6f : 1f);

            // toggle aktif
            int cs = S(18);
            var cr = new Rectangle(rToggle.X + S(4), rToggle.Y + (rToggle.Height - cs) / 2, cs, cs);
            if (on)
            {
                using (var b = new SolidBrush(hot == CardPart.Toggle ? Theme.AccentHover : Theme.Accent)) g.FillEllipse(b, cr);
                Theme.Glyph(g, G.Check, Theme.Icons(7f), Color.White, new Rectangle(cr.X, cr.Y + 1, cr.Width, cr.Height));
            }
            else
            {
                using (var p = new Pen(hot == CardPart.Toggle ? Theme.Text : Theme.Muted, 1.4f)) g.DrawEllipse(p, cr);
            }
            Theme.DrawText(g, on ? "Aktif" : "Nonaktif", Theme.Semi(8.5f),
                on || hot == CardPart.Toggle ? Theme.Text : Theme.Muted,
                new Rectangle(cr.Right + S(7), rToggle.Y, S(70), rToggle.Height),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);

            // tombol edit
            if (hot == CardPart.Edit) Theme.Fill(g, rEdit, S(8), Theme.Field);
            Theme.Glyph(g, G.Edit, Theme.Icons(10f), hot == CardPart.Edit ? Theme.Text : Theme.Muted, rEdit);

            // nama
            Theme.DrawText(g, Item.Name, Theme.UI(11.5f, FontStyle.Bold), on ? Theme.Text : Theme.Muted,
                new Rectangle(S(14), S(42), Width - S(28), S(28)),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            // tombol play
            Color pc = on ? (hot == CardPart.Play ? Theme.AccentHover : Theme.Accent) : Color.FromArgb(72, 72, 88);
            if (playing)
                using (var b = new SolidBrush(Color.FromArgb(55, Theme.Accent))) g.FillEllipse(b, Rectangle.Inflate(rPlay, S(6), S(6)));
            using (var b = new LinearGradientBrush(rPlay, pc, on ? Theme.Blend(pc, Theme.Pink, 0.4f) : pc, 45f))
                g.FillEllipse(b, rPlay);
            int cx = rPlay.X + rPlay.Width / 2, cy = rPlay.Y + rPlay.Height / 2;
            using (var wb = new SolidBrush(Color.White))
            {
                if (playing)
                {
                    int sq = S(16);
                    Theme.Fill(g, new RectangleF(cx - sq / 2f, cy - sq / 2f, sq, sq), S(3), Color.White);
                }
                else
                {
                    float h = S(20), w = S(17), ox = S(2);
                    var pts = new[] {
                        new PointF(cx - w / 2 + ox, cy - h / 2),
                        new PointF(cx - w / 2 + ox, cy + h / 2),
                        new PointF(cx + w / 2 + ox, cy) };
                    using (var path = new GraphicsPath())
                    {
                        path.AddPolygon(pts);
                        g.FillPath(wb, path);
                    }
                }
            }

            // badge shortcut
            int by = Height - S(36), bh = S(24);
            string kt = Keyb.Combo(Item.Key, Item.Mods);
            string label = kt.Length == 0 ? "TANPA SHORTCUT" : kt.ToUpperInvariant();
            Font bf = Theme.Semi(7.5f);
            int bw = Math.Min(Width - S(90), Theme.Measure(g, label, bf).Width + S(16));
            var br = new Rectangle(Width - S(12) - bw, by, bw, bh);
            Theme.Fill(g, br, S(6), kt.Length == 0 ? Theme.Card : Color.FromArgb(40, 40, 52));
            Theme.Stroke(g, br, S(6), Theme.Border);
            Theme.DrawText(g, label, bf, kt.Length == 0 ? Theme.Muted : Theme.Text, br,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            // info kiri bawah
            int lx = S(14);
            Color ic = missing ? Theme.Danger : Theme.Muted;
            Theme.Glyph(g, missing ? G.Warning : G.Volume, Theme.Icons(9f), ic, new Rectangle(lx, by, S(16), bh));
            Theme.DrawText(g, missing ? "File hilang" : Item.Volume + "%", Theme.UI(8.5f), ic,
                new Rectangle(lx + S(20), by, br.X - lx - S(24), bh),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    class AddCard : DarkControl
    {
        public AddCard()
        {
            Size = new Size(S(212), S(172));
            Margin = new Padding(S(8));
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.HQ(g);
            var r = new RectangleF(1f, 1f, Width - 2.5f, Height - 2.5f);
            float rad = S(14);
            if (Hover) Theme.Fill(g, r, rad, Theme.Blend(Theme.Bg, Theme.Card, 0.7f));
            using (var path = Theme.Round(r, rad))
            using (var pen = new Pen(Hover ? Theme.Accent : Theme.Border, 1.4f))
            {
                pen.DashStyle = DashStyle.Dash;
                pen.DashPattern = new float[] { 4f, 3f };
                g.DrawPath(pen, path);
            }
            Theme.Glyph(g, G.Add, Theme.Icons(22f), Hover ? Theme.AccentSoft : Theme.Text, new Rectangle(0, S(34), Width, S(48)));
            Theme.DrawText(g, "Tambah Suara", Theme.Semi(10.5f), Theme.Text, new Rectangle(0, S(90), Width, S(24)),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            Theme.DrawText(g, "atau drag & drop file audio", Theme.UI(8.5f), Theme.Muted, new Rectangle(0, S(116), Width, S(20)),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }
}
