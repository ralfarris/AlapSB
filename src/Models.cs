using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace AlapSB
{
    public class SoundItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string FilePath { get; set; }
        public int Key { get; set; }
        public int Mods { get; set; }
        public int Volume { get; set; }
        public bool Enabled { get; set; }

        public SoundItem()
        {
            Id = NewId();
            Name = "Suara baru";
            FilePath = "";
            Volume = 100;
            Enabled = true;
        }

        public static string NewId() { return Guid.NewGuid().ToString("N"); }

        public SoundItem Clone() { return (SoundItem)MemberwiseClone(); }
    }

    public class Preset
    {
        public string Name { get; set; }
        public List<SoundItem> Sounds { get; set; }

        public Preset() { Name = "Default"; Sounds = new List<SoundItem>(); }
    }

    public class MusicTrack
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string FilePath { get; set; }

        public MusicTrack()
        {
            Id = SoundItem.NewId();
            Title = "Lagu";
            Artist = "TWICE";
            FilePath = "";
        }
    }

    public class AppConfig
    {
        public List<Preset> Presets { get; set; }
        public int Current { get; set; }
        public int MasterVolume { get; set; }
        public int StopKey { get; set; }
        public int StopMods { get; set; }
        public bool CloseToTray { get; set; }
        public bool TrayHintShown { get; set; }
        public List<MusicTrack> Playlist { get; set; }
        public int MusicVolume { get; set; }
        public bool MusicLoop { get; set; }
        public bool MusicShuffle { get; set; }
        public bool MusicSeeded { get; set; }

        public AppConfig()
        {
            Presets = new List<Preset>();
            Playlist = new List<MusicTrack>();
            MasterVolume = 80;
            MusicVolume = 80;
            MusicLoop = true;
            CloseToTray = false;
        }

        public void Normalize()
        {
            if (Presets == null) Presets = new List<Preset>();
            if (Presets.Count == 0) Presets.Add(new Preset());
            foreach (var p in Presets)
            {
                if (p.Sounds == null) p.Sounds = new List<SoundItem>();
                if (string.IsNullOrEmpty(p.Name)) p.Name = "Preset";
                foreach (var s in p.Sounds)
                {
                    if (string.IsNullOrEmpty(s.Id)) s.Id = SoundItem.NewId();
                    if (s.FilePath == null) s.FilePath = "";
                    if (s.Name == null) s.Name = "";
                    s.Volume = Math.Max(0, Math.Min(100, s.Volume));
                }
            }
            if (Playlist == null) Playlist = new List<MusicTrack>();
            foreach (var t in Playlist)
            {
                if (string.IsNullOrEmpty(t.Id)) t.Id = SoundItem.NewId();
                if (string.IsNullOrEmpty(t.Title)) t.Title = "Lagu";
                if (string.IsNullOrEmpty(t.Artist)) t.Artist = "Artist";
                if (t.FilePath == null) t.FilePath = "";
            }
            if (Current < 0 || Current >= Presets.Count) Current = 0;
            MasterVolume = Math.Max(0, Math.Min(100, MasterVolume));
            if (MusicVolume <= 0 || MusicVolume > 100) MusicVolume = 80;
        }
    }

    /// <summary>Simpan config (JSON) &amp; file suara di sebelah .exe (portable).</summary>
    static class Storage
    {
        public static readonly string DataDir;
        public static readonly string ConfigPath;
        public static readonly string SoundsDir;
        public static readonly string MusicDir;

        static Storage()
        {
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            DataDir = CanWrite(exeDir)
                ? exeDir
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlapSB");
            Directory.CreateDirectory(DataDir);
            ConfigPath = Path.Combine(DataDir, "alapsb.json");
            SoundsDir = Path.Combine(DataDir, "sounds");
            MusicDir = Path.Combine(DataDir, "music");
        }

        static bool CanWrite(string dir)
        {
            try
            {
                string t = Path.Combine(dir, ".alapsb_write_test");
                File.WriteAllText(t, "x");
                File.Delete(t);
                return true;
            }
            catch { return false; }
        }

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var c = new JavaScriptSerializer().Deserialize<AppConfig>(File.ReadAllText(ConfigPath, Encoding.UTF8));
                    if (c != null) { c.Normalize(); return c; }
                }
            }
            catch { }
            var n = new AppConfig();
            n.Normalize();
            return n;
        }

        public static void Save(AppConfig c)
        {
            try
            {
                string json = new JavaScriptSerializer().Serialize(c);
                string tmp = ConfigPath + ".tmp";
                File.WriteAllText(tmp, json, Encoding.UTF8);
                File.Copy(tmp, ConfigPath, true);
                File.Delete(tmp);
            }
            catch { }
        }

        public static string Resolve(string p)
        {
            if (string.IsNullOrEmpty(p)) return "";
            return Path.IsPathRooted(p) ? p : Path.Combine(DataDir, p);
        }

        /// <summary>Copy file audio ke folder "sounds" supaya tidak hilang kalau file aslinya dipindah.</summary>
        public static string Import(string p)
        {
            try
            {
                string full = Path.GetFullPath(Resolve(p));
                if (!File.Exists(full)) return p;
                string soundsFull = Path.GetFullPath(SoundsDir).TrimEnd('\\') + "\\";
                if (full.StartsWith(soundsFull, StringComparison.OrdinalIgnoreCase))
                    return "sounds\\" + full.Substring(soundsFull.Length);

                Directory.CreateDirectory(SoundsDir);
                string name = Path.GetFileNameWithoutExtension(full);
                string ext = Path.GetExtension(full);
                string target = Path.Combine(SoundsDir, name + ext);
                int i = 2;
                while (File.Exists(target))
                {
                    if (new FileInfo(target).Length == new FileInfo(full).Length)
                        return "sounds\\" + Path.GetFileName(target);
                    target = Path.Combine(SoundsDir, name + " (" + i++ + ")" + ext);
                }
                File.Copy(full, target);
                return "sounds\\" + Path.GetFileName(target);
            }
            catch { return p; }
        }

        public static string ImportMusic(string p)
        {
            try
            {
                string full = Path.GetFullPath(Resolve(p));
                if (!File.Exists(full)) return p;
                string musicFull = Path.GetFullPath(MusicDir).TrimEnd('\\') + "\\";
                if (full.StartsWith(musicFull, StringComparison.OrdinalIgnoreCase))
                    return "music\\" + full.Substring(musicFull.Length);

                Directory.CreateDirectory(MusicDir);
                string name = Path.GetFileNameWithoutExtension(full);
                string ext = Path.GetExtension(full);
                string target = Path.Combine(MusicDir, name + ext);
                int i = 2;
                while (File.Exists(target))
                {
                    if (new FileInfo(target).Length == new FileInfo(full).Length)
                        return "music\\" + Path.GetFileName(target);
                    target = Path.Combine(MusicDir, name + " (" + i++ + ")" + ext);
                }
                File.Copy(full, target);
                return "music\\" + Path.GetFileName(target);
            }
            catch { return p; }
        }
    }
}
