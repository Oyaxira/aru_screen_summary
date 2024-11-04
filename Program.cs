using System;
using System.Windows.Forms;
using System.Diagnostics;

namespace ScreenshotGPT
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Trace.Listeners.Add(new UTF8TraceListener("debug.log"));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
