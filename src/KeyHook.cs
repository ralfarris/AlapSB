using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AlapSB
{
    /// <summary>
    /// Shortcut global (tetap jalan walau sedang di game / OBS).
    /// Menggabungkan Low-Level Hook + Raw Input (RIDEV_INPUTSINK) agar tetap tembus di game anti-cheat / DirectInput.
    /// </summary>
    class KeyHook : IDisposable
    {
        const int WH_KEYBOARD_LL = 13;
        IntPtr hook = IntPtr.Zero;
        Native.LowLevelKeyboardProc proc;
        readonly int[] lastFireTick = new int[256];
        readonly bool[] watchedKeys = new bool[256];

        public bool Suspended;
        public event Action<int, int> KeyPressed;
        public bool Active { get { return hook != IntPtr.Zero; } }

        public void UpdateWatchedKeys(List<SoundItem> sounds, int stopKey)
        {
            Array.Clear(watchedKeys, 0, watchedKeys.Length);
            if (stopKey > 0 && stopKey < 256) watchedKeys[stopKey] = true;
            if (sounds == null) return;
            for (int i = 0; i < sounds.Count; i++)
            {
                var s = sounds[i];
                if (s.Enabled && s.Key > 0 && s.Key < 256)
                    watchedKeys[NormalizeVk(s.Key)] = true;
            }
        }

        public void Install(IntPtr hwnd)
        {
            if (hook == IntPtr.Zero)
            {
                proc = Callback;
                using (var p = Process.GetCurrentProcess())
                using (var m = p.MainModule)
                    hook = Native.SetWindowsHookEx(WH_KEYBOARD_LL, proc, Native.GetModuleHandle(m.ModuleName), 0);
            }
            if (hwnd != IntPtr.Zero)
                Native.RegisterRawKeyboard(hwnd);
        }

        static int NormalizeVk(int vk)
        {
            if (vk == 0xA0 || vk == 0xA1) return 0x10; // LShift/RShift -> Shift
            if (vk == 0xA2 || vk == 0xA3) return 0x11; // LCtrl/RCtrl -> Ctrl
            if (vk == 0xA4 || vk == 0xA5) return 0x12; // LAlt/RAlt -> Alt
            return vk;
        }

        void DispatchKey(int vk)
        {
            if (Suspended) return;
            vk = NormalizeVk(vk);
            if ((uint)vk >= 256 || !watchedKeys[vk]) return;

            int now = Environment.TickCount;
            int prev = lastFireTick[vk];
            if (prev != 0 && unchecked(now - prev) < 35)
                return; // Cegah double-fire antara WH_KEYBOARD_LL & WM_INPUT
            lastFireTick[vk] = now;

            var h = KeyPressed;
            if (h != null) h(vk, CurrentMods());
        }

        IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                int vk = Marshal.ReadInt32(lParam);
                if (msg == 0x100 || msg == 0x104)
                    DispatchKey(vk);
            }
            return Native.CallNextHookEx(hook, nCode, wParam, lParam);
        }

        public void HandleRawMessage(ref Message m)
        {
            if (m.Msg != Native.WM_INPUT) return;
            int vk;
            bool isDown;
            if (Native.TryParseRawKey(m.LParam, out vk, out isDown) && isDown)
                DispatchKey(vk);
        }

        public static int CurrentMods()
        {
            int m = 0;
            if ((Native.GetAsyncKeyState(0x11) & 0x8000) != 0) m |= Keyb.Ctrl;
            if ((Native.GetAsyncKeyState(0x10) & 0x8000) != 0) m |= Keyb.Shift;
            if ((Native.GetAsyncKeyState(0x12) & 0x8000) != 0) m |= Keyb.Alt;
            return m;
        }

        public void Dispose()
        {
            if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook);
            hook = IntPtr.Zero;
        }
    }
}
