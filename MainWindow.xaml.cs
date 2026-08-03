using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;
using Button = System.Windows.Controls.Button;
using WinForms = System.Windows.Forms;

namespace MetropolisHUD
{
    public struct LogItem
    {
        public string Time { get; set; }
        public string Channel { get; set; }
        public string Detail { get; set; }
        public string RawLine { get; set; }
    }

    public class HudConfig
    {
        public double Top { get; set; } = 50;
        public double Left { get; set; } = 50;
        public double Width { get; set; } = 680;
        public double Height { get; set; } = 360;
    }

    public partial class MainWindow : Window
    {
        private const string SignalFile  = @"C:\Users\John\.gemini\config\hud_signal.json";
        private const string HistoryFile = @"C:\Users\John\.gemini\config\hud_history.log";
        private const string MindDbFile  = @"C:\Users\John\.gemini\config\mind.duckdb";
        private const string AgentsFile  = @"C:\Users\John\.gemini\config\AGENTS.md";
        private const string ConfigFile  = @"C:\Users\John\.gemini\config\hud_config.json";
        private const string PipeName    = "MetropolisHUDPipe";

        // Win32 Interop
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WM_HOTKEY = 0x0312;

        private const int HOTKEY_ID_WIN_H = 9001;
        private const int HOTKEY_ID_CTRL_SHIFT_H = 9002;

        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint VK_H = 0x48;

        private bool _isClickThrough = false;
        private HwndSource? _hwndSource;
        private WinForms.NotifyIcon? _notifyIcon;
        private CancellationTokenSource? _pipeCts;

        private readonly DispatcherTimer _timer;
        private DateTime _lastSignalMtime = DateTime.MinValue;
        private DateTime _lastDuckDbMtime = DateTime.MinValue;
        private DateTime _lastAgentsMtime = DateTime.MinValue;
        private DateTime _lastGitMtime = DateTime.MinValue;
        private DateTime _lastDotnetMtime = DateTime.MinValue;
        private DateTime _lastPythonMtime = DateTime.MinValue;
        private long _lastHistoryPosition = 0;

        // Catppuccin Macchiato Palette Brushes
        private readonly SolidColorBrush _brushOff     = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#45475A"));
        private readonly SolidColorBrush _brushPurple  = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBA6F7")); // THOUGHT
        private readonly SolidColorBrush _brushGreen   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A6E3A1")); // DUCKDB
        private readonly SolidColorBrush _brushCyan    = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#89B4FA")); // EDGE
        private readonly SolidColorBrush _brushOrange  = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAB387")); // MCP
        private readonly SolidColorBrush _brushMagenta = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5C2E7")); // SKILLS
        private readonly SolidColorBrush _brushGold    = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9E2AF")); // MUTATE
        private readonly SolidColorBrush _brushCoral   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E78284")); // AGENT
        private readonly SolidColorBrush _brushTeal    = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94E2D5")); // SEARCH
        private readonly SolidColorBrush _brushRed     = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F38BA8")); // ERROR

        private int _thoughtTicks = 0;
        private int _duckDbTicks = 0;
        private int _edgeTicks = 0;
        private int _mcpTicks = 0;
        private int _skillsTicks = 0;
        private int _mutateTicks = 0;
        private int _agentTicks = 0;
        private int _searchTicks = 0;
        private int _errorTicks = 0;

        private int _eventCount = 0;
        private readonly List<LogItem> _logEntries = new List<LogItem>();
        private readonly List<DateTime> _eventTimestamps = new List<DateTime>();

