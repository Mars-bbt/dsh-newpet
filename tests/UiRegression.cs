using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

class UiRegression
{
    static object Field(object instance, string name) { return instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance); }
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS: " + name); }
    static void WaitForPaint()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        timer.Tick += delegate { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    static void Capture(Window window, string file)
    {
        WaitForPaint(); var content = (FrameworkElement)window.Content;
        var image = new RenderTargetBitmap((int)Math.Ceiling(window.Width), (int)Math.Ceiling(window.Height), 96, 96, PixelFormats.Pbgra32);
        image.Render(content); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using (var output = File.Create(file)) encoder.Save(output);
    }
    [STAThread]
    static int Main(string[] args)
    {
        Window window = null;
        try
        {
            var assembly = Assembly.LoadFrom(args[0]);
            window = (Window)Activator.CreateInstance(assembly.GetType("MarsNewPet.PetWindow"));
            ((DispatcherTimer)Field(window, "heartbeat")).Stop();
            window.Show();
            var resize = window.GetType().GetMethod("ResizePet", BindingFlags.NonPublic | BindingFlags.Instance);
            resize.Invoke(window, new object[] { 0.8 });
            Check(((System.Collections.IDictionary)Field(window, "pictures")).Count == 8, "all eight PNG poses load");
            var update = window.GetType().GetMethod("UpdateTask", BindingFlags.NonPublic | BindingFlags.Instance);
            var state = new Dictionary<string, object> { { "busy", true }, { "failure", false }, { "task", "检查工作姿态和任务气泡" }, { "draft", "正在准备素材" }, { "tools", "" }, { "step", "1.2" }, { "session", "qa" } };
            update.Invoke(window, new object[] { state });
            Check((string)Field(window, "pose") == "work", "thinking phase uses laptop working pose");
            Check(((Border)Field(window, "speech")).Visibility == Visibility.Visible, "working bubble visible");
            Capture(window, Path.Combine(args[1], "working-ui.png"));
            state["tools"] = "本地检查"; update.Invoke(window, new object[] { state });
            Check(((TextBlock)Field(window, "paragraph")).Text.Contains("本地检查"), "tool update reaches bubble");
            state["failure"] = true; update.Invoke(window, new object[] { state });
            Check((string)Field(window, "pose") == "failure", "failure pose switches");
            state["failure"] = false; state["busy"] = false; update.Invoke(window, new object[] { state });
            Check((string)Field(window, "pose") == "success", "completion pose switches");
            Check(((StackPanel)Field(window, "completion")).Visibility == Visibility.Visible, "completion buttons visible");
            Capture(window, Path.Combine(args[1], "completed-ui.png"));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { if (window != null) window.Close(); }
    }
}
