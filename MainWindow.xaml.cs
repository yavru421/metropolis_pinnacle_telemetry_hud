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
        public double Width { get; set; } = 960;
        public double Height { get; set; } = 480;
        public double BadgeFontSize { get; set; } = 20;
        public bool IsLogStreamCollapsed { get; set; } = false;
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
        private FileSystemWatcher? _devWatcher;
        private FileSystemWatcher? _configWatcher;
        private FileSystemWatcher? _brainWatcher;

        private readonly DispatcherTimer _timer;
        private DateTime _lastSignalMtime = DateTime.MinValue;
        private DateTime _lastDuckDbMtime = DateTime.MinValue;
        private DateTime _lastAgentsMtime = DateTime.MinValue;
        private DateTime _lastGitMtime = DateTime.MinValue;
        private DateTime _lastDotnetMtime = DateTime.MinValue;
        private DateTime _lastPythonMtime = DateTime.MinValue;
        private long _lastHistoryPosition = 0;
        private DateTime _currentLogDate = DateTime.Today;

        // Catppuccin Macchiato Palette Brushes
        private readonly SolidColorBrush _brushOff      = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#45475A"));
        private readonly SolidColorBrush _brushPurple   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBA6F7")); // THOUGHT
        private readonly SolidColorBrush _brushLavender = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B4BEFE")); // SEQTHINK
        private readonly SolidColorBrush _brushGreen    = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A6E3A1")); // DUCKDB
        private readonly SolidColorBrush _brushCyan     = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#89B4FA")); // EDGE
        private readonly SolidColorBrush _brushYellow   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9E2AF")); // WRANGLER
        private readonly SolidColorBrush _brushOrange   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAB387")); // MCP
        private readonly SolidColorBrush _brushMagenta  = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5C2E7")); // SKILLS
        private readonly SolidColorBrush _brushMaroon   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBA0AC")); // MUTATE
        private readonly SolidColorBrush _brushCoral    = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E78284")); // AGENT
        private readonly SolidColorBrush _brushTeal     = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94E2D5")); // SEARCH
        private readonly SolidColorBrush _brushRed      = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F38BA8")); // ERROR

        private int _thoughtTicks = 0;
        private int _seqthinkTicks = 0;
        private int _duckDbTicks = 0;
        private int _edgeTicks = 0;
        private int _wranglerTicks = 0;
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

            MouseDown += (s, ev) =>
            {
                if (ev.ChangedButton == System.Windows.Input.MouseButton.Left)
                    DragMove();
            };

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
            InitFileSystemWatchers();
        }

        private void InitFileSystemWatchers()
        {
            try
            {
                if (Directory.Exists(@"C:\dev"))
                {
                    _devWatcher = new FileSystemWatcher(@"C:\dev")
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                        Filter = "*.*",
                        EnableRaisingEvents = true
                    };
                    _devWatcher.Changed += OnDevFileChanged;
                    _devWatcher.Created += OnDevFileChanged;
                }

                string configDir = @"C:\Users\John\.gemini\config";
                if (Directory.Exists(configDir))
                {
                    _configWatcher = new FileSystemWatcher(configDir)
                    {
                        IncludeSubdirectories = false,
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                        Filter = "*.*",
                        EnableRaisingEvents = true
                    };
                    _configWatcher.Changed += OnConfigDirectoryChanged;
                    _configWatcher.Created += OnConfigDirectoryChanged;
                }

                string brainDir = @"C:\Users\John\.gemini\antigravity\brain";
                if (Directory.Exists(brainDir))
                {
                    _brainWatcher = new FileSystemWatcher(brainDir)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                        Filter = "*.jsonl",
                        EnableRaisingEvents = true
                    };
                    _brainWatcher.Changed += OnBrainDirectoryChanged;
                    _brainWatcher.Created += OnBrainDirectoryChanged;
                }
            }
            catch { }
        }

        private void OnDevFileChanged(object sender, FileSystemEventArgs e)
        {
            string ext = System.IO.Path.GetExtension(e.FullPath).ToLower();
            string name = e.Name ?? "";
            if (name.Contains("hud_signal.json") || name.Contains("hud_history.log") || name.Contains(".git") || name.Contains("obj") || name.Contains("bin"))
                return;

            if (ext == ".cs" || ext == ".py" || ext == ".json" || ext == ".js" || ext == ".ts" || ext == ".md")
            {
                Dispatcher.Invoke(() => TriggerChannel("MUTATE", DateTime.Now.ToString("HH:mm:ss"), $"{e.ChangeType}: {System.IO.Path.GetFileName(e.FullPath)}", appendToHistory: false));
            }
        }

        private void OnConfigDirectoryChanged(object sender, FileSystemEventArgs e)
        {
            string name = e.Name ?? "";
            if (name.EndsWith(".duckdb") || name.EndsWith(".duckdb-wal"))
            {
                Dispatcher.Invoke(() => TriggerChannel("DUCKDB", DateTime.Now.ToString("HH:mm:ss"), $"Database update: {name}", appendToHistory: false));
            }
            else if (name.Equals("AGENTS.md", StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.Invoke(() => TriggerChannel("AGENT", DateTime.Now.ToString("HH:mm:ss"), "AGENTS.md updated", appendToHistory: false));
            }
        }

        private void OnBrainDirectoryChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (!e.FullPath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
                    return;

                string channel = "THOUGHT";
                string detail = "Agent transcript step logged";

                using (var fs = new FileStream(e.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs, Encoding.UTF8))
                {
                    string? lastLine = null;
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            lastLine = line;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(lastLine))
                    {
                        if (lastLine.Contains("wrangler_"))
                        {
                            channel = "WRANGLER";
                            detail = "Cloudflare Wrangler MCP CLI operations";
                        }
                        else if (lastLine.Contains("run_edge_inference") || lastLine.Contains("orchestrator_chat") || lastLine.Contains("cloudflare"))
                        {
                            channel = "EDGE";
                            detail = "Cloudflare Workers AI edge router synthesis";
                        }
                        else if (lastLine.Contains("workspace_duckdb_query") || lastLine.Contains("duckdb"))
                        {
                            channel = "DUCKDB";
                            detail = "DuckDB telemetry database query";
                        }
                        else if (lastLine.Contains("sequentialthinking"))
                        {
                            channel = "SEQTHINK";
                            detail = "SequentialThinking cognitive trace step";
                        }
                        else if (lastLine.Contains("grep_search") || lastLine.Contains("view_file") || lastLine.Contains("list_dir") || lastLine.Contains("read_url"))
                        {
                            channel = "SEARCH";
                            detail = "Workspace search & inspection tool";
                        }
                        else if (lastLine.Contains("replace_file_content") || lastLine.Contains("multi_replace_file_content") || lastLine.Contains("write_to_file") || lastLine.Contains("workspace_fs_mutate") || lastLine.Contains("workspace_verify_state"))
                        {
                            channel = "MUTATE";
                            detail = "Workspace file mutation";
                        }
                        else if (lastLine.Contains("run_command") || lastLine.Contains("exec_cmd") || lastLine.Contains("manage_task"))
                        {
                            channel = "MCP";
                            detail = "Process & terminal execution sidecar";
                        }
                    }
                }

                Dispatcher.Invoke(() => TriggerChannel(channel, DateTime.Now.ToString("HH:mm:ss"), detail, appendToHistory: false));
            }
            catch
            {
                Dispatcher.Invoke(() => TriggerChannel("THOUGHT", DateTime.Now.ToString("HH:mm:ss"), "Agent transcript step logged", appendToHistory: false));
            }
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

                    if (fi.LastWriteTime.Date < DateTime.Today)
                    {
                        _logEntries.Clear();
                        _eventCount = 0;
                        TxtCounter.Text = "EVENTS: 0";
                        RenderLogs();
                        return;
                    }

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
            DateTime entryDate = DateTime.Today;

            int firstBracketClose = rawLine.IndexOf(']');
            if (firstBracketClose > 1 && rawLine.StartsWith("["))
            {
                string headerStr = rawLine.Substring(1, firstBracketClose - 1);
                if (DateTime.TryParse(headerStr, out DateTime parsedDt))
                {
                    entryDate = parsedDt.Date;
                    time = parsedDt.ToString("HH:mm:ss");
                }
                else
                {
                    time = headerStr;
                }

                string rest = rawLine.Substring(firstBracketClose + 1).Trim();
                int colonIdx = rest.IndexOf(':');
                if (colonIdx > 0)
                {
                    channel = rest.Substring(0, colonIdx).Trim().ToUpper();
                    detail = rest.Substring(colonIdx + 1).Trim();
                }
            }

            if (entryDate != DateTime.Today)
            {
                return; // Daily visual ledger reset: filter past dates from visual HUD ledger
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
                // Midnight rollover check for daily visual ledger reset
                if (DateTime.Today != _currentLogDate)
                {
                    _currentLogDate = DateTime.Today;
                    _logEntries.Clear();
                    _eventCount = 0;
                    TxtCounter.Text = "EVENTS: 0";
                    ParseAndAddLogEntry($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] AGENT: --- DAILY LEDGER VISUAL RESET ({_currentLogDate:yyyy-MM-dd}) ---");
                    RenderLogs();
                }

                // Stealth Badge Opacity Decay (50 ticks = 5 second slow cool-down)
                if (_thoughtTicks > 0)  { _thoughtTicks--;  BadgeThought.Opacity  = _thoughtTicks / 50.0; }
                if (_seqthinkTicks > 0){ _seqthinkTicks--; BadgeSeqthink.Opacity = _seqthinkTicks / 50.0; }
                if (_duckDbTicks > 0)  { _duckDbTicks--;   BadgeDuckDb.Opacity   = _duckDbTicks / 50.0; }
                if (_edgeTicks > 0)    { _edgeTicks--;     BadgeEdge.Opacity     = _edgeTicks / 50.0; }
                if (_wranglerTicks > 0){ _wranglerTicks--; BadgeWrangler.Opacity = _wranglerTicks / 50.0; }
                if (_mcpTicks > 0)     { _mcpTicks--;      BadgeMcp.Opacity      = _mcpTicks / 50.0; }
                if (_skillsTicks > 0)  { _skillsTicks--;   BadgeSkills.Opacity   = _skillsTicks / 50.0; }
                if (_mutateTicks > 0)  { _mutateTicks--;   BadgeMutate.Opacity   = _mutateTicks / 50.0; }
                if (_agentTicks > 0)   { _agentTicks--;    BadgeAgent.Opacity    = _agentTicks / 50.0; }
                if (_searchTicks > 0)  { _searchTicks--;   BadgeSearch.Opacity   = _searchTicks / 50.0; }
                if (_errorTicks > 0)   { _errorTicks--;    BadgeError.Opacity    = _errorTicks / 50.0; }

                CheckHistoryLogStream();
                CheckSignalJson();
                CheckFileWatchers();
                UpdateSparklinePulse();
            }
            catch { }
        }

        private void CheckSignalJson()
        {
            try
            {
                if (File.Exists(SignalFile))
                {
                    var fi = new FileInfo(SignalFile);
                    if (fi.LastWriteTime > _lastSignalMtime)
                    {
                        _lastSignalMtime = fi.LastWriteTime;
                        string json;
                        using (var stream = new FileStream(SignalFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            json = reader.ReadToEnd();
                        }

                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;

                            string channel = root.GetProperty("channel").GetString()?.ToUpper() ?? "";
                            string detail  = root.GetProperty("detail").GetString() ?? "";
                            string time    = root.GetProperty("timestamp").GetString() ?? DateTime.Now.ToString("HH:mm:ss");

                            TriggerChannel(channel, time, detail, appendToHistory: false);
                        }
                    }
                }
            }
            catch { }
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
                    string fullTimeLogLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {channel}: {detail}";
                    File.AppendAllText(HistoryFile, fullTimeLogLine + Environment.NewLine);
                    if (File.Exists(HistoryFile))
                    {
                        _lastHistoryPosition = new FileInfo(HistoryFile).Length;
                    }

                    // Auto-pipe telemetry into DuckDB JSONL telemetry stream
                    string jsonlFile = @"C:\Users\John\.gemini\config\hud_telemetry.jsonl";
                    var jsonObject = new
                    {
                        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        channel = channel,
                        detail = detail,
                        source = "MetropolisHUD"
                    };
                    string jsonLine = JsonSerializer.Serialize(jsonObject);
                    File.AppendAllText(jsonlFile, jsonLine + Environment.NewLine);
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
                    BadgeThought.Opacity = 1.0;
                    _thoughtTicks = 50;
                    TxtThoughtTrace.Text = $"THOUGHT: {detail}";
                    break;
                case "SEQTHINK":
                    BadgeSeqthink.Opacity = 1.0;
                    _seqthinkTicks = 50;
                    TxtThoughtTrace.Text = $"SEQTHINK: {detail}";
                    break;
                case "DUCKDB":
                    BadgeDuckDb.Opacity = 1.0;
                    _duckDbTicks = 50;
                    break;
                case "EDGE":
                    BadgeEdge.Opacity = 1.0;
                    _edgeTicks = 50;
                    break;
                case "WRANGLER":
                    BadgeWrangler.Opacity = 1.0;
                    _wranglerTicks = 50;
                    break;
                case "MCP":
                    BadgeMcp.Opacity = 1.0;
                    _mcpTicks = 50;
                    break;
                case "SKILLS":
                    BadgeSkills.Opacity = 1.0;
                    _skillsTicks = 50;
                    break;
                case "MUTATE":
                    BadgeMutate.Opacity = 1.0;
                    _mutateTicks = 50;
                    break;
                case "AGENT":
                    BadgeAgent.Opacity = 1.0;
                    _agentTicks = 50;
                    break;
                case "SEARCH":
                    BadgeSearch.Opacity = 1.0;
                    _searchTicks = 50;
                    break;
                case "ERROR":
                case "FAIL":
                    BadgeError.Opacity = 1.0;
                    _errorTicks = 75;
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
                    "THOUGHT"  => _brushPurple,
                    "SEQTHINK" => _brushLavender,
                    "DUCKDB"   => _brushGreen,
                    "EDGE"     => _brushCyan,
                    "WRANGLER" => _brushYellow,
                    "MCP"      => _brushOrange,
                    "SKILLS"   => _brushMagenta,
                    "MUTATE"   => _brushMaroon,
                    "AGENT"    => _brushCoral,
                    "SEARCH"   => _brushTeal,
                    "ERROR"    => _brushRed,
                    "FAIL"     => _brushRed,
                    _          => _brushPurple
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



        public double CurrentBadgeFontSize => TxtThought?.FontSize ?? 20;
        public bool IsLogStreamCollapsed => BorderLogContainer?.Visibility == Visibility.Collapsed;

        public void SetBadgeFontSize(double newSize)
        {
            if (TxtThought != null) TxtThought.FontSize = newSize;
            if (TxtSeqthink != null) TxtSeqthink.FontSize = newSize;
            if (TxtDuckDb != null) TxtDuckDb.FontSize = newSize;
            if (TxtEdge != null) TxtEdge.FontSize = newSize;
            if (TxtWrangler != null) TxtWrangler.FontSize = newSize;
            if (TxtMcp != null) TxtMcp.FontSize = newSize;
            if (TxtSkills != null) TxtSkills.FontSize = newSize;
            if (TxtMutate != null) TxtMutate.FontSize = newSize;
            if (TxtAgent != null) TxtAgent.FontSize = newSize;
            if (TxtSearch != null) TxtSearch.FontSize = newSize;
            if (TxtError != null) TxtError.FontSize = newSize;

            SaveConfig();
        }

        public void SetLogStreamCollapsed(bool collapsed)
        {
            if (BorderLogContainer != null)
            {
                BorderLogContainer.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            }
            if (BtnToggleLogs != null)
            {
                BtnToggleLogs.Content = collapsed ? "▲ EXPAND LOGS" : "▼ COLLAPSE LOGS";
            }
            SaveConfig();
        }

        private void BtnToggleLogs_Click(object sender, RoutedEventArgs e)
        {
            SetLogStreamCollapsed(!IsLogStreamCollapsed);
        }

        private void BtnOpenSettingsWindow_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SettingsWindow(this)
            {
                Owner = this
            };
            dlg.ShowDialog();
        }

        private void BtnCloseApp_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public void SaveCurrentConfig()
        {
            SaveConfig();
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
                        SetBadgeFontSize(cfg.BadgeFontSize >= 10 && cfg.BadgeFontSize <= 48 ? cfg.BadgeFontSize : 20);
                        SetLogStreamCollapsed(cfg.IsLogStreamCollapsed);
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
                    Height = Height,
                    BadgeFontSize = CurrentBadgeFontSize,
                    IsLogStreamCollapsed = IsLogStreamCollapsed
                };
                string json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFile, json);
            }
            catch { }
        }
    }
}
