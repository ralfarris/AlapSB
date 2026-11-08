using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace AlapSB
{
    /// <summary>
    /// Pemutar audio super ringan pakai Windows MCI (winmm) — tanpa library tambahan.
    /// Mendukung mp3, wav, wma, m4a/aac (sesuai codec Windows), bisa diputar bersamaan.
    /// </summary>
    class AudioEngine
    {
        class Inst { public string Alias; public string Key; public int DevId; }

        public const int MM_MCINOTIFY = 0x3B9;

        readonly IntPtr hwnd;
        readonly List<Inst> list = new List<Inst>();
        int counter;

        public event EventHandler Changed;
        public string LastError = "";

        public AudioEngine(IntPtr notifyWindow) { hwnd = notifyWindow; }

        static int Send(string cmd, IntPtr cb) { return Native.mciSendString(cmd, null, 0, cb); }

        static int Open(string file, string alias)
        {
            return Send("open \"" + file + "\" type mpegvideo alias " + alias, IntPtr.Zero);
        }

        static string Err(int code)
        {
            var sb = new StringBuilder(256);
            Native.mciGetErrorString(code, sb, sb.Capacity);
            return sb.ToString();
        }

        /// <summary>Putar file. Kalau key yang sama sedang main, diulang dari awal.</summary>
        public bool Play(string key, string file, int volume)
        {
            StopInternal(key);
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                LastError = "File audio tidak ditemukan.";
                Raise();
                return false;
            }

            string alias = "alap" + (++counter);
            int r = Open(file, alias);
            if (r != 0)
            {
                var sb = new StringBuilder(1024);
                if (Native.GetShortPathName(file, sb, sb.Capacity) > 0) r = Open(sb.ToString(), alias);
            }
            if (r != 0)
            {
                LastError = "Gagal membuka audio: " + Err(r);
                Raise();
                return false;
            }

            Send("setaudio " + alias + " volume to " + Math.Max(0, Math.Min(1000, volume * 10)), IntPtr.Zero);
            r = Send("play " + alias + " from 0 notify", hwnd);
            if (r != 0)
            {
                Send("close " + alias, IntPtr.Zero);
                LastError = "Gagal memutar: " + Err(r);
                Raise();
                return false;
            }

            var inst = new Inst();
            inst.Alias = alias;
            inst.Key = key;
            inst.DevId = Native.mciGetDeviceID(alias);
            list.Add(inst);
            Raise();
            return true;
        }

        bool StopInternal(string key)
        {
            bool any = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Key != key) continue;
                Send("close " + list[i].Alias, IntPtr.Zero);
                list.RemoveAt(i);
                any = true;
            }
            return any;
        }

        public void Stop(string key)
        {
            if (StopInternal(key)) Raise();
        }

        public void StopAll()
        {
            foreach (var i in list) Send("close " + i.Alias, IntPtr.Zero);
            list.Clear();
            Raise();
        }

        public bool IsPlaying(string key)
        {
            foreach (var i in list) if (i.Key == key) return true;
            return false;
        }

        public int PlayingCount { get { return list.Count; } }

        /// <summary>Dipanggil dari WndProc form utama saat audio selesai.</summary>
        public bool HandleMessage(ref Message m)
        {
            if (m.Msg != MM_MCINOTIFY) return false;
            int flag = m.WParam.ToInt32();
            int dev = (int)m.LParam.ToInt64();
            if (flag == 1 || flag == 8) // SUCCESSFUL / FAILURE
            {
                bool any = false;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].DevId != dev) continue;
                    Send("close " + list[i].Alias, IntPtr.Zero);
                    list.RemoveAt(i);
                    any = true;
                }
                if (any) Raise();
            }
            return true;
        }

        void Raise()
        {
            var h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }
    }
}
