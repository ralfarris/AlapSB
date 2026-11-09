using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace AlapSB
{
    /// <summary>Win32 interop (hook keyboard global, audio MCI, dark title bar).</summary>
    static class Native
    {
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc fn, IntPtr hMod, uint threadId);

        [DllImport("user32.dll")]
        public static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string name);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vk);

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        public static extern int mciSendString(string command, StringBuilder ret, int retLen, IntPtr callback);

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        public static extern int mciGetDeviceID(string device);

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        public static extern bool mciGetErrorString(int error, StringBuilder buffer, int len);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetShortPathName(string longPath, StringBuilder shortPath, int len);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int RegisterWindowMessage(string name);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool ChangeWindowMessageFilter(int msg, uint flag);

        [DllImport("psapi.dll")]
        public static extern bool EmptyWorkingSet(IntPtr process);

        [DllImport("shell32.dll", SetLastError = true)]
        public static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        public const int WM_INPUT = 0x00FF;
        public const int RIDEV_INPUTSINK = 0x00000100;
        public const int RID_INPUT = 0x10000003;

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll")]
        public static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        public static void RegisterRawKeyboard(IntPtr hwnd)
        {
            try
            {
                var dev = new RAWINPUTDEVICE[1];
                dev[0].usUsagePage = 0x01; // Generic Desktop
                dev[0].usUsage = 0x06;     // Keyboard
                dev[0].dwFlags = RIDEV_INPUTSINK;
                dev[0].hwndTarget = hwnd;
                RegisterRawInputDevices(dev, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
            }
            catch { }
        }

        public static bool TryParseRawKey(IntPtr lParam, out int vk, out bool isDown)
        {
            vk = 0; isDown = false;
            try
            {
                uint headerSize = (uint)(8 + IntPtr.Size * 2);
                uint size = 0;
                if (GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, headerSize) != 0 || size == 0 || size > 256)
                    return false;
                IntPtr ptr = Marshal.AllocHGlobal((int)size);
                try
                {
                    if (GetRawInputData(lParam, RID_INPUT, ptr, ref size, headerSize) != size) return false;
                    int dwType = Marshal.ReadInt32(ptr, 0);
                    if (dwType != 1) return false; // 1 = RIM_TYPEKEYBOARD
                    int off = (int)headerSize;
                    ushort flags = (ushort)Marshal.ReadInt16(ptr, off + 2);
                    ushort vkey = (ushort)Marshal.ReadInt16(ptr, off + 6);
                    uint msg = (uint)Marshal.ReadInt32(ptr, off + 8);
                    if (vkey == 0 || vkey == 0xFF) return false;
                    vk = vkey;
                    isDown = (flags & 1) == 0 && (msg == 0x0100 || msg == 0x0104);
                    return true;
                }
                finally { Marshal.FreeHGlobal(ptr); }
            }
            catch { return false; }
        }

        public static void DarkTitle(Form f)
        {
            try
            {
                int on = 1;
                if (DwmSetWindowAttribute(f.Handle, 20, ref on, 4) != 0)
                    DwmSetWindowAttribute(f.Handle, 19, ref on, 4);
            }
            catch { }
        }
    }
}
