using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AlapSB
{
    class DarkDialog : Form
    {
        public DarkDialog()
        {
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Panel;
            ForeColor = Theme.Text;
            Font = Theme.UI(9.5f);
            DoubleBuffered = true;
        }

        protected static int S(float v) { return Theme.S(v); }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTitle(this);
        }

        protected virtual bool CanEscape() { return true; }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && CanEscape()) { DialogResult = DialogResult.Cancel; return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected Label AddLabel(string text, int x, int y, bool muted)
        {
            var l = new Label();
            l.AutoSize = true;
            l.UseMnemonic = false;
            l.Text = text;
            l.Font = muted ? Theme.UI(8.5f) : Theme.Semi(9.5f);
            l.ForeColor = muted ? Theme.Muted : Theme.Text;
            l.BackColor = Color.Transparent;
            l.Location = new Point(x, y);
            Controls.Add(l);
            return l;
        }

        protected FlatButton AddButton(string text, string glyph, BtnKind kind, int x, int y, int w)
        {
            var b = new FlatButton(text, glyph, kind);
            b.SetBounds(x, y, w, S(38));
            Controls.Add(b);
            return b;
        }

        protected int FooterY { get { return ClientSize.Height - S(62); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, FooterY - S(12), ClientSize.Width, FooterY - S(12));
        }
    }

    /// <summary>Dialog tambah / edit suara.</summary>
    class EditForm : DarkDialog
    {
        const string PreviewKey = "__preview__";
        readonly SoundItem item;
        readonly AudioEngine engine;
        readonly int master;
        string filePath;

        readonly InputBox nameBox, fileBox;
        readonly FlatButton previewBtn;
        readonly KeyCaptureBox keyBox;
        readonly Slider vol;
        readonly Toggle enabled;
        readonly Label volLabel;

        public EditForm(SoundItem it, bool isNew, AudioEngine eng, int masterVolume)
        {
            item = it;
            engine = eng;
            master = masterVolume;
            filePath = it.FilePath;

            Text = isNew ? "Tambah Suara  ·  AlapSB" : "Edit Suara  ·  AlapSB";
            ClientSize = new Size(S(470), S(470));
            int x = S(24), w = ClientSize.Width - 2 * x, y = S(20);

            AddLabel("Nama suara", x, y, false); y += S(26);
            nameBox = new InputBox();
            nameBox.SetBounds(x, y, w, S(38));
            nameBox.Text = it.Name;
            Controls.Add(nameBox);
            y += S(54);

            AddLabel("File audio", x, y, false);
            var hint = AddLabel("mp3 · wav · wma · m4a", 0, y + S(2), true);
            hint.Left = x + w - hint.PreferredWidth;
            y += S(26);
            fileBox = new InputBox();
            fileBox.Box.ReadOnly = true;
            fileBox.SetBounds(x, y, w - S(150), S(38));
            fileBox.Text = DisplayName(filePath);
            Controls.Add(fileBox);
            var browse = AddButton("Pilih", G.Folder, BtnKind.Secondary, fileBox.Right + S(8), y, S(94));
            browse.Click += delegate { Browse(); };
            previewBtn = AddButton("", G.Play, BtnKind.Secondary, browse.Right + S(8), y, S(40));
            previewBtn.Click += delegate { Preview(); };
            y += S(54);

            AddLabel("Shortcut keyboard", x, y, false);
            var khint = AddLabel("bisa pakai Ctrl / Shift / Alt", 0, y + S(2), true);
            khint.Left = x + w - khint.PreferredWidth;
            y += S(26);
            keyBox = new KeyCaptureBox();
            keyBox.SetBounds(x, y, w, S(42));
            keyBox.Key = it.Key;
            keyBox.Mods = it.Mods;
            Controls.Add(keyBox);
            y += S(58);

            AddLabel("Volume", x, y, false);
            volLabel = AddLabel(it.Volume + "%", 0, y, false);
            volLabel.ForeColor = Theme.AccentSoft;
            volLabel.Left = x + w - S(40);
            y += S(26);
            vol = new Slider();
            vol.SetBounds(x - S(4), y, w + S(8), S(26));
            vol.Value = it.Volume;
            vol.ValueChanged += delegate { volLabel.Text = vol.Value + "%"; };
            Controls.Add(vol);
            y += S(44);

            enabled = new Toggle();
            enabled.Location = new Point(x, y);
            enabled.Checked = it.Enabled;
            Controls.Add(enabled);
            AddLabel("Aktifkan shortcut suara ini", x + S(54), y + S(3), false);

            int fy = FooterY;
            var save = AddButton("Simpan", G.Save, BtnKind.Primary, ClientSize.Width - x - S(118), fy, S(118));
            save.Click += delegate { Save(); };
            var cancel = AddButton("Batal", null, BtnKind.Secondary, save.Left - S(98), fy, S(90));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; };
            if (!isNew)
            {
                var del = AddButton("Hapus", G.Delete, BtnKind.Danger, x, fy, S(104));
                del.Click += delegate
                {
                    if (MessageBox.Show(this, "Hapus suara \"" + item.Name + "\"?", "AlapSB", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        DialogResult = DialogResult.Abort;
                };
            }

            engine.Changed += OnEngineChanged;
        }

        static string DisplayName(string p)
        {
            return string.IsNullOrEmpty(p) ? "" : Path.GetFileName(p);
        }

        protected override bool CanEscape() { return !keyBox.Capturing; }

        void OnEngineChanged(object s, EventArgs e)
        {
            previewBtn.Glyph = engine.IsPlaying(PreviewKey) ? G.Stop : G.Play;
        }

        void Browse()
        {
            using (var d = new OpenFileDialog())
            {
                d.Title = "Pilih file audio";
                d.Filter = "File audio|*.mp3;*.wav;*.wma;*.m4a;*.aac;*.mid;*.midi|Semua file|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string oldAuto = string.IsNullOrEmpty(filePath) ? "" : Path.GetFileNameWithoutExtension(filePath);
                filePath = d.FileName;
                fileBox.Text = DisplayName(filePath);
                string n = nameBox.Text.Trim();
                if (n.Length == 0 || n == "Suara baru" || n == oldAuto)
                    nameBox.Text = Path.GetFileNameWithoutExtension(filePath);
            }
        }

        void Preview()
        {
            if (engine.IsPlaying(PreviewKey)) { engine.Stop(PreviewKey); return; }
            if (string.IsNullOrEmpty(filePath)) { Browse(); return; }
            if (!engine.Play(PreviewKey, Storage.Resolve(filePath), vol.Value * master / 100))
                MessageBox.Show(this, engine.LastError, "AlapSB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        void Save()
        {
            if (string.IsNullOrEmpty(filePath))
            {
                MessageBox.Show(this, "Pilih file audio dulu ya.", "AlapSB", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string n = nameBox.Text.Trim();
            item.Name = n.Length > 0 ? n : Path.GetFileNameWithoutExtension(filePath);
            item.FilePath = filePath;
            item.Key = keyBox.Key;
            item.Mods = keyBox.Mods;
            item.Volume = vol.Value;
            item.Enabled = enabled.Checked;
            DialogResult = DialogResult.OK;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            engine.Changed -= OnEngineChanged;
            engine.Stop(PreviewKey);
            base.OnFormClosed(e);
        }
    }

    class PromptForm : DarkDialog
    {
        readonly InputBox box;
        public string Value { get { return box.Text.Trim(); } }

        public PromptForm(string title, string label, string initial)
        {
            Text = title;
            ClientSize = new Size(S(400), S(184));
            int x = S(22), w = ClientSize.Width - 2 * x;
            AddLabel(label, x, S(20), false);
            box = new InputBox();
            box.SetBounds(x, S(48), w, S(38));
            box.Text = initial ?? "";
            Controls.Add(box);
            var ok = AddButton("Simpan", null, BtnKind.Primary, ClientSize.Width - x - S(100), FooterY, S(100));
            ok.Click += delegate { Accept(); };
            var cancel = AddButton("Batal", null, BtnKind.Secondary, ok.Left - S(98), FooterY, S(90));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; };
            box.Box.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Accept(); }
            };
        }

        void Accept() { if (Value.Length > 0) DialogResult = DialogResult.OK; }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            box.Box.Focus();
            box.Box.SelectAll();
        }
    }

    class KeyPromptForm : DarkDialog
    {
        readonly KeyCaptureBox keyBox;
        public int Key { get { return keyBox.Key; } }
        public int Mods { get { return keyBox.Mods; } }

        public KeyPromptForm(string title, string label, int key, int mods)
        {
            Text = title;
            ClientSize = new Size(S(420), S(188));
            int x = S(22), w = ClientSize.Width - 2 * x;
            AddLabel(label, x, S(20), false);
            keyBox = new KeyCaptureBox();
            keyBox.SetBounds(x, S(48), w, S(42));
            keyBox.Key = key;
            keyBox.Mods = mods;
            Controls.Add(keyBox);
            var ok = AddButton("Simpan", null, BtnKind.Primary, ClientSize.Width - x - S(100), FooterY, S(100));
            ok.Click += delegate { DialogResult = DialogResult.OK; };
            var cancel = AddButton("Batal", null, BtnKind.Secondary, ok.Left - S(98), FooterY, S(90));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; };
        }

        protected override bool CanEscape() { return !keyBox.Capturing; }
    }
}
