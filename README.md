# AlapSB

Soundboard desktop super ringan (~62 KB, tanpa Electron/Chromium) untuk streaming & gaming di Windows.

[![Download Latest Release](https://img.shields.io/github/v/release/ralfarris/AlapSB?label=Download%20AlapSB&style=for-the-badge&color=7c5cff)](https://github.com/ralfarris/AlapSB/releases/latest)

## Cara Menggunakan (Untuk Pengguna Langsung)
1. Buka halaman [**GitHub Releases**](https://github.com/ralfarris/AlapSB/releases/latest).
2. Download file **`AlapSB.exe`** (atau `AlapSB-Windows.zip`).
3. Langsung jalankan aplikasinya (tanpa perlu install apa pun!).

---

## Menjalankan dari Source Code (Developer)
```bash
python run.py
```

**Atau langsung jalankan executable:**
- Buka `dist\AlapSB.exe`
- Untuk rebuild dari source code: jalankan `build.bat` atau `python run.py --build`

## Fitur
- Input file audio (`mp3`, `wav`, `wma`, `m4a`, `aac`) via tombol **Tambah Suara** atau **Drag & Drop**
- Global shortcut keybind (tetap jalan saat bermain game / OBS)
- Multi-preset soundboard & randomizer jika ada keybind yang sama
- Kontrol volume per-sound & Master Volume
- Berjalan ringan di System Tray
