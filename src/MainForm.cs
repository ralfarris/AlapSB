using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Windows.Forms;

namespace AlapSB
{
    class MainForm : Form
    {
        static readonly string[] AudioExt = { ".mp3", ".wav", ".wma", ".m4a", ".aac", ".mid", ".midi" };

        readonly AppConfig cfg;
        readonly Random rnd = new Random();
        readonly int showMsg;
        readonly bool isAdmin;
        readonly Process proc = Process.GetCurrentProcess();

        AudioEngine engine;
        MusicPlayer musicPlayer;
        MusicPage musicPage;
        KeyHook hook;
        PaintPanel header, tip, status;
        BufferedFlow flow;
        FlatButton presetBtn, addPresetBtn, renamePresetBtn, delPresetBtn, musicBtn, stopBtn, settingsBtn, exitBtn;
        NotifyIcon tray;
        ContextMenuStrip menu;
        Timer statsTimer;
        long memMb;
        string flash;
        DateTime flashUntil;

        static int S(float v) { return Theme.S(v); }
        Preset Cur { get { return cfg.Presets[cfg.Current]; } }

        public MainForm()
        {
            cfg = Storage.Load();
            showMsg = Native.RegisterWindowMessage("AlapSB_Show_by_Alpha");
            try { Native.ChangeWindowMessageFilter(showMsg, 1); } catch { }
            isAdmin = IsAdmin();

            AutoScaleMode = AutoScaleMode.None;
            Text = "AlapSB";
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.UI(9f);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(S(1000), S(660));
            MinimumSize = new Size(S(820), S(480));
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            ShowIcon = true;

            BuildUI();
            BuildTray();

            statsTimer = new Timer();
            statsTimer.Interval = 2000;
            statsTimer.Tick += delegate { UpdateStats(); };
            statsTimer.Start();
            UpdateStats();
        }

        // ───────────────────────────── UI ─────────────────────────────

        void BuildUI()
        {
            flow = new BufferedFlow();
            flow.Dock = DockStyle.Fill;
            flow.AutoScroll = true;
            flow.BackColor = Theme.Bg;
            flow.Padding = new Padding(S(16), S(4), S(16), S(16));
            HookDrop(flow);

            status = new PaintPanel();
            status.Dock = DockStyle.Bottom;
            status.Height = S(32);
            status.Paint += PaintStatus;

            tip = new PaintPanel();
            tip.Dock = DockStyle.Top;
            tip.Height = S(60);
            tip.BackColor = Theme.Bg;
            tip.Paint += PaintTip;
            tip.Visible = !isAdmin;

            header = new PaintPanel();
            header.Dock = DockStyle.Top;
            header.Height = S(148);
            header.BackColor = Theme.Bg;
            header.Paint += PaintHeader;
            header.Resize += delegate { LayoutHeader(); };

            presetBtn = new FlatButton(Cur.Name, G.Library, BtnKind.Secondary);
            presetBtn.Dropdown = true;
            presetBtn.Click += delegate { ShowPresetMenu(); };
            addPresetBtn = new FlatButton("", G.Add, BtnKind.Secondary);
            addPresetBtn.Click += delegate { AddPreset(); };
            renamePresetBtn = new FlatButton("", G.Edit, BtnKind.Secondary);
            renamePresetBtn.Click += delegate { RenamePreset(); };
            delPresetBtn = new FlatButton("", G.Delete, BtnKind.Danger);
            delPresetBtn.Click += delegate { DeletePreset(); };

            musicBtn = new FlatButton("Music", G.Music, BtnKind.Primary);
            musicBtn.Click += delegate { OpenMusicPage(); };
            stopBtn = new FlatButton("Stop Semua", G.Stop, BtnKind.Secondary);
            stopBtn.Click += delegate { if (engine != null) engine.StopAll(); if (musicPlayer != null) musicPlayer.Stop(); };
            settingsBtn = new FlatButton("", G.Settings, BtnKind.Secondary);
            settingsBtn.Click += delegate { ShowSettings(); };
            exitBtn = new FlatButton("Keluar", G.Power, BtnKind.Danger);
            exitBtn.Click += delegate { ExitApp(); };

            header.Controls.AddRange(new Control[] { presetBtn, addPresetBtn, renamePresetBtn, delPresetBtn, musicBtn, stopBtn, settingsBtn, exitBtn });

            var tt = new ToolTip();
            tt.SetToolTip(presetBtn, "Ganti preset soundboard");
            tt.SetToolTip(addPresetBtn, "Preset baru");
            tt.SetToolTip(renamePresetBtn, "Ganti nama preset");
            tt.SetToolTip(delPresetBtn, "Hapus preset");
            tt.SetToolTip(musicBtn, "Buka Music Player");
            tt.SetToolTip(settingsBtn, "Pengaturan");
            tt.SetToolTip(exitBtn, "Tutup & keluar dari aplikasi");

            Controls.Add(flow);
            Controls.Add(status);
            Controls.Add(tip);
            Controls.Add(header);
            HookDrop(this);
        }

