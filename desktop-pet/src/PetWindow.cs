using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MarsNewPet
{
    public sealed class PetWindow : Window
    {
        private readonly string dataFolder;
        private readonly Dictionary<string, BitmapImage> pictures = new Dictionary<string, BitmapImage>();
        private readonly Canvas surface = new Canvas();
        private readonly Grid avatar = new Grid();
        private readonly Image front = new Image(), back = new Image();
        private readonly Border speech = new Border(), shortcuts = new Border();
        private readonly TextBlock heading = new TextBlock(), paragraph = new TextBlock();
        private readonly StackPanel completion = new StackPanel();
        private readonly TranslateTransform movement = new TranslateTransform();
        private readonly TranslateTransform gaze = new TranslateTransform();
        private readonly RotateTransform rotation = new RotateTransform();
        private readonly ScaleTransform squash = new ScaleTransform(1, 1);
        private readonly DispatcherTimer heartbeat = new DispatcherTimer(), hoverDelay = new DispatcherTimer(), clicks = new DispatcherTimer();
        private readonly Random random = new Random();
        private DateTime messageUntil = DateTime.MinValue, workStarted, lastActivity = DateTime.UtcNow;
        private string nickname = "主人", petName = "鲸鱼娘", pose = "idle", task = "", currentSession = "";
        private bool querying, working, finished, folded, dragging, pressed, temporaryMessage;
        private double zoom = 0.4;
        private Point pointerOrigin, windowOrigin;
        private UIElement dragTarget;
        private int tapCount, tick;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int w, int h, uint flags);

        public PetWindow()
        {
            Title = "Mars 鲸鱼娘桌宠"; WindowStyle = WindowStyle.None; AllowsTransparency = true;
            Background = Brushes.Transparent; Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize;
            FontFamily = new FontFamily("Microsoft YaHei UI"); FontSize = 12;
            dataFolder = Environment.GetEnvironmentVariable("MARS_PET_PREFS") ??
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MarsNewPet");
            Directory.CreateDirectory(dataFolder);
            LoadImages(); BuildWindow(); LoadPosition(); ReadNames();
            Loaded += delegate { Constrain(); StartMotion(); };
            Closing += delegate { SavePosition(); };
            heartbeat.Interval = TimeSpan.FromMilliseconds(750);
            heartbeat.Tick += delegate { OnHeartbeat(); }; heartbeat.Start();
            hoverDelay.Interval = TimeSpan.FromMilliseconds(700);
            hoverDelay.Tick += delegate { hoverDelay.Stop(); if (!IsMouseOver) shortcuts.Visibility = Visibility.Hidden; };
            clicks.Interval = TimeSpan.FromMilliseconds(350);
            clicks.Tick += delegate {
                clicks.Stop(); int count = tapCount; tapCount = 0;
                if (count >= 3) { SetPose("celebrate"); Spin(); Say("转圈圈～", 2200); }
                else if (count == 2) { SetPose("celebrate"); Hop(); Sparkles(); Say(nickname + "，一起庆祝吧！", 2500); }
                else { SetPose("interact"); Pat(); Say(nickname + "，" + petName + "在这里哦～", 3000); OpenDesktop(); }
            };
        }
        private void LoadImages()
        {
            string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(PetWindow).Assembly.Location), "..", "assets", "generated"));
            foreach (string name in new[] { "idle", "work", "thinking", "success", "failure", "celebrate", "sleep", "interact" })
            {
                string file = System.IO.Path.Combine(directory, name + ".png");
                if (!File.Exists(file)) throw new FileNotFoundException("缺少桌宠立绘", file);
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(file); image.EndInit(); image.Freeze(); pictures[name] = image;
            }
        }
        private void BuildWindow()
        {
            Content = surface;
            var transforms = new TransformGroup(); transforms.Children.Add(squash); transforms.Children.Add(rotation); transforms.Children.Add(movement); transforms.Children.Add(gaze);
            avatar.RenderTransform = transforms; avatar.RenderTransformOrigin = new Point(0.5, 0.92);
            front.Source = pictures["idle"]; front.Stretch = back.Stretch = Stretch.Uniform;
            back.Opacity = 0; avatar.Children.Add(front); avatar.Children.Add(back); surface.Children.Add(avatar);
            avatar.MouseLeftButtonDown += BeginDrag; avatar.MouseMove += MoveDrag; avatar.MouseLeftButtonUp += EndDrag;
            avatar.MouseMove += delegate(object sender, MouseEventArgs args) {
                if (pressed) return; Point mouse = args.GetPosition(avatar);
                gaze.X = (mouse.X / Math.Max(1, avatar.Width) - 0.5) * 5;
                gaze.Y = (mouse.Y / Math.Max(1, avatar.Height) - 0.5) * 3;
            };
            avatar.MouseLeave += delegate { gaze.X = gaze.Y = 0; };
            MouseEnter += delegate { hoverDelay.Stop(); shortcuts.Visibility = Visibility.Visible; };
            MouseLeave += delegate { hoverDelay.Stop(); hoverDelay.Start(); };
            heading.FontWeight = FontWeights.SemiBold; heading.Foreground = Brush("#C5DEFF");
            paragraph.TextWrapping = TextWrapping.Wrap; paragraph.MaxWidth = 248; paragraph.Foreground = Brushes.White;
            completion.Orientation = Orientation.Horizontal; completion.HorizontalAlignment = HorizontalAlignment.Right;
            completion.Margin = new Thickness(0, 8, 0, 0); completion.Visibility = Visibility.Collapsed;
            completion.Children.Add(Button("知道了", delegate { finished = false; HideSpeech(); }));
            completion.Children.Add(Button("查看", delegate { finished = false; HideSpeech(); OpenDesktop(); }));
            var words = new StackPanel(); words.Children.Add(heading); words.Children.Add(paragraph); words.Children.Add(completion);
            speech.Child = words; speech.Background = Brush("#EA162D54"); speech.CornerRadius = new CornerRadius(13);
            speech.Padding = new Thickness(12, 9, 12, 9); speech.MaxWidth = 276; speech.Visibility = Visibility.Hidden;
            speech.MouseLeftButtonDown += BeginDrag; speech.MouseMove += MoveDrag; speech.MouseLeftButtonUp += EndDrag;
            surface.Children.Add(speech);
            var commands = new StackPanel { Orientation = Orientation.Horizontal };
            commands.Children.Add(Button("💻", OpenDesktop, "呼出 DeepSeek Harness"));
            commands.Children.Add(Button("💰", ShowBalance, "查看余额"));
            shortcuts.Child = commands; shortcuts.Background = Brush("#E0162D54"); shortcuts.CornerRadius = new CornerRadius(12);
            shortcuts.Padding = new Thickness(3); shortcuts.Visibility = Visibility.Hidden; surface.Children.Add(shortcuts);
            var menu = new ContextMenu();
            AddMenu(menu, "放大", delegate { ResizePet(zoom * 1.25); });
            AddMenu(menu, "缩小", delegate { ResizePet(zoom / 1.25); });
            AddMenu(menu, "重置大小", delegate { ResizePet(0.4); });
            AddMenu(menu, "回到默认位置", ResetPosition); menu.Items.Add(new Separator());
            var startup = new MenuItem { Header = "开机自启", IsCheckable = true };
            startup.Click += delegate { SetStartup(startup.IsChecked); };
            menu.Opened += delegate { startup.IsChecked = File.Exists(StartupFile()); };
            menu.Items.Add(startup); AddMenu(menu, "呼出 DeepSeek Harness", OpenDesktop);
            AddMenu(menu, "退出桌宠", Close); ContextMenu = menu;
            SizeChanged += delegate { Layout(); };
            ResizePet(zoom);
        }
        private static bool FindParentButton(DependencyObject item)
        {
            while (item != null) { if (item is Button) return true; item = VisualTreeHelper.GetParent(item); }
            return false;
        }
        private static SolidColorBrush Brush(string color) { return (SolidColorBrush)new BrushConverter().ConvertFromString(color); }
        private static Button Button(string label, Action action, string tooltip = null)
        {
            var button = new Button { Content = label, ToolTip = tooltip, Cursor = Cursors.Hand,
                Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(2), MinWidth = 28,
                Background = Brush("#294673"), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            button.Click += delegate(object sender, RoutedEventArgs args) { args.Handled = true; action(); };
            return button;
        }
        private static void AddMenu(ContextMenu menu, string label, Action action)
        {
            var item = new MenuItem { Header = label }; item.Click += delegate { action(); }; menu.Items.Add(item);
        }
        private void Layout()
        {
            double height = 220 * zoom / 0.4, width = height * 0.7;
            avatar.Width = width; avatar.Height = height;
            Canvas.SetLeft(avatar, (Width - width) / 2); Canvas.SetTop(avatar, Height - height - 38);
            speech.Measure(new Size(276, 160));
            Canvas.SetLeft(speech, (Width - speech.DesiredSize.Width) / 2);
            Canvas.SetTop(speech, Math.Max(0, Height - height - 44 - speech.DesiredSize.Height));
            shortcuts.Measure(new Size(100, 38));
            Canvas.SetLeft(shortcuts, (Width - shortcuts.DesiredSize.Width) / 2); Canvas.SetTop(shortcuts, Height - 36);
        }
        private void ResizePet(double value)
        {
            zoom = Math.Max(0.15, Math.Min(3, value));
            Width = Math.Max(280, 180 * zoom / 0.4); Height = 220 * zoom / 0.4 + 172;
            Layout(); if (IsLoaded) { Constrain(); SavePosition(); }
        }
        private void BeginDrag(object sender, MouseButtonEventArgs args)
        {
            if (FindParentButton(args.OriginalSource as DependencyObject)) return;
            pressed = true; dragging = false; pointerOrigin = PointToScreen(args.GetPosition(this));
            windowOrigin = new Point(Left, Top); dragTarget = (UIElement)sender; dragTarget.CaptureMouse(); args.Handled = true;
        }
        private void MoveDrag(object sender, MouseEventArgs args)
        {
            if (!pressed) return;
            Point current = PointToScreen(args.GetPosition(this));
            var delta = current - pointerOrigin;
            if (!dragging && delta.Length < 5) return;
            dragging = true; speech.Visibility = shortcuts.Visibility = Visibility.Hidden;
            var source = PresentationSource.FromVisual(this);
            var logical = source.CompositionTarget.TransformFromDevice.Transform(delta);
            Left = windowOrigin.X + logical.X; Top = windowOrigin.Y + logical.Y; Constrain();
        }
        private void EndDrag(object sender, MouseButtonEventArgs args)
        {
            if (!pressed) return;
            dragTarget.ReleaseMouseCapture(); pressed = false; args.Handled = true;
            if (dragging) { dragging = false; SavePosition(); return; }
            if (sender == speech)
            {
                if (working) { folded = !folded; paragraph.Visibility = folded ? Visibility.Collapsed : Visibility.Visible; Layout(); }
                else if (finished) { finished = false; HideSpeech(); OpenDesktop(); }
                return;
            }
            lastActivity = DateTime.UtcNow; tapCount++; clicks.Stop(); clicks.Start();
        }
        private void Constrain()
        {
            // Constrain the illustration, allowing transparent padding and speech to extend beyond an edge.
            var bounds = SystemParameters.WorkArea;
            double x = (Width - avatar.Width) / 2, y = Height - avatar.Height - 38;
            Left = Math.Max(bounds.Left - x, Math.Min(bounds.Right - x - avatar.Width, Left));
            Top = Math.Max(bounds.Top - y, Math.Min(bounds.Bottom - y - avatar.Height - 2, Top));
        }
        private void ResetPosition()
        {
            var bounds = SystemParameters.WorkArea; Left = bounds.Right - Width - 18; Top = bounds.Bottom - Height - 12;
            Constrain(); SavePosition();
        }
        private void LoadPosition()
        {
            try
            {
                var settings = DshBridge.Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(System.IO.Path.Combine(dataFolder, "position.json")));
                ResizePet(Convert.ToDouble(settings["scale"], CultureInfo.InvariantCulture));
                Left = Convert.ToDouble(settings["left"]); Top = Convert.ToDouble(settings["top"]);
            }
            catch (Exception) { ResetPosition(); }
        }
        private void SavePosition()
        {
            try { File.WriteAllText(System.IO.Path.Combine(dataFolder, "position.json"), DshBridge.Json.Serialize(new { left = Left, top = Top, scale = zoom })); }
            catch (Exception) { }
        }
        private void ReadNames()
        {
            try
            {
                var names = DshBridge.Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(System.IO.Path.Combine(dataFolder, "names.json")));
                nickname = DshBridge.Text(names, "title", "主人"); petName = DshBridge.Text(names, "selfName", "鲸鱼娘");
            }
            catch (Exception) { }
        }
        private void StartMotion()
        {
            movement.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -4, TimeSpan.FromSeconds(1.8)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            rotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-1.2, 1.2, TimeSpan.FromSeconds(3.6)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        }
        private void SetPose(string next)
        {
            if (pose == next || !pictures.ContainsKey(next)) return;
            pose = next; back.Source = front.Source; back.Opacity = 1; front.Source = pictures[next]; front.Opacity = 0;
            front.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240)));
            back.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(240)));
        }
        private void Pat()
        {
            squash.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 1.05, TimeSpan.FromMilliseconds(160)) { AutoReverse = true });
            squash.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 0.95, TimeSpan.FromMilliseconds(160)) { AutoReverse = true });
        }
        private void Hop()
        {
            var jump = new DoubleAnimation(0, -16, TimeSpan.FromMilliseconds(220)) { AutoReverse = true };
            jump.Completed += delegate { StartMotion(); }; movement.BeginAnimation(TranslateTransform.YProperty, jump);
        }
        private void Spin()
        {
            var spin = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(650));
            spin.Completed += delegate { StartMotion(); }; rotation.BeginAnimation(RotateTransform.AngleProperty, spin);
        }
        private void Sparkles()
        {
            for (int i = 0; i < 7; i++)
            {
                var dot = new Ellipse { Width = 5, Height = 5, Fill = Brush(i % 2 == 0 ? "#B0DDFF" : "#FFD99E"), IsHitTestVisible = false };
                surface.Children.Add(dot); double x = Width / 2 + random.Next(-60, 61), y = Height - 100;
                Canvas.SetLeft(dot, x); Canvas.SetTop(dot, y);
                dot.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(y, y - random.Next(35, 90), TimeSpan.FromMilliseconds(650)));
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(700)); fade.Completed += delegate { surface.Children.Remove(dot); };
                dot.BeginAnimation(OpacityProperty, fade);
            }
        }
        private void Say(string message, int milliseconds)
        {
            if ((working || finished) && !temporaryMessage) return;
            heading.Text = ""; paragraph.Text = message; paragraph.Visibility = Visibility.Visible;
            completion.Visibility = Visibility.Collapsed; speech.Visibility = Visibility.Visible;
            messageUntil = DateTime.UtcNow.AddMilliseconds(milliseconds); Layout();
        }
        private void HideSpeech() { speech.Visibility = Visibility.Hidden; completion.Visibility = Visibility.Collapsed; }
        private void OpenDesktop()
        {
            try { DshBridge.OpenDesktop(); }
            catch (Exception error) { Say(error.Message, 4000); }
        }
        private void ShowBalance()
        {
            ThreadPool.QueueUserWorkItem(delegate {
                string message;
                try {
                    var balance = DshBridge.Request("balance");
                    message = DshBridge.Flag(balance, "ok") ? "DeepSeek 余额：" + DshBridge.Text(balance, "total") + " " + DshBridge.Text(balance, "currency", "CNY") : DshBridge.Text(balance, "error", "暂时无法查询余额");
                } catch (Exception error) { message = error.Message; }
                Dispatcher.BeginInvoke(new Action(delegate { temporaryMessage = true; Say(message, 8000); }));
            });
        }
        private void OnHeartbeat()
        {
            tick++; if (tick % 8 == 0) ReadNames();
            if (tick % 3 == 0)
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            }
            if (!working && !finished && DateTime.UtcNow > messageUntil) {
                HideSpeech(); double idleMinutes = (DateTime.UtcNow - lastActivity).TotalMinutes;
                SetPose(idleMinutes > 5 ? "sleep" : idleMinutes > 2 ? "thinking" : "idle");
            }
            if (querying) return; querying = true;
            ThreadPool.QueueUserWorkItem(delegate {
                Dictionary<string, object> state = null;
                try { state = DshBridge.Request("state"); } catch (Exception) { }
                Dispatcher.BeginInvoke(new Action(delegate { querying = false; if (state != null) UpdateTask(state); }));
            });
        }
        private void UpdateTask(Dictionary<string, object> state)
        {
            if (temporaryMessage && DateTime.UtcNow < messageUntil) return;
            temporaryMessage = false;
            bool active = DshBridge.Flag(state, "busy"), failure = DshBridge.Flag(state, "failure");
            string nextTask = DshBridge.Text(state, "task");
            if (active)
            {
                if (!working || nextTask != task) { workStarted = DateTime.UtcNow; finished = false; }
                working = true; task = nextTask; currentSession = DshBridge.Text(state, "session");
                string tool = DshBridge.Text(state, "tools"), draft = DshBridge.Text(state, "draft"), step = DshBridge.Text(state, "step");
                SetPose(failure ? "failure" : "work");
                heading.Text = (folded ? "▸ " : "▾ ") + "工作中 · " + (int)(DateTime.UtcNow - workStarted).TotalSeconds + "s";
                paragraph.Text = (task.Length > 0 ? "📋 " + Shorten(task, 90) + "\n" : "") +
                    (tool.Length > 0 ? "🔧 " + tool : draft.Length > 0 ? Shorten(draft, 140) : "正在思考…") +
                    (step.Length > 0 ? " · 第 " + step + " 步" : "");
                paragraph.Visibility = folded ? Visibility.Collapsed : Visibility.Visible;
                completion.Visibility = Visibility.Collapsed; speech.Visibility = Visibility.Visible; Layout();
            }
            else if (working)
            {
                working = false; finished = true; SetPose(failure ? "failure" : "success");
                heading.Text = failure ? "任务遇到问题" : "✅ 任务已完成"; paragraph.Text = Shorten(task, 90);
                paragraph.Visibility = Visibility.Visible; completion.Visibility = Visibility.Visible; speech.Visibility = Visibility.Visible; Layout();
            }
        }
        private static string Shorten(string value, int limit)
        {
            value = value.Replace('\r', ' ').Replace('\n', ' '); return value.Length <= limit ? value : value.Substring(0, limit) + "…";
        }
        private static string StartupFile()
        {
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "MarsNewPet.cmd");
        }
        private void SetStartup(bool enabled)
        {
            try {
                if (enabled) File.WriteAllText(StartupFile(), "@echo off\r\nstart \"\" \"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"\r\n");
                else if (File.Exists(StartupFile())) File.Delete(StartupFile());
            } catch (Exception error) { Say(error.Message, 4000); }
        }
    }
}
