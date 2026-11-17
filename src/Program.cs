using System;
using System.Threading;
using System.Windows.Forms;

namespace Quadra
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, "Local\\Quadra.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("Quadra is already running. Look for its icon in the system tray.",
                        "Quadra", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
                GC.KeepAlive(mutex);
            }
        }
    }
}
