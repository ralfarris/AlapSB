using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace AlapSB
{
    class TrackRow : DarkControl
    {
        public readonly MusicTrack Track;
        public readonly int Index;
        readonly Func<bool> isCurrent;
        readonly Func<bool> isPlaying;
        Rectangle rPlay, rDel;
        int hotPart; // 0=none, 1=play, 2=delete

        public event EventHandler PlayClicked, DeleteClicked;

        public TrackRow(int index, MusicTrack track, Func<bool> current, Func<bool> playing)
        {
            Index = index;
            Track = track;
            isCurrent = current;
            isPlaying = playing;
            Height = S(54);
            Margin = new Padding(0, 0, 0, S(6));
            Cursor = Cursors.Hand;
            CalcRects();
        }

        void CalcRects()
        {
            rPlay = new Rectangle(S(14), (Height - S(34)) / 2, S(34), S(34));
            rDel = new Rectangle(Width - S(46), (Height - S(32)) / 2, S(32), S(32));
        }

        protected override void OnResize(EventArgs e) { CalcRects(); base.OnResize(e); }

        int PartAt(Point p)
        {
            if (rDel.Contains(p)) return 2;
            if (rPlay.Contains(p)) return 1;
            return 0;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int p = PartAt(e.Location);
            if (p != hotPart) { hotPart = p; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { hotPart = 0; base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            if (PartAt(e.Location) == 2)
            {
                if (DeleteClicked != null) DeleteClicked(this, EventArgs.Empty);
            }
            else
            {
                if (PlayClicked != null) PlayClicked(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            Theme.HQ(g);

            bool cur = isCurrent != null && isCurrent();
            bool play = isPlaying != null && isPlaying();

            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color bg = cur ? Theme.Blend(Theme.Card, Theme.Accent, 0.16f) : (Hover ? Theme.CardHover : Theme.Card);
            Theme.Fill(g, r, S(10), bg);
            Color border = cur ? Theme.Accent : (Hover ? Theme.Blend(Theme.Border, Theme.Accent, 0.35f) : Theme.Border);
            Theme.Stroke(g, r, S(10), border, cur ? 1.4f : 1f);

            // Tombol/indikator kiri
            if (Hover || cur)
            {
                Theme.Fill(g, rPlay, rPlay.Width / 2f, cur ? Theme.Accent : Theme.Field);
                Theme.Glyph(g, play ? G.Pause : G.Play, Theme.Icons(10f), Color.White, rPlay);
            }
            else
            {
                Theme.DrawText(g, (Index + 1).ToString(), Theme.Semi(9.5f), Theme.Muted, rPlay,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }

            // Judul & Artis
            int tx = rPlay.Right + S(14);
            int tw = rDel.Left - tx - S(12);
            Theme.DrawText(g, Track.Title, Theme.Semi(10.5f), cur ? Theme.AccentSoft : Theme.Text,
                new Rectangle(tx, S(7), tw, S(22)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            Theme.DrawText(g, Track.Artist, Theme.UI(8.5f), Theme.Muted,
                new Rectangle(tx, S(28), tw, S(18)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            // Tombol hapus kanan
            if (hotPart == 2) Theme.Fill(g, rDel, S(8), Theme.Blend(Theme.Field, Theme.Danger, 0.25f));
            Theme.Glyph(g, G.Delete, Theme.Icons(9.5f), hotPart == 2 ? Theme.Danger : (Hover ? Theme.Text : Theme.Muted), rDel);
        }
    }

    /// <summary>Halaman Music Player ala Spotify (lengkap dengan Back, Playlist TWICE, Bottom Player Bar).</summary>
    class MusicPage : PaintPanel
    {
        readonly AppConfig cfg;
        readonly MusicPlayer player;
        readonly Action onBack;
        readonly Action onSave;
        readonly Random rnd = new Random();

        readonly PaintPanel topBar, bottomBar;
        readonly Panel listContainer;
        readonly FlatButton backBtn, addSongBtn;
        readonly FlatButton prevBtn, playPauseBtn, nextBtn, shuffleBtn, loopBtn;
        readonly Slider seekBar, volBar;
        readonly Timer uiTimer;
        ContextMenuStrip addMenu;
        bool seeking;

        static int S(float v) { return Theme.S(v); }

        public MusicPage(AppConfig config, MusicPlayer musicPlayer, Action backCallback, Action saveCallback)
        {
            cfg = config;
            player = musicPlayer;
            onBack = backCallback;
            onSave = saveCallback;
            Dock = DockStyle.Fill;
            BackColor = Theme.Bg;

            // Top bar
            topBar = new PaintPanel();
            topBar.Dock = DockStyle.Top;
            topBar.Height = S(138);
            topBar.BackColor = Theme.Bg;
            topBar.Paint += PaintTopBar;
            topBar.Resize += delegate { LayoutTopBar(); };

            backBtn = new FlatButton("Kembali ke Soundboard", G.Back, BtnKind.Secondary);
            backBtn.Click += delegate { if (onBack != null) onBack(); };
            addSongBtn = new FlatButton("Tambah Lagu", G.Add, BtnKind.Primary);
            addSongBtn.Click += delegate { ShowAddSongMenu(); };
            topBar.Controls.AddRange(new Control[] { backBtn, addSongBtn });

            // Bottom player bar (ala Spotify)
            bottomBar = new PaintPanel();
            bottomBar.Dock = DockStyle.Bottom;
            bottomBar.Height = S(92);
            bottomBar.BackColor = Theme.Panel;
            bottomBar.Paint += PaintBottomBar;
            bottomBar.Resize += delegate { LayoutBottomBar(); };

            shuffleBtn = new FlatButton("", G.Shuffle, cfg.MusicShuffle ? BtnKind.Primary : BtnKind.Ghost);
            shuffleBtn.Click += delegate
            {
                cfg.MusicShuffle = !cfg.MusicShuffle;
                shuffleBtn.Kind = cfg.MusicShuffle ? BtnKind.Primary : BtnKind.Ghost;
                shuffleBtn.Invalidate();
                if (onSave != null) onSave();
            };

            prevBtn = new FlatButton("", G.Prev, BtnKind.Secondary);
            prevBtn.Click += delegate { PlayPrev(); };

            playPauseBtn = new FlatButton("", G.Play, BtnKind.Primary);
            playPauseBtn.Click += delegate { TogglePlayPause(); };

            nextBtn = new FlatButton("", G.Next, BtnKind.Secondary);
            nextBtn.Click += delegate { PlayNext(false); };

            loopBtn = new FlatButton("", G.Repeat, cfg.MusicLoop ? BtnKind.Primary : BtnKind.Ghost);
            loopBtn.Click += delegate
            {
                cfg.MusicLoop = !cfg.MusicLoop;
                loopBtn.Kind = cfg.MusicLoop ? BtnKind.Primary : BtnKind.Ghost;
                loopBtn.Invalidate();
                if (onSave != null) onSave();
            };

            seekBar = new Slider();
            seekBar.Value = 0;
            seekBar.MouseDown += delegate { seeking = true; };
            seekBar.MouseUp += delegate
            {
                if (seeking && player.IsOpened)
                {
                    int len = player.LengthMs;
                    if (len > 0) player.SeekMs(len * seekBar.Value / 100);
                }
                seeking = false;
            };

            volBar = new Slider();
            volBar.Value = cfg.MusicVolume;
            volBar.ValueChanged += delegate
            {
                cfg.MusicVolume = volBar.Value;
                player.SetVolume(cfg.MusicVolume);
                bottomBar.Invalidate();
                if (onSave != null) onSave();
            };

            bottomBar.Controls.AddRange(new Control[] { shuffleBtn, prevBtn, playPauseBtn, nextBtn, loopBtn, seekBar, volBar });

            var tt = new ToolTip();
            tt.SetToolTip(shuffleBtn, "Acak lagu (Shuffle)");
            tt.SetToolTip(prevBtn, "Lagu sebelumnya");
            tt.SetToolTip(playPauseBtn, "Putar / Jeda");
            tt.SetToolTip(nextBtn, "Lagu berikutnya");
            tt.SetToolTip(loopBtn, "Ulangi playlist otomatis (Auto-Continue / Loop)");
            tt.SetToolTip(volBar, "Volume musik");

            // Track list container
            listContainer = new Panel();
            listContainer.Dock = DockStyle.Fill;
            listContainer.AutoScroll = true;
            listContainer.BackColor = Theme.Bg;
            listContainer.Padding = new Padding(S(24), S(8), S(24), S(16));
            listContainer.Resize += delegate { LayoutTrackRows(); };

            Controls.Add(listContainer);
            Controls.Add(bottomBar);
            Controls.Add(topBar);

            player.StateChanged += delegate { SyncPlayerUI(); };
            player.TrackFinished += delegate { OnTrackEnded(); };

            uiTimer = new Timer();
            uiTimer.Interval = 400;
            uiTimer.Tick += delegate
            {
                if (!Visible) return;
                if (player.IsOpened && !seeking)
                {
                    int len = player.LengthMs;
                    int pos = player.PositionMs;
                    if (len > 0) seekBar.Value = Math.Max(0, Math.Min(100, (int)((long)pos * 100 / len)));
                    bottomBar.Invalidate();
                }
            };
            uiTimer.Start();

            RebuildPlaylist();
        }

        void LayoutTopBar()
        {
            int pad = S(24), h = S(38);
            backBtn.FitWidth();
            backBtn.SetBounds(pad, S(18), backBtn.Width, h);

            addSongBtn.FitWidth();
            addSongBtn.SetBounds(topBar.Width - pad - addSongBtn.Width, S(78), addSongBtn.Width, h);
            topBar.Invalidate();
        }

        void PaintTopBar(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            Theme.HQ(g);
            int pad = S(24);

            // Album badge gradien ala Spotify playlist header
            var badge = new Rectangle(pad, S(74), S(46), S(46));
            using (var path = Theme.Round(badge, S(11)))
            using (var b = new LinearGradientBrush(badge, Theme.Accent, Theme.Pink, 45f))
                g.FillPath(b, path);
            Theme.Glyph(g, G.Music, Theme.Icons(14f), Color.White, badge);

            int tx = badge.Right + S(16);
            Theme.DrawText(g, "Playlist Musik  ·  AlapSB", Theme.UI(15f, FontStyle.Bold), Theme.Text,
                new Rectangle(tx, S(70), topBar.Width - tx - S(180), S(28)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            Theme.DrawText(g, cfg.Playlist.Count + " lagu siap diputar  ·  Musik tetap jalan walau kembali ke Soundboard",
                Theme.UI(8.8f), Theme.Muted,
                new Rectangle(tx, S(102), topBar.Width - tx - S(180), S(20)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        void LayoutBottomBar()
        {
            int w = bottomBar.Width;
            int cy = S(14), bh = S(34), bw = S(36);
            int center = w / 2;

            playPauseBtn.SetBounds(center - S(20), cy - S(2), S(40), S(38));
            prevBtn.SetBounds(playPauseBtn.Left - S(8) - bw, cy, bw, bh);
            shuffleBtn.SetBounds(prevBtn.Left - S(8) - bw, cy, bw, bh);
            nextBtn.SetBounds(playPauseBtn.Right + S(8), cy, bw, bh);
            loopBtn.SetBounds(nextBtn.Right + S(8), cy, bw, bh);

            int sw = Math.Max(S(200), Math.Min(S(420), w - S(460)));
            seekBar.SetBounds(center - sw / 2, S(56), sw, S(24));

            volBar.SetBounds(w - S(24) - S(116), S(34), S(116), S(24));
            bottomBar.Invalidate();
        }

        static string FormatMs(int ms)
        {
            if (ms <= 0) return "0:00";
            int totalSec = ms / 1000;
            int m = totalSec / 60;
            int s = totalSec % 60;
            return m + ":" + (s < 10 ? "0" + s : s.ToString());
        }

        MusicTrack CurrentTrack()
        {
            if (string.IsNullOrEmpty(player.CurrentId)) return null;
            foreach (var t in cfg.Playlist) if (t.Id == player.CurrentId) return t;
            return null;
        }

        int CurrentIndex()
        {
            if (string.IsNullOrEmpty(player.CurrentId)) return -1;
            for (int i = 0; i < cfg.Playlist.Count; i++)
                if (cfg.Playlist[i].Id == player.CurrentId) return i;
            return -1;
        }

        void PaintBottomBar(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Panel);
            Theme.HQ(g);
            using (var p = new Pen(Theme.Border)) g.DrawLine(p, 0, 0, bottomBar.Width, 0);

            int pad = S(24);
            var cur = CurrentTrack();
            string title = cur != null ? cur.Title : "Belum ada lagu diputar";
            string artist = cur != null ? cur.Artist : "Klik lagu di atas untuk mulai memutar";

            int maxLeftW = Math.Max(S(120), seekBar.Left - pad - S(56));
            Theme.DrawText(g, title, Theme.Semi(10f), cur != null ? Theme.Text : Theme.Muted,
                new Rectangle(pad, S(22), maxLeftW, S(22)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            Theme.DrawText(g, artist, Theme.UI(8.5f), Theme.Muted,
                new Rectangle(pad, S(44), maxLeftW, S(18)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            // Waktu kiri & kanan seekBar
            int pos = player.IsOpened ? player.PositionMs : 0;
            int len = player.IsOpened ? player.LengthMs : 0;
            Theme.DrawText(g, FormatMs(pos), Theme.UI(8f), Theme.Muted,
                new Rectangle(seekBar.Left - S(46), seekBar.Top, S(42), seekBar.Height),
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            Theme.DrawText(g, FormatMs(len), Theme.UI(8f), Theme.Muted,
                new Rectangle(seekBar.Right + S(4), seekBar.Top, S(42), seekBar.Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            // Ikon volume & persentase
            int vx = volBar.Left - S(58);
            Theme.Glyph(g, cfg.MusicVolume == 0 ? "\uE74F" : G.Volume, Theme.Icons(10f), Theme.Muted,
                new Rectangle(vx, volBar.Top, S(20), volBar.Height));
            Theme.DrawText(g, cfg.MusicVolume + "%", Theme.Semi(8.5f), Theme.Text,
                new Rectangle(vx + S(20), volBar.Top, S(36), volBar.Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        public void RebuildPlaylist()
        {
            listContainer.SuspendLayout();
            var old = new List<Control>();
            foreach (Control c in listContainer.Controls) old.Add(c);
            listContainer.Controls.Clear();
            foreach (var c in old) c.Dispose();

            for (int i = 0; i < cfg.Playlist.Count; i++)
            {
                var tr = cfg.Playlist[i];
                var row = new TrackRow(i, tr,
                    () => player.CurrentId == tr.Id && player.IsOpened,
                    () => player.CurrentId == tr.Id && player.IsPlaying);
                row.PlayClicked += delegate { OnRowClicked(tr); };
                row.DeleteClicked += delegate { DeleteTrack(tr); };
                listContainer.Controls.Add(row);
            }
            LayoutTrackRows();
            listContainer.ResumeLayout();
            topBar.Invalidate();
            SyncPlayerUI();
        }

        void LayoutTrackRows()
        {
            int pad = S(24);
            int y = S(8) + listContainer.AutoScrollPosition.Y;
            int w = Math.Max(S(300), listContainer.ClientSize.Width - pad * 2);
            foreach (Control c in listContainer.Controls)
            {
                c.SetBounds(pad, y, w, S(54));
                y += S(60);
            }
        }

        void OnRowClicked(MusicTrack tr)
        {
            if (player.CurrentId == tr.Id && player.IsOpened)
            {
                player.TogglePause();
            }
            else
            {
                PlayTrack(tr);
            }
        }

        public void PlayTrack(MusicTrack tr)
        {
            if (tr == null) return;
            player.PlayTrack(tr.Id, Storage.Resolve(tr.FilePath), cfg.MusicVolume);
        }

        public void TogglePlayPause()
        {
            if (player.IsOpened)
            {
                player.TogglePause();
                return;
            }
            if (cfg.Playlist.Count > 0)
                PlayTrack(cfg.Playlist[0]);
        }

        public void PlayPrev()
        {
            if (cfg.Playlist.Count == 0) return;
            int idx = CurrentIndex();
            int prev = idx <= 0 ? cfg.Playlist.Count - 1 : idx - 1;
            PlayTrack(cfg.Playlist[prev]);
        }

        public void PlayNext(bool fromAutoEnd)
        {
            if (cfg.Playlist.Count == 0) return;
            if (fromAutoEnd && !cfg.MusicLoop)
            {
                // Kalau auto-continue dimatikan, berhenti saat lagu habis
                SyncPlayerUI();
                return;
            }
            int idx = CurrentIndex();
            int next;
            if (cfg.MusicShuffle && cfg.Playlist.Count > 1)
            {
                next = rnd.Next(cfg.Playlist.Count);
                if (next == idx) next = (idx + 1) % cfg.Playlist.Count;
            }
            else
            {
                next = (idx + 1) % cfg.Playlist.Count;
            }
            PlayTrack(cfg.Playlist[next]);
        }

        void OnTrackEnded()
        {
            PlayNext(true);
        }

        void DeleteTrack(MusicTrack tr)
        {
            if (player.CurrentId == tr.Id) player.Stop();
            cfg.Playlist.Remove(tr);
            if (onSave != null) onSave();
            RebuildPlaylist();
        }

        static List<string> GetBuiltInTwiceFileNames()
        {
            var list = new List<string>();
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                const string prefix = "AlapSB.Music.";
                foreach (var resName in asm.GetManifestResourceNames())
                {
                    if (resName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        list.Add(resName.Substring(prefix.Length));
                }
            }
            catch { }
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        static void EnsureBuiltInFileExtracted(string fileName)
        {
            try
            {
                Directory.CreateDirectory(Storage.MusicDir);
                string targetPath = Path.Combine(Storage.MusicDir, fileName);
                if (File.Exists(targetPath)) return;
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using (var src = asm.GetManifestResourceStream("AlapSB.Music." + fileName))
                using (var dst = File.Create(targetPath))
                {
                    if (src != null) src.CopyTo(dst);
                }
            }
            catch { }
        }

        void AddBuiltInTrack(string fileName)
        {
            EnsureBuiltInFileExtracted(fileName);
            string rel = "music\\" + fileName;
            foreach (var t in cfg.Playlist)
            {
                if (string.Equals(t.FilePath, rel, StringComparison.OrdinalIgnoreCase))
                    return; // sudah ada di playlist
            }
            var tr = new MusicTrack();
            string raw = Path.GetFileNameWithoutExtension(fileName);
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
        }

        void ShowAddSongMenu()
        {
            if (addMenu != null) addMenu.Dispose();
            addMenu = Theme.NewMenu();

            // Opsi 1: Pilih file lagu dari komputer
            var browseItem = addMenu.Items.Add("Pilih file lagu dari komputer...");
            browseItem.Tag = "accent";
            browseItem.Click += delegate { BrowseSongs(); };

            // Opsi 2: Lagu Default (TWICE)
            var builtIns = GetBuiltInTwiceFileNames();
            var twiceMenu = new ToolStripMenuItem("Pilih dari Lagu Default (TWICE)");
            twiceMenu.DropDown.Renderer = new DarkRenderer();

            var addAll = twiceMenu.DropDownItems.Add("+ Masukkan Semua Lagu Default (TWICE)");
            addAll.Tag = "accent";
            addAll.Click += delegate
            {
                foreach (var fn in builtIns) AddBuiltInTrack(fn);
                if (onSave != null) onSave();
                RebuildPlaylist();
            };
            twiceMenu.DropDownItems.Add(new ToolStripSeparator());

            foreach (var fn in builtIns)
            {
                string fileName = fn;
                string rel = "music\\" + fileName;
                bool alreadyIn = false;
                foreach (var t in cfg.Playlist)
                {
                    if (string.Equals(t.FilePath, rel, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyIn = true;
                        break;
                    }
                }
                string label = Path.GetFileNameWithoutExtension(fileName);
                var sub = new ToolStripMenuItem(label);
                sub.Checked = alreadyIn;
                sub.Click += delegate
                {
                    AddBuiltInTrack(fileName);
                    if (onSave != null) onSave();
                    RebuildPlaylist();
                };
                twiceMenu.DropDownItems.Add(sub);
            }
            addMenu.Items.Add(twiceMenu);

            if (cfg.Playlist.Count > 0)
            {
                addMenu.Items.Add(new ToolStripSeparator());
                var clearItem = addMenu.Items.Add("Kosongkan Playlist");
                clearItem.Tag = "danger";
                clearItem.Click += delegate
                {
                    player.Stop();
                    cfg.Playlist.Clear();
                    if (onSave != null) onSave();
                    RebuildPlaylist();
                };
            }

            addMenu.Show(addSongBtn, new Point(addSongBtn.Width, addSongBtn.Height + S(4)), ToolStripDropDownDirection.BelowLeft);
        }

        void BrowseSongs()
        {
            using (var d = new OpenFileDialog())
            {
                d.Title = "Pilih file lagu";
                d.Multiselect = true;
                d.Filter = "File audio|*.mp3;*.wav;*.wma;*.m4a;*.aac|Semua file|*.*";
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                foreach (var f in d.FileNames)
                {
                    var t = new MusicTrack();
                    string raw = Path.GetFileNameWithoutExtension(f);
                    int dash = raw.IndexOf(" - ");
                    if (dash > 0)
                    {
                        t.Title = raw.Substring(0, dash).Trim();
                        t.Artist = raw.Substring(dash + 3).Trim();
                    }
                    else
                    {
                        t.Title = raw;
                        t.Artist = "Custom Track";
                    }
                    t.FilePath = Storage.ImportMusic(f);
                    cfg.Playlist.Add(t);
                }
                if (onSave != null) onSave();
                RebuildPlaylist();
            }
        }

        public void SyncPlayerUI()
        {
            playPauseBtn.Glyph = player.IsPlaying ? G.Pause : G.Play;
            if (!player.IsOpened) seekBar.Value = 0;
            foreach (Control c in listContainer.Controls) c.Invalidate();
            bottomBar.Invalidate();
        }
    }
}
