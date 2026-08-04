# ⚡ MetropolisHUD: Ambient Telemetry & Stealth Visualizer for Autonomous AI Agents

> **Peripheral vision for autonomous AI agents on MetroNode.**

MetropolisHUD is a lightweight, zero-latency, Windows-native WPF telemetry overlay designed for developers and AI operators running autonomous agent workflows. Rather than burying agent activity inside dense terminal logs, web dashboards, or IDE output windows, MetropolisHUD floats directly over your workspace with Win32 click-through pass-through capability (`Win+H`), visually broadcasting live agent cognitive cycles via real-time glowing ambient badges and status streams.

---

## 💎 Key Features

* **Stealth Translucent Display**: Ultra-clean `#18050914` translucent dark backing that sits quietly on top of your workspace (`Topmost=True`, `AllowsTransparency=True`).
* **11-Channel Telemetry Matrix**: Real-time glowing status badges that ignite the instant a subsystem is touched and slowly cool down over non-linear decay curves.
* **Win32 Click-Through Passthrough (`Win+H`)**: Toggles hardware click-through (`WS_EX_TRANSPARENT`), allowing your mouse clicks to pass directly through the HUD into your IDE/browser underneath.
* **Named Pipe IPC (`\\.\pipe\MetropolisHUDPipe`)**: Zero-overhead inter-process communication protocol for local Python, Rust, TypeScript, or C# sidecars to stream signals instantly.
* **User Activity & Log Stream**: Expandable/collapsible log panel for deep diagnostic inspection when needed, with 1-click collapse (`▼ COLLAPSE LOGS`).
* **Standalone Settings Modal**: Separate configuration window for keybindings, IPC options, and badge decay tuning.

---

## 📡 The 11 Telemetry Channels

| Channel | Badge | Description | Triggering Subsystem |
|---|---|---|---|
| **THOUGHT** | `THOUGHT` | Internal model reasoning & planning | Reasoning step start |
| **SEQTHINK** | `SEQTHINK` | Sequential Thinking MCP sandwich trace | `sequentialthinking` step |
| **DUCKDB** | `DUCKDB` | Telemetry lake queries & updates | `mind.duckdb` / `agent_memory` |
| **EDGE** | `EDGE` | Cloudflare Workers AI edge router | `run_edge_inference` / `orchestrator_chat` |
| **WRANGLER** | `WRANGLER` | Cloudflare D1/Pages/KV deployment | `wrangler` CLI / MCP sidecar |
| **MCP** | `MCP` | Model Context Protocol tool execution | Native MCP tool call dispatch |
| **SKILLS** | `SKILLS` | Custom skill invocation & workflow | `SKILL.md` execution |
| **MUTATE** | `MUTATE` | Filesystem edits & file creations | `workspace_fs_mutate` / `replace_file_content` |
| **AGENT** | `AGENT` | Subagent spawning & background tasks | `invoke_subagent` / subagent IPC |
| **SEARCH** | `SEARCH` | Local & web search execution | Everything search / web search |
| **ERROR** | `ERROR` | Execution exception / build failure | Command or sidecar exception |

---

## 🏗️ Architecture

```
┌────────────────────────────────────────────────────────┐
│               MetropolisHUD (WPF / C#)                 │
│         Win32 Passthrough Overlay (Win+H)             │
└───────────────────────────▲────────────────────────────┘
                            │ Named Pipe: \\.\pipe\MetropolisHUDPipe
┌───────────────────────────┴────────────────────────────┐
│          MetroNode Local AI Sidecar Engine             │
│   (Python IPC / DuckDB Lake / MCP Tools / Cloudflare)  │
└────────────────────────────────────────────────────────┘
```

---

## 🚀 Building & Running

### Prerequisites
* Windows 10 / 11
* .NET 10 SDK (or .NET 8+)

### Build & Launch
```powershell
# Clone the repository
git clone https://github.com/yavru421/metropolis_pinnacle_telemetry_hud.git
cd metropolis_pinnacle_telemetry_hud

# Build the project
dotnet build MetropolisHUD.csproj -c Release

# Run the HUD
dotnet run --project MetropolisHUD.csproj
```

---

## 🧪 Testing IPC Signals

You can test named pipe signal delivery directly using PowerShell:

```powershell
# Send a test payload to light up DUCKDB & MCP badges
$pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", "MetropolisHUDPipe", [System.IO.Pipes.PipeDirection]::Out)
$pipe.Connect(1000)
$writer = New-Object System.IO.StreamWriter($pipe)
$payload = '{"channel":"DUCKDB","text":"Query executed on mind.duckdb","activity":"Reading corrections table"}'
$writer.WriteLine($payload)
$writer.Flush()
$pipe.Close()
```

---

## 📄 License
MIT License. Built for the Metropolis Infrastructure Topology (`MetroNode`).
