using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public static class DesktopRegression
{
    public delegate bool Visit(IntPtr handle, IntPtr context);
    [DllImport("user32.dll")] static extern bool EnumWindows(Visit visit, IntPtr context);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr handle, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr handle, StringBuilder text, int limit);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr handle, uint command);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr handle);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr handle);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr handle, int command);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr handle, uint message, IntPtr word, IntPtr parameter);
    static bool Until(Func<bool> success) { for (int i = 0; i < 70; i++) { if (success()) return true; Thread.Sleep(100); } return success(); }
    static void Ensure(bool success, string message) { if (!success) throw new Exception(message); }
    static IntPtr FindWindow(List<uint> pids)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr unused) {
            uint pid; GetWindowThreadProcessId(h, out pid); if (!pids.Contains(pid) || GetWindow(h, 4) != IntPtr.Zero) return true;
            var text = new StringBuilder(256); GetClassName(h, text, text.Capacity);
            if (text.ToString() != "Chrome_WidgetWin_1") return true;
            found = h; return false;
        }, IntPtr.Zero);
        return found;
    }
    public static int Main(string[] args)
    {
        IntPtr window = IntPtr.Zero; bool visible = false, maximized = false, minimized = false;
        Action focus = null;
        try
        {
            var pids = new List<uint>();
            foreach (Process process in Process.GetProcessesByName("DeepSeek Harness")) using (process) pids.Add((uint)process.Id);
            var bridge = Assembly.LoadFrom(args[0]).GetType("MarsNewPet.DshBridge", true);
            var method = bridge.GetMethod("OpenDesktop", BindingFlags.Static | BindingFlags.NonPublic);
            focus = () => method.Invoke(null, null);
            Environment.SetEnvironmentVariable("ELECTRON_RUN_AS_NODE", "1");
            window = FindWindow(pids);
            if (window != IntPtr.Zero) { visible = IsWindowVisible(window); maximized = IsZoomed(window); minimized = IsIconic(window); }
            else {
                focus(); Ensure(Until(() => { window = FindWindow(pids); return window != IntPtr.Zero; }), "DeepSeek desktop main window not created.");
                Console.WriteLine("PASS: desktop with no existing window reopened.");
            }
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                focus(); Ensure(Until(() => IsWindowVisible(window) && !IsIconic(window)), "Desktop did not open from plugin host environment.");
                Thread.Sleep(1800);
                Ensure(PostMessage(window, 0x0112, new IntPtr(0xF060), IntPtr.Zero), "Cannot send title-bar close.");
                Ensure(Until(() => !IsWindowVisible(window)), "Desktop cannot close to background after opening.");
                Console.WriteLine("PASS: host environment -> open -> close, cycle " + cycle);
            }
            focus(); Ensure(Until(() => IsWindowVisible(window)), "Cannot reopen desktop."); Thread.Sleep(1800);
            ShowWindow(window, 6); Ensure(Until(() => IsIconic(window)), "Cannot minimize desktop.");
            focus(); Ensure(Until(() => !IsIconic(window) && IsWindowVisible(window)), "Cannot restore minimized desktop.");
            Thread.Sleep(1800); Console.WriteLine("PASS: minimized desktop restored.");
            ShowWindow(window, 3); Ensure(Until(() => IsZoomed(window)), "Cannot maximize desktop.");
            PostMessage(window, 0x0112, new IntPtr(0xF060), IntPtr.Zero); Ensure(Until(() => !IsWindowVisible(window)), "Cannot hide maximized desktop.");
            focus(); Ensure(Until(() => IsZoomed(window) && IsWindowVisible(window)), "Maximized state not preserved.");
            Console.WriteLine("PASS: maximized desktop restored.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            if (window != IntPtr.Zero && focus != null)
            {
                focus(); Until(() => IsWindowVisible(window)); Thread.Sleep(1800);
                ShowWindow(window, maximized ? 3 : minimized ? 6 : 9);
                if (!visible) { PostMessage(window, 0x0112, new IntPtr(0xF060), IntPtr.Zero); Until(() => !IsWindowVisible(window)); }
            }
        }
    }
}
