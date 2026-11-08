using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace AlapSB
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool restart = Array.IndexOf(args, "--restart") >= 0;
            bool created;
            using (var mutex = new Mutex(true, "AlapSB_by_Alpha_SingleInstance", out created))
            {
                if (!created && restart)
                {
                    try { created = mutex.WaitOne(10000); }
                    catch (AbandonedMutexException) { created = true; }
                }
                if (!created)
                {
                    // sudah jalan -> munculkan jendela yang sudah ada
                    Native.PostMessage((IntPtr)0xFFFF, Native.RegisterWindowMessage("AlapSB_Show_by_Alpha"), IntPtr.Zero, IntPtr.Zero);
                    return;
                }

                try { Native.SetProcessDPIAware(); } catch { }
                try { Native.SetCurrentProcessExplicitAppUserModelID("Alpha.AlapSB.App.v1"); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var g = Graphics.FromHwnd(IntPtr.Zero)) Theme.Scale = g.DpiX / 96f;

                Application.Run(new MainForm());
                try { mutex.ReleaseMutex(); } catch { }
            }
        }
    }
}
