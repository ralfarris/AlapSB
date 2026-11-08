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
