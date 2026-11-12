using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace AlapSB
{
    /// <summary>
    /// Pemutar lagu dedicated (single-stream seperti Spotify) pakai Windows MCI.
    /// Mendukung play, pause, resume, seek, volume, dan auto-next saat lagu selesai.
    /// </summary>
    class MusicPlayer
    {
        const string Alias = "alap_music_stream";
        readonly IntPtr hwnd;
        int devId = -1;
        bool opened;
        bool paused;
        string currentId = "";

        public event EventHandler StateChanged;
        public event EventHandler TrackFinished;
        public string LastError = "";

        public MusicPlayer(IntPtr notifyWindow) { hwnd = notifyWindow; }

        static int Send(string cmd, IntPtr cb) { return Native.mciSendString(cmd, null, 0, cb); }

        static string Query(string cmd)
        {
            var sb = new StringBuilder(128);
            if (Native.mciSendString(cmd, sb, sb.Capacity, IntPtr.Zero) != 0) return "";
            return sb.ToString().Trim();
        }

        public string CurrentId { get { return currentId; } }
        public bool IsOpened { get { return opened; } }
        public bool IsPlaying { get { return opened && !paused; } }
        public bool IsPaused { get { return opened && paused; } }

        public bool PlayTrack(string id, string file, int volume)
        {
            CloseInternal();
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                LastError = "File lagu tidak ditemukan.";
                Raise();
                return false;
            }

            int r = Send("open \"" + file + "\" type mpegvideo alias " + Alias, IntPtr.Zero);
            if (r != 0)
            {
                var sb = new StringBuilder(1024);
                if (Native.GetShortPathName(file, sb, sb.Capacity) > 0)
                    r = Send("open \"" + sb.ToString() + "\" type mpegvideo alias " + Alias, IntPtr.Zero);
            }
            if (r != 0)
            {
                LastError = "Gagal membuka lagu.";
                Raise();
                return false;
            }

            Send("set " + Alias + " time format milliseconds", IntPtr.Zero);
            SetVolume(volume);
            r = Send("play " + Alias + " from 0 notify", hwnd);
            if (r != 0)
            {
                Send("close " + Alias, IntPtr.Zero);
                LastError = "Gagal memutar lagu.";
                Raise();
                return false;
            }

            opened = true;
            paused = false;
            currentId = id ?? "";
            devId = Native.mciGetDeviceID(Alias);
            Raise();
            return true;
        }

        public void TogglePause()
        {
            if (!opened) return;
            if (paused)
            {
                int pos = PositionMs;
                Send("play " + Alias + " from " + pos + " notify", hwnd);
                paused = false;
            }
            else
            {
                Send("pause " + Alias, IntPtr.Zero);
                paused = true;
            }
            Raise();
        }

        public void SeekMs(int ms)
        {
            if (!opened) return;
            int len = LengthMs;
            int target = Math.Max(0, Math.Min(len > 0 ? len - 100 : ms, ms));
            if (paused)
            {
                Send("seek " + Alias + " to " + target, IntPtr.Zero);
            }
            else
            {
                Send("play " + Alias + " from " + target + " notify", hwnd);
            }
            Raise();
        }

        public void SetVolume(int volume)
        {
            int v = AudioEngine.ToMciVolume(volume);
            Send("setaudio " + Alias + " volume to " + v, IntPtr.Zero);
        }

        public int PositionMs
        {
            get
            {
                if (!opened) return 0;
                int v;
                return int.TryParse(Query("status " + Alias + " position"), out v) ? v : 0;
            }
        }

        public int LengthMs
        {
            get
            {
                if (!opened) return 0;
                int v;
                return int.TryParse(Query("status " + Alias + " length"), out v) ? v : 0;
            }
        }

        void CloseInternal()
        {
            if (opened) Send("close " + Alias, IntPtr.Zero);
            opened = false;
            paused = false;
            devId = -1;
            currentId = "";
        }

        public void Stop()
        {
            if (!opened) return;
            CloseInternal();
            Raise();
        }

        public bool HandleMessage(ref Message m)
        {
            if (m.Msg != AudioEngine.MM_MCINOTIFY) return false;
            int dev = (int)m.LParam.ToInt64();
            if (!opened || dev != devId) return false;

            int flag = m.WParam.ToInt32();
            if (flag == 1) // MCI_NOTIFY_SUCCESSFUL (lagu selesai sampai akhir)
            {
                CloseInternal();
                Raise();
                var f = TrackFinished;
                if (f != null) f(this, EventArgs.Empty);
            }
            return true;
        }

        void Raise()
        {
            var h = StateChanged;
            if (h != null) h(this, EventArgs.Empty);
        }
    }
}
