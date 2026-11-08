using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AlapSB
{
    /// <summary>
    /// Shortcut global (tetap jalan walau sedang di game / OBS).
    /// Tombol TIDAK diblokir — tetap diteruskan ke aplikasi lain.
    /// </summary>
    class KeyHook : IDisposable
    {
        const int WH_KEYBOARD_LL = 13;
        IntPtr hook = IntPtr.Zero;
        Native.LowLevelKeyboardProc proc;
        readonly HashSet<int> down = new HashSet<int>();

        public bool Suspended;
        public event Action<int, int> KeyPressed;
        public bool Active { get { return hook != IntPtr.Zero; } }

        public void Install()
        {
            if (hook != IntPtr.Zero) return;
            proc = Callback;
            using (var p = Process.GetCurrentProcess())
            using (var m = p.MainModule)
                hook = Native.SetWindowsHookEx(WH_KEYBOARD_LL, proc, Native.GetModuleHandle(m.ModuleName), 0);
        }

        IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                int vk = Marshal.ReadInt32(lParam);
                if (msg == 0x100 || msg == 0x104)
                {
                    // hanya saat pertama ditekan (abaikan auto-repeat)
                    if (down.Add(vk) && !Suspended && !Keyb.IsModifier(vk))
                    {
                        var h = KeyPressed;
                        if (h != null) h(vk, CurrentMods());
                    }
                }
                else if (msg == 0x101 || msg == 0x105)
                {
                    down.Remove(vk);
                }
            }
            return Native.CallNextHookEx(hook, nCode, wParam, lParam);
        }

        static int CurrentMods()
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