        void LayoutHeader()
        {
            int pad = S(24), y = S(98), h = S(38);
            int x = header.Width - pad;
            exitBtn.FitWidth();
            exitBtn.SetBounds(x - exitBtn.Width, y, exitBtn.Width, h);
            x = exitBtn.Left - S(8);
            settingsBtn.SetBounds(x - h, y, h, h);
            x = settingsBtn.Left - S(8);
            stopBtn.FitWidth();
            stopBtn.SetBounds(x - stopBtn.Width, y, stopBtn.Width, h);
            x = stopBtn.Left - S(8);
            musicBtn.FitWidth();
            musicBtn.SetBounds(x - musicBtn.Width, y, musicBtn.Width, h);

            int maxPresetRight = musicBtn.Left - S(16);
            int actionsW = 3 * (h + S(8));
            int pw = Math.Max(S(120), Math.Min(S(190), maxPresetRight - actionsW - (pad + S(130))));
            presetBtn.SetBounds(pad + S(130), y, pw, h);
            addPresetBtn.SetBounds(presetBtn.Right + S(8), y, h, h);
            renamePresetBtn.SetBounds(addPresetBtn.Right + S(8), y, h, h);
            delPresetBtn.SetBounds(renamePresetBtn.Right + S(8), y, h, h);
            header.Invalidate();
        }

