using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace MetropolisHUD
{
    public partial class MainWindow : Window
    {
        private const string SignalFile  = @"C:\Users\John\.gemini\config\hud_signal.json";
        private const string HistoryFile = @"C:\Users\John\.gemini\config\hud_history.log";
        private const string MindDbFile  = @"C:\Users\John\.gemini\config\mind.duckdb";
        private const string AgentsFile  = @"C:\Users\John\.gemini\config\AGENTS.md";

        private readonly DispatcherTimer _timer;
        private DateTime _lastSignalMtime = DateTime.MinValue;
        private DateTime _lastDuckDbMtime = DateTime.MinValue;
        private DateTime _lastAgentsMtime = DateTime.MinValue;

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
        private readonly System.Collections.Generic.List<string> _logEntries = new System.Collections.Generic.List<string>();

        public MainWindow()
        {
            InitializeComponent();

            LoadPersistentHistory();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void LoadPersistentHistory()
        {
            try
            {
                if (File.Exists(HistoryFile))
                {
                    string[] lines = File.ReadAllLines(HistoryFile);
                    int start = Math.Max(0, lines.Length - 100);
                    for (int i = start; i < lines.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(lines[i]))
                        {
                            _logEntries.Add(lines[i]);
                        }
                    }
                    _eventCount = _logEntries.Count;
                    TxtCounter.Text = $"EVENTS: {_eventCount}";
                    TxtLog.Text = string.Join(Environment.NewLine, _logEntries);
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => LogScrollViewer.ScrollToEnd()));
                }
            }
            catch
            {
                // Silently handle startup file reads
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            try
            {
                // Decay LED flashes
                if (_thoughtTicks > 0) { _thoughtTicks--; if (_thoughtTicks == 0) LedThought.Fill = _brushOff; }
                if (_duckDbTicks > 0)  { _duckDbTicks--;  if (_duckDbTicks == 0)  LedDuckDb.Fill  = _brushOff; }
                if (_edgeTicks > 0)    { _edgeTicks--;    if (_edgeTicks == 0)    LedEdge.Fill    = _brushOff; }
                if (_mcpTicks > 0)     { _mcpTicks--;     if (_mcpTicks == 0)     LedMcp.Fill     = _brushOff; }
                if (_skillsTicks > 0)  { _skillsTicks--;  if (_skillsTicks == 0)  LedSkills.Fill  = _brushOff; }
                if (_mutateTicks > 0)  { _mutateTicks--;  if (_mutateTicks == 0)  LedMutate.Fill  = _brushOff; }
                if (_agentTicks > 0)   { _agentTicks--;   if (_agentTicks == 0)   LedAgent.Fill   = _brushOff; }
                if (_searchTicks > 0)  { _searchTicks--;  if (_searchTicks == 0)  LedSearch.Fill  = _brushOff; }
                if (_errorTicks > 0)   { _errorTicks--;   if (_errorTicks == 0)   LedError.Fill   = _brushOff; }

                // 1. Check Signal JSON Bus
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

                        TriggerChannel(channel, time, detail);
                    }
                }

                // 2. Check DuckDB mtime
                if (File.Exists(MindDbFile))
                {
                    var fi = new FileInfo(MindDbFile);
                    if (fi.LastWriteTime > _lastDuckDbMtime)
                    {
                        if (_lastDuckDbMtime != DateTime.MinValue)
                        {
                            TriggerChannel("DUCKDB", DateTime.Now.ToString("HH:mm:ss"), "mind.duckdb query/write event");
                        }
                        _lastDuckDbMtime = fi.LastWriteTime;
                    }
                }

                // 3. Check AGENTS.md mtime
                if (File.Exists(AgentsFile))
                {
                    var fi = new FileInfo(AgentsFile);
                    if (fi.LastWriteTime > _lastAgentsMtime)
                    {
                        if (_lastAgentsMtime != DateTime.MinValue)
                        {
                            TriggerChannel("MUTATE", DateTime.Now.ToString("HH:mm:ss"), "AGENTS.md invariant updated");
                        }
                        _lastAgentsMtime = fi.LastWriteTime;
                    }
                }
            }
            catch
            {
                // Silently handle IO locks
            }
        }

        private void TriggerChannel(string channel, string time, string detail)
        {
            _eventCount++;
            TxtCounter.Text = $"EVENTS: {_eventCount}";

            string logLine = $"[{time}] {channel}: {detail}";
            _logEntries.Add(logLine);
            if (_logEntries.Count > 150)
            {
                _logEntries.RemoveAt(0);
            }
            TxtLog.Text = string.Join(Environment.NewLine, _logEntries);
            LogScrollViewer.ScrollToEnd();

            try
            {
                File.AppendAllText(HistoryFile, logLine + Environment.NewLine);
            }
            catch
            {
                // Silently handle history write contention
            }

            switch (channel)
            {
                case "THOUGHT":
                    LedThought.Fill = _brushPurple;
                    _thoughtTicks = 15;
                    TxtThoughtTrace.Text = $"THOUGHT TRACE: {detail}";
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
                    _errorTicks = 30; // 3 second hold for errors
                    break;
            }
        }
    }
}