        public MainWindow()
        {
            InitializeComponent();

            LoadConfig();
            LoadPersistentHistory();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            _hwndSource = HwndSource.FromHwnd(handle);
            _hwndSource?.AddHook(HwndHook);

            RegisterHotKey(handle, HOTKEY_ID_WIN_H, MOD_WIN, VK_H);
            RegisterHotKey(handle, HOTKEY_ID_CTRL_SHIFT_H, MOD_CONTROL | MOD_SHIFT, VK_H);

            Topmost = true;
            Activate();
            Focus();
            try
            {
                SetForegroundWindow(handle);
                BringWindowToTop(handle);
            }
            catch { }

            InitSystemTray();

            _pipeCts = new CancellationTokenSource();
            Task.Run(() => StartNamedPipeServer(_pipeCts.Token));
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                UnregisterHotKey(handle, HOTKEY_ID_WIN_H);
                UnregisterHotKey(handle, HOTKEY_ID_CTRL_SHIFT_H);
                _hwndSource?.RemoveHook(HwndHook);

                _pipeCts?.Cancel();
                _notifyIcon?.Dispose();

                SaveConfig();
            }
            catch { }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID_WIN_H)
                {
                    ToggleClickThrough();
                    handled = true;
                }
                else if (id == HOTKEY_ID_CTRL_SHIFT_H)
                {
                    ToggleVisibility();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void ToggleClickThrough()
        {
            _isClickThrough = !_isClickThrough;
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            if (_isClickThrough)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
                BadgeClickThrough.Visibility = Visibility.Visible;
            }
            else
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
                BadgeClickThrough.Visibility = Visibility.Collapsed;
            }
        }

        private void ToggleVisibility()
        {
            if (IsVisible)
            {
                Hide();
            }
            else
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            }
        }

        private void InitSystemTray()
        {
            _notifyIcon = new WinForms.NotifyIcon
            {
                Text = "Metropolis Telemetry HUD",
                Icon = System.Drawing.SystemIcons.Application,
                Visible = true
            };

            var contextMenu = new WinForms.ContextMenuStrip();
            contextMenu.Items.Add("Show HUD", null, (s, e) => { Show(); WindowState = WindowState.Normal; Activate(); });
            contextMenu.Items.Add("Hide HUD", null, (s, e) => Hide());
            contextMenu.Items.Add("Toggle Pass-Through (Win+H)", null, (s, e) => ToggleClickThrough());
            contextMenu.Items.Add("Reset Position", null, (s, e) => { Top = 50; Left = 50; SaveConfig(); });
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Exit", null, (s, e) => System.Windows.Application.Current.Shutdown());

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => ToggleVisibility();
        }

        private async Task StartNamedPipeServer(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var pipeServer = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await pipeServer.WaitForConnectionAsync(token);

                    using var reader = new StreamReader(pipeServer, Encoding.UTF8);
                    string? line = await reader.ReadLineAsync(token);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(line);
                            var root = doc.RootElement;
                            string channel = root.GetProperty("channel").GetString()?.ToUpper() ?? "MCP";
                            string detail  = root.GetProperty("detail").GetString() ?? "";
                            string time    = root.GetProperty("timestamp").GetString() ?? DateTime.Now.ToString("HH:mm:ss");

                            Dispatcher.Invoke(() => TriggerChannel(channel, time, detail, appendToHistory: true));
                        }
                        catch
                        {
                            Dispatcher.Invoke(() => TriggerChannel("MCP", DateTime.Now.ToString("HH:mm:ss"), line, appendToHistory: true));
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(200, token);
                }
            }
        }

        private void LoadPersistentHistory()
        {
            try
            {
                if (File.Exists(HistoryFile))
                {
                    var fi = new FileInfo(HistoryFile);
                    _lastHistoryPosition = fi.Length;

                    string[] lines = File.ReadAllLines(HistoryFile);
                    int start = Math.Max(0, lines.Length - 100);
                    for (int i = start; i < lines.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(lines[i]))
                        {
                            ParseAndAddLogEntry(lines[i]);
                        }
                    }
                    _eventCount = _logEntries.Count;
                    TxtCounter.Text = $"EVENTS: {_eventCount}";
                    RenderLogs();
                }
            }
            catch { }
        }

        private void ParseAndAddLogEntry(string rawLine)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            string channel = "THOUGHT";
            string detail = rawLine;

            int firstBracketClose = rawLine.IndexOf(']');
            if (firstBracketClose > 1 && rawLine.StartsWith("["))
            {
                time = rawLine.Substring(1, firstBracketClose - 1);
                string rest = rawLine.Substring(firstBracketClose + 1).Trim();
                int colonIdx = rest.IndexOf(':');
                if (colonIdx > 0)
                {
                    channel = rest.Substring(0, colonIdx).Trim().ToUpper();
                    detail = rest.Substring(colonIdx + 1).Trim();
                }
            }

            _logEntries.Add(new LogItem { Time = time, Channel = channel, Detail = detail, RawLine = rawLine });
            if (_logEntries.Count > 200)
            {
                _logEntries.RemoveAt(0);
            }
        }

        private void CheckHistoryLogStream()
        {
            try
            {
                if (File.Exists(HistoryFile))
                {
                    var fi = new FileInfo(HistoryFile);
                    if (fi.Length < _lastHistoryPosition)
                    {
                        _lastHistoryPosition = 0;
                    }

                    if (fi.Length > _lastHistoryPosition)
                    {
                        using var stream = new FileStream(HistoryFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        stream.Position = _lastHistoryPosition;
                        using var reader = new StreamReader(stream, Encoding.UTF8);
                        string? line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                ParseAndTriggerLogLine(line);
                            }
                        }
                        _lastHistoryPosition = stream.Position;
                    }
                }
            }
            catch { }
        }

        private void ParseAndTriggerLogLine(string rawLine)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            string channel = "THOUGHT";
            string detail = rawLine;

            int firstBracketClose = rawLine.IndexOf(']');
            if (firstBracketClose > 1 && rawLine.StartsWith("["))
            {
                time = rawLine.Substring(1, firstBracketClose - 1);
                string rest = rawLine.Substring(firstBracketClose + 1).Trim();
                int colonIdx = rest.IndexOf(':');
                if (colonIdx > 0)
                {
                    channel = rest.Substring(0, colonIdx).Trim().ToUpper();
                    detail = rest.Substring(colonIdx + 1).Trim();
                }
            }

            TriggerChannel(channel, time, detail, appendToHistory: false);
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            try
            {
                // LED Decay
                if (_thoughtTicks > 0) { _thoughtTicks--; if (_thoughtTicks == 0) LedThought.Fill = _brushOff; }
                if (_duckDbTicks > 0)  { _duckDbTicks--;  if (_duckDbTicks == 0)  LedDuckDb.Fill  = _brushOff; }
                if (_edgeTicks > 0)    { _edgeTicks--;    if (_edgeTicks == 0)    LedEdge.Fill    = _brushOff; }
                if (_mcpTicks > 0)     { _mcpTicks--;     if (_mcpTicks == 0)     LedMcp.Fill     = _brushOff; }
                if (_skillsTicks > 0)  { _skillsTicks--;  if (_skillsTicks == 0)  LedSkills.Fill  = _brushOff; }
                if (_mutateTicks > 0)  { _mutateTicks--;  if (_mutateTicks == 0)  LedMutate.Fill  = _brushOff; }
                if (_agentTicks > 0)   { _agentTicks--;   if (_agentTicks == 0)   LedAgent.Fill   = _brushOff; }
                if (_searchTicks > 0)  { _searchTicks--;  if (_searchTicks == 0)  LedSearch.Fill  = _brushOff; }
                if (_errorTicks > 0)   { _errorTicks--;   if (_errorTicks == 0)   LedError.Fill   = _brushOff; }

                CheckHistoryLogStream();
                CheckSignalJson();
                CheckFileWatchers();
                UpdateSparklinePulse();
            }
            catch { }
        }

        private void CheckSignalJson()
        {
            if (File.Exists(SignalFile))
            {
                var fi = new FileInfo(SignalFile);
                if (fi.LastWriteTime > _lastSignalMtime)
                {
                    _lastSignalMtime = fi.LastWriteTime;
                    string json = File.ReadAllText(SignalFile);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    string channel = root.GetProperty("channel").GetString()?.ToUpper() ?? "";
                    string detail  = root.GetProperty("detail").GetString() ?? "";
                    string time    = root.GetProperty("timestamp").GetString() ?? DateTime.Now.ToString("HH:mm:ss");

                    TriggerChannel(channel, time, detail, appendToHistory: true);
                }
            }
        }

        private void CheckFileWatchers()
        {
            if (File.Exists(MindDbFile))
            {
                var fi = new FileInfo(MindDbFile);
                if (fi.LastWriteTime > _lastDuckDbMtime)
                {
                    if (_lastDuckDbMtime != DateTime.MinValue)
                        TriggerChannel("DUCKDB", DateTime.Now.ToString("HH:mm:ss"), "mind.duckdb telemetry database modified", appendToHistory: true);
                    _lastDuckDbMtime = fi.LastWriteTime;
                }
            }

            if (File.Exists(AgentsFile))
            {
                var fi = new FileInfo(AgentsFile);
                if (fi.LastWriteTime > _lastAgentsMtime)
                {
                    if (_lastAgentsMtime != DateTime.MinValue)
                        TriggerChannel("MUTATE", DateTime.Now.ToString("HH:mm:ss"), "AGENTS.md invariant file modified", appendToHistory: true);
                    _lastAgentsMtime = fi.LastWriteTime;
                }
            }

            string gitHead = @"C:\dev\MetropolisHUD\.git\HEAD";
            if (File.Exists(gitHead))
            {
                var fi = new FileInfo(gitHead);
                if (fi.LastWriteTime > _lastGitMtime)
                {
                    if (_lastGitMtime != DateTime.MinValue)
                        TriggerChannel("MUTATE", DateTime.Now.ToString("HH:mm:ss"), "[GIT] Repository commit update detected", appendToHistory: true);
                    _lastGitMtime = fi.LastWriteTime;
                }
            }
        }

        public void TriggerChannel(string channel, string time, string detail, bool appendToHistory = true)
        {
            _eventCount++;
            TxtCounter.Text = $"EVENTS: {_eventCount}";
            _eventTimestamps.Add(DateTime.Now);

            string logLine = $"[{time}] {channel}: {detail}";
            var item = new LogItem { Time = time, Channel = channel, Detail = detail, RawLine = logLine };
            _logEntries.Add(item);
            if (_logEntries.Count > 200)
            {
                _logEntries.RemoveAt(0);
            }

            RenderLogs();

            if (appendToHistory)
            {
                try
                {
                    File.AppendAllText(HistoryFile, logLine + Environment.NewLine);
                    if (File.Exists(HistoryFile))
                    {
                        _lastHistoryPosition = new FileInfo(HistoryFile).Length;
                    }
                }
                catch { }
            }

            if (channel == "ERROR" || channel == "FAIL")
            {
                try { SystemSounds.Exclamation.Play(); } catch { }
            }

            switch (channel)
            {
                case "THOUGHT":
                    LedThought.Fill = _brushPurple;
                    _thoughtTicks = 15;
                    TxtThoughtTrace.Text = $"THOUGHT: {detail}";
                    break;
                case "DUCKDB":
                    LedDuckDb.Fill = _brushGreen;
                    _duckDbTicks = 15;
                    break;
                case "EDGE":
                    LedEdge.Fill = _brushCyan;
                    _edgeTicks = 15;
                    break;
                case "MCP":
                    LedMcp.Fill = _brushOrange;
                    _mcpTicks = 15;
                    break;
                case "SKILLS":
                    LedSkills.Fill = _brushMagenta;
                    _skillsTicks = 15;
                    break;
                case "MUTATE":
                    LedMutate.Fill = _brushGold;
                    _mutateTicks = 15;
                    break;
                case "AGENT":
                    LedAgent.Fill = _brushCoral;
                    _agentTicks = 15;
                    break;
                case "SEARCH":
                    LedSearch.Fill = _brushTeal;
                    _searchTicks = 15;
                    break;
                case "ERROR":
                case "FAIL":
                    LedError.Fill = _brushRed;
                    _errorTicks = 30;
                    break;
            }
        }

        private void RenderLogs()
        {
            DocLog.Blocks.Clear();
            Paragraph p = new Paragraph();

            foreach (var item in _logEntries)
            {
                Run runTime = new Run($"[{item.Time}] ") { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C7086")) };
                p.Inlines.Add(runTime);

                SolidColorBrush channelBrush = item.Channel switch
                {
                    "THOUGHT" => _brushPurple,
                    "DUCKDB"  => _brushGreen,
                    "EDGE"    => _brushCyan,
                    "MCP"     => _brushOrange,
                    "SKILLS"  => _brushMagenta,
                    "MUTATE"  => _brushGold,
                    "AGENT"   => _brushCoral,
                    "SEARCH"  => _brushTeal,
                    "ERROR"   => _brushRed,
                    "FAIL"    => _brushRed,
                    _         => _brushPurple
                };

                Run runChannel = new Run($"{item.Channel}: ") { Foreground = channelBrush, FontWeight = FontWeights.Bold };
                p.Inlines.Add(runChannel);

                Run runDetail = new Run(item.Detail + "\n") { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CDD6F4")) };
                p.Inlines.Add(runDetail);
            }

            DocLog.Blocks.Add(p);
            RichLog.ScrollToEnd();
        }

        private void UpdateSparklinePulse()
        {
            DateTime now = DateTime.Now;
            _eventTimestamps.RemoveAll(ts => (now - ts).TotalSeconds > 60);

            int[] bins = new int[12];
            foreach (var ts in _eventTimestamps)
            {
                double ageSec = (now - ts).TotalSeconds;
                int binIndex = 11 - (int)(ageSec / 5.0);
                if (binIndex >= 0 && binIndex < 12)
                {
                    bins[binIndex]++;
                }
            }

            SparklineCanvas.Children.Clear();
            double width = SparklineCanvas.Width;
            double height = SparklineCanvas.Height;

            int maxCount = Math.Max(1, bins.Max());
            PointCollection points = new PointCollection();

            for (int i = 0; i < 12; i++)
            {
                double x = (i / 11.0) * width;
                double y = height - ((double)bins[i] / maxCount * (height - 4)) - 2;
                points.Add(new Point(x, y));
            }

            Polyline polyline = new Polyline
            {
                Points = points,
                Stroke = _brushCyan,
                StrokeThickness = 1.5
            };

            SparklineCanvas.Children.Add(polyline);
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    string json = File.ReadAllText(ConfigFile);
                    var cfg = JsonSerializer.Deserialize<HudConfig>(json);
                    if (cfg != null)
                    {
                        Top = cfg.Top;
                        Left = cfg.Left;
                        Width = cfg.Width;
                        Height = cfg.Height;
                    }
                }
            }
            catch { }
        }

        private void SaveConfig()
        {
            try
            {
                var cfg = new HudConfig
                {
                    Top = Top,
                    Left = Left,
                    Width = Width,
                    Height = Height
                };
                string json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFile, json);
            }
            catch { }
        }
    }
}
