using System;
using System.Threading;
using System.Windows;

namespace MarsNewPet
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (Array.IndexOf(args, "--focus-desktop") >= 0)
            {
                try { DshBridge.OpenDesktop(); }
                catch (Exception error) { Console.Error.WriteLine(error.Message); Environment.ExitCode = 1; }
                return;
            }
            bool owner;
            using (var instance = new Mutex(true, "MarsNewPet.Desktop.v2", out owner))
            {
                if (!owner) return;
                var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                app.Run(new PetWindow());
            }
        }
    }
}