        void PaintHeader(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            Theme.HQ(g);
            int pad = S(24);

            // judul
            var titleFont = Theme.UI(20f, FontStyle.Bold);
            int tx = pad;
            var ts = Theme.Measure(g, "AlapSB", titleFont);
            Theme.DrawText(g, "AlapSB", titleFont, Theme.Text, new Rectangle(tx, S(18), ts.Width + S(4), S(40)),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);

            Theme.DrawText(g, "Soundboard ringan buat streaming — masukin suara, pilih shortcut, langsung gas.",
                Theme.UI(9.5f), Theme.Muted, new Rectangle(tx, S(58), header.Width - tx - pad, S(22)),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            // label preset
            int y = S(98), h = S(38);
            Theme.DrawText(g, "Preset Soundboard", Theme.Semi(9.5f), Theme.Text, new Rectangle(pad, y, S(126), h),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);
        }

        void PaintTip(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            Theme.HQ(g);
            var r = new Rectangle(S(24), S(4), tip.Width - S(48), tip.Height - S(14));
            Theme.Fill(g, r, S(10), Color.FromArgb(25, 25, 35));
            Theme.Stroke(g, r, S(10), Theme.Border);
            Theme.Fill(g, new Rectangle(r.X, r.Y, S(4), r.Height), S(2), Theme.Accent);
            Theme.Glyph(g, G.Bulb, Theme.Icons(12f), Theme.AccentSoft, new Rectangle(r.X + S(14), r.Y, S(22), r.Height));
            Theme.DrawText(g, "Tips! Kalau shortcut nggak jalan pas lagi main game, jalankan AlapSB sebagai Administrator (ikon ⚙ → Jalankan sebagai Administrator).",
                Theme.UI(9f), Theme.Text, new Rectangle(r.X + S(46), r.Y, r.Width - S(56), r.Height),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        void PaintStatus(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Panel);
            Theme.HQ(g);
            using (var p = new Pen(Theme.Border)) g.DrawLine(p, 0, 0, status.Width, 0);
            int pad = S(24), h = status.Height;
            bool fl = flash != null && DateTime.Now < flashUntil;
            bool hk = hook != null && hook.Active;
            int d = S(8);
            using (var b = new SolidBrush(fl ? Theme.Pink : (hk ? Theme.Green : Theme.Danger)))
                g.FillEllipse(b, pad, (h - d) / 2, d, d);
            string left = fl ? flash :
                (hk ? "Shortcut global aktif" : "Shortcut global tidak aktif") +
                "   ·   " + Cur.Sounds.Count + " suara   ·   RAM " + memMb + " MB";
            Theme.DrawText(g, left, Theme.UI(8.5f), fl ? Theme.Text : Theme.Muted,
                new Rectangle(pad + d + S(10), 0, status.Width - pad * 2 - S(220), h),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            Theme.DrawText(g, "AlapSB v1.0", Theme.Semi(8.5f), Theme.Muted, new Rectangle(0, 0, status.Width - pad, h),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.SingleLine);
        }

        void UpdateStats()
        {
            try { proc.Refresh(); memMb = proc.WorkingSet64 / (1024 * 1024); } catch { }
            if (status != null) status.Invalidate();
        }

        void Flash(string msg)
        {
            flash = msg;
            flashUntil = DateTime.Now.AddSeconds(5);
            status.Invalidate();
        }

        void RebuildCards()
        {
            flow.SuspendLayout();
            var old = new List<Control>();
            foreach (Control c in flow.Controls) old.Add(c);
            flow.Controls.Clear();
            foreach (var c in old) c.Dispose();

            foreach (var it in Cur.Sounds)
            {
                var item = it;
                var card = new SoundCard(item, IsPlaying);
                card.PlayClicked += delegate { TogglePlay(item); };
                card.EditClicked += delegate { EditItem(item); };
                card.ToggleClicked += delegate { item.Enabled = !item.Enabled; Save(); card.Invalidate(); };
                card.MouseUp += (s, e) => { if (e.Button == MouseButtons.Right) ShowCardMenu(card, e.Location); };
                HookDrop(card);
                flow.Controls.Add(card);
            }
            var add = new AddCard();
            add.Click += delegate { EditItem(null); };
            HookDrop(add);
            flow.Controls.Add(add);
            flow.ResumeLayout();

            presetBtn.Text = Cur.Name;
            delPresetBtn.Enabled = cfg.Presets.Count > 1;
            status.Invalidate();
        }

        bool IsPlaying(string id) { return engine != null && engine.IsPlaying(id); }

        // ─────────────────────────── Audio / Hotkey ───────────────────────────

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTitle(this);
            if (engine != null) return;
            engine = new AudioEngine(Handle);
            engine.Changed += delegate
            {
                foreach (Control c in flow.Controls) if (c is SoundCard) c.Invalidate();
            };
            musicPlayer = new MusicPlayer(Handle);
            musicPlayer.StateChanged += delegate { if (status != null) status.Invalidate(); };
            EnsureDefaultMusicPlaylist();
            musicPage = new MusicPage(cfg, musicPlayer, CloseMusicPage, Save);
            musicPage.Visible = false;
            Controls.Add(musicPage);
            musicPage.BringToFront();

            hook = new KeyHook();
            hook.KeyPressed += (vk, mods) => BeginInvoke((Action)(() => OnGlobalKey(vk, mods)));
            hook.Install(Handle);
            RebuildCards();
        }

        void EnsureDefaultMusicPlaylist()
        {
            try
            {
                Directory.CreateDirectory(Storage.MusicDir);

                // Ekstrak lagu bawaan yang di-bundle di dalam .exe ke folder music
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                const string prefix = "AlapSB.Music.";
                foreach (var resName in asm.GetManifestResourceNames())
                {
                    if (!resName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    string fileName = resName.Substring(prefix.Length);
                    string targetPath = Path.Combine(Storage.MusicDir, fileName);
                    if (!File.Exists(targetPath))
                    {
                        using (var src = asm.GetManifestResourceStream(resName))
                        using (var dst = File.Create(targetPath))
                        {
                            if (src != null) src.CopyTo(dst);
                        }
                    }
                }

                bool changed = false;
                var files = Directory.GetFiles(Storage.MusicDir, "*.mp3");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (var f in files)
                {
                    string rel = "music\\" + Path.GetFileName(f);
                    bool exists = false;
                    foreach (var t in cfg.Playlist)
                    {
                        if (string.Equals(t.FilePath, rel, StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists)
                    {
                        var tr = new MusicTrack();
                        string raw = Path.GetFileNameWithoutExtension(f);
                        int dash = raw.IndexOf(" - ");
                        if (dash > 0)
                        {
                            tr.Title = raw.Substring(0, dash).Trim();
                            tr.Artist = raw.Substring(dash + 3).Trim();
                        }
                        else
                        {
                            tr.Title = raw;
                            tr.Artist = "TWICE";
                        }
                        tr.FilePath = rel;
                        cfg.Playlist.Add(tr);
                        changed = true;
                    }
                }
                if (changed) Save();
            }
            catch { }
        }

        void OpenMusicPage()
        {
            if (musicPage == null) return;
            header.Visible = false;
            tip.Visible = false;
            flow.Visible = false;
            musicPage.Visible = true;
            musicPage.BringToFront();
            musicPage.RebuildPlaylist();
        }

        void CloseMusicPage()
        {
            if (musicPage == null) return;
            musicPage.Visible = false;
            header.Visible = true;
            tip.Visible = !isAdmin;
            flow.Visible = true;
            status.Invalidate();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (Cur.Sounds.Count == 0) Flash("Klik \"Tambah Suara\" atau drag & drop file audio ke jendela ini buat mulai.");
        }

        void OnGlobalKey(int vk, int mods)
        {
            if (cfg.StopKey != 0 && vk == cfg.StopKey && (cfg.StopMods == 0 || (mods & cfg.StopMods) == cfg.StopMods))
            {
                engine.StopAll();
                return;
            }

            var matches = new List<SoundItem>();
            foreach (var s in Cur.Sounds)
            {
                if (!s.Enabled || s.Key != vk) continue;
                // Cocok jika modifier tepat sama, ATAU jika sound tidak butuh modifier tapi ditekan bersama modifier lain (misal gamer sedang tahan Shift/Ctrl)
                if (s.Mods == mods || s.Mods == 0)
                {
                    matches.Add(s);
                }
            }

            if (matches.Count == 0) return;

            // Prioritaskan yang modifier-nya match persis jika ada
            SoundItem target = null;
            foreach (var m in matches)
            {
                if (m.Mods == mods) { target = m; break; }
            }
            if (target == null)
            {
                target = matches[rnd.Next(matches.Count)];
            }

            // Langsung mainkan suaranya (tidak pakai toggle stop) agar setiap trigger pasti bunyi
            PlayItem(target);
        }

        void PlayItem(SoundItem it)
        {
            if (!engine.Play(it.Id, Storage.Resolve(it.FilePath), it.Volume))
                Flash(it.Name + ": " + engine.LastError);
        }

        void TogglePlay(SoundItem it)
        {
            if (engine.IsPlaying(it.Id)) engine.Stop(it.Id);
            else PlayItem(it);
        }

        protected override void WndProc(ref Message m)
        {
            if (hook != null) hook.HandleRawMessage(ref m);
            if (musicPlayer != null && musicPlayer.HandleMessage(ref m)) return;
            if (engine != null && engine.HandleMessage(ref m)) return;
            if (m.Msg == showMsg && showMsg != 0) { ShowMain(); return; }
            base.WndProc(ref m);
        }

        // ─────────────────────────── Aksi ───────────────────────────

        DialogResult RunDialog(Form f)
        {
            hook.Suspended = true;
            try { return f.ShowDialog(this); }
            finally { hook.Suspended = false; }
        }

        void EditItem(SoundItem item)
        {
            bool isNew = item == null;
            var work = isNew ? new SoundItem() : item.Clone();
            using (var f = new EditForm(work, isNew, engine, 100))
            {
                var r = RunDialog(f);
                if (r == DialogResult.OK)
                {
                    work.FilePath = Storage.Import(work.FilePath);
                    if (isNew) Cur.Sounds.Add(work);
                    else
                    {
                        int i = Cur.Sounds.IndexOf(item);
                        if (i >= 0) Cur.Sounds[i] = work; else Cur.Sounds.Add(work);
                    }
                }
                else if (r == DialogResult.Abort && !isNew)
                {
                    engine.Stop(item.Id);
                    Cur.Sounds.Remove(item);
                }
                else return;
            }
            Save();
            RebuildCards();
        }

        void DeleteItem(SoundItem it)
        {
            if (MessageBox.Show(this, "Hapus suara \"" + it.Name + "\"?", "AlapSB", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            engine.Stop(it.Id);
            Cur.Sounds.Remove(it);
            Save();
            RebuildCards();
        }

        ContextMenuStrip ResetMenu()
        {
            if (menu != null) menu.Dispose();
            menu = Theme.NewMenu();
            return menu;
        }

        void ShowCardMenu(SoundCard card, Point p)
        {
            var it = card.Item;
            var m = ResetMenu();
            m.Items.Add(engine.IsPlaying(it.Id) ? "Stop" : "Putar").Click += delegate { TogglePlay(it); };
            m.Items.Add("Edit...").Click += delegate { EditItem(it); };
            m.Items.Add("Duplikat").Click += delegate
            {
                var d = it.Clone();
                d.Id = SoundItem.NewId();
                d.Name = it.Name + " (copy)";
                Cur.Sounds.Insert(Cur.Sounds.IndexOf(it) + 1, d);
                Save();
                RebuildCards();
            };
            m.Items.Add(it.Enabled ? "Nonaktifkan" : "Aktifkan").Click += delegate { it.Enabled = !it.Enabled; Save(); card.Invalidate(); };
            if (cfg.Presets.Count > 1)
            {
                var move = new ToolStripMenuItem("Pindah ke preset");
                for (int i = 0; i < cfg.Presets.Count; i++)
                {
                    if (i == cfg.Current) continue;
                    var target = cfg.Presets[i];
                    move.DropDownItems.Add(target.Name).Click += delegate
                    {
                        Cur.Sounds.Remove(it);
                        target.Sounds.Add(it);
                        Save();
                        RebuildCards();
                    };
                }
                move.DropDown.Renderer = new DarkRenderer();
                m.Items.Add(move);
            }
            m.Items.Add(new ToolStripSeparator());
            var del = m.Items.Add("Hapus");
            del.Tag = "danger";
            del.Click += delegate { DeleteItem(it); };
            m.Show(card, p);
        }

        void ShowPresetMenu()
        {
            var m = ResetMenu();
            for (int i = 0; i < cfg.Presets.Count; i++)
            {
                int idx = i;
                var item = new ToolStripMenuItem(cfg.Presets[i].Name + "   (" + cfg.Presets[i].Sounds.Count + ")");
                item.Checked = i == cfg.Current;
                item.Click += delegate { cfg.Current = idx; Save(); RebuildCards(); };
                m.Items.Add(item);
            }
            m.Items.Add(new ToolStripSeparator());
            var add = m.Items.Add("+  Preset baru...");
            add.Tag = "accent";
            add.Click += delegate { AddPreset(); };
            m.MinimumSize = new Size(presetBtn.Width, 0);
            m.Show(presetBtn, new Point(0, presetBtn.Height + S(4)));
        }

        string Ask(string title, string label, string initial)
        {
            using (var f = new PromptForm(title, label, initial))
                return RunDialog(f) == DialogResult.OK ? f.Value : null;
        }

        void AddPreset()
        {
            string n = Ask("Preset baru", "Nama preset", "Preset " + (cfg.Presets.Count + 1));
            if (n == null) return;
            var p = new Preset();
            p.Name = n;
            cfg.Presets.Add(p);
            cfg.Current = cfg.Presets.Count - 1;
            Save();
            RebuildCards();
        }

        void RenamePreset()
        {
            string n = Ask("Ganti nama preset", "Nama preset", Cur.Name);
            if (n == null) return;
            Cur.Name = n;
            Save();
            RebuildCards();
        }

        void DeletePreset()
        {
            if (cfg.Presets.Count <= 1) return;
            if (MessageBox.Show(this, "Hapus preset \"" + Cur.Name + "\" beserta " + Cur.Sounds.Count + " suara di dalamnya?", "AlapSB",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            engine.StopAll();
            cfg.Presets.RemoveAt(cfg.Current);
            cfg.Current = Math.Max(0, cfg.Current - 1);
            Save();
            RebuildCards();
        }

        void ShowSettings()
        {
            var m = ResetMenu();
            m.Items.Add("Shortcut Stop Semua:  " + (cfg.StopKey == 0 ? "belum di-set" : Keyb.Combo(cfg.StopKey, cfg.StopMods)))
                .Click += delegate { SetStopKey(); };
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Buka folder suara").Click += delegate
            {
                try { Directory.CreateDirectory(Storage.SoundsDir); Process.Start("explorer.exe", "\"" + Storage.SoundsDir + "\""); } catch { }
            };
            if (!isAdmin)
            {
                var adm = m.Items.Add("Jalankan sebagai Administrator");
                adm.Tag = "accent";
                adm.Click += delegate { RestartAsAdmin(); };
            }
            m.Items.Add("Tentang AlapSB").Click += delegate
            {
                MessageBox.Show(this, "AlapSB v1.0\nSoundboard ringan untuk streaming.", "Tentang AlapSB",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            var uninst = m.Items.Add("Uninstall");
            uninst.Tag = "danger";
            uninst.Click += delegate { UninstallApp(); };
            m.Items.Add(new ToolStripSeparator());
            var quit = m.Items.Add("Keluar");
            quit.Tag = "danger";
            quit.Click += delegate { ExitApp(); };
            m.Show(settingsBtn, new Point(settingsBtn.Width, settingsBtn.Height + S(4)), ToolStripDropDownDirection.BelowLeft);
        }

        void SetStopKey()
        {
            using (var f = new KeyPromptForm("Shortcut Stop Semua", "Shortcut untuk menghentikan semua suara", cfg.StopKey, cfg.StopMods))
            {
                if (RunDialog(f) != DialogResult.OK) return;
                cfg.StopKey = f.Key;
                cfg.StopMods = f.Mods;
                Save();
            }
        }

        void UninstallApp()
        {
            var res = MessageBox.Show(this,
                "Are you sure you want to uninstall AlapSB?",
                "Uninstall AlapSB",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res != DialogResult.Yes) return;

            MessageBox.Show(this,
                "Terima kasih sudah menggunakan AlapSB!\n\nSampai jumpa lagi di live stream berikutnya! 👋",
                "Selamat Tinggal!",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            try
            {
                if (engine != null) engine.StopAll();
                if (musicPlayer != null) musicPlayer.Stop();
                if (hook != null) hook.Dispose();
                if (tray != null) { tray.Visible = false; tray.Dispose(); }

                string exePath = Application.ExecutablePath;
                string exeDir = Path.GetDirectoryName(exePath);
                string jsonPath = Storage.ConfigPath;
                string soundsDir = Storage.SoundsDir;
                string musicDir = Storage.MusicDir;

                // Script self-delete setelah aplikasi exit
                string cmd = string.Format(
                    "/c ping 127.0.0.1 -n 2 > nul & del /f /q \"{0}\" & if exist \"{1}\" del /f /q \"{1}\" & if exist \"{2}\" rmdir /s /q \"{2}\" & if exist \"{3}\" rmdir /s /q \"{3}\"",
                    exePath, jsonPath, soundsDir, musicDir);

                var psi = new ProcessStartInfo("cmd.exe", cmd)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch { }

            ExitApp();
        }

        static bool IsAdmin()
        {
            try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
            catch { return false; }
        }

        void RestartAsAdmin()
        {
            try
            {
                var psi = new ProcessStartInfo(Application.ExecutablePath, "--restart");
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                ExitApp();
            }
            catch (Win32Exception) { /* UAC dibatalkan */ }
        }

        // ─────────────────────────── Drag & drop ───────────────────────────

        void HookDrop(Control c)
        {
            c.AllowDrop = true;
            c.DragEnter += OnDragEnterAny;
            c.DragDrop += OnDragDropAny;
        }

        void OnDragEnterAny(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        void OnDragDropAny(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return;
            int added = 0;
            foreach (var f in files)
            {
                if (Array.IndexOf(AudioExt, Path.GetExtension(f).ToLowerInvariant()) < 0) continue;
                var s = new SoundItem();
                s.Name = Path.GetFileNameWithoutExtension(f);
                s.FilePath = Storage.Import(f);
                Cur.Sounds.Add(s);
                added++;
            }
            if (added == 0) { Flash("Format tidak didukung. Pakai mp3, wav, wma, m4a, atau aac ya."); return; }
            Save();
            RebuildCards();
            Flash(added + " suara ditambahkan. Klik ikon pensil di kartu buat set shortcut-nya.");
        }

        // ─────────────────────────── Tray & lifecycle ───────────────────────────

        void BuildTray()
        {
            var tm = Theme.NewMenu();
            tm.Items.Add("Tampilkan AlapSB").Click += delegate { ShowMain(); };
            tm.Items.Add("Stop semua suara").Click += delegate { if (engine != null) engine.StopAll(); };
            tm.Items.Add(new ToolStripSeparator());
            var q = tm.Items.Add("Keluar");
            q.Tag = "danger";
            q.Click += delegate { ExitApp(); };

            tray = new NotifyIcon();
            tray.Icon = Icon ?? SystemIcons.Application;
            tray.Text = "AlapSB";
            tray.ContextMenuStrip = tm;
            tray.Visible = true;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowMain(); };
        }

        void ShowMain()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        void ExitApp()
        {
            Close();
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Save();
            if (hook != null) hook.Dispose();
            if (engine != null) engine.StopAll();
            if (musicPlayer != null) musicPlayer.Stop();
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            base.OnFormClosed(e);
        }

        void Save() { Storage.Save(cfg); }
    }
}
