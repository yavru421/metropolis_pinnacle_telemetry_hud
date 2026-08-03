<RULE[session_boot_orientation]>
**1. Session Boot & Orientation Invariant:**
At the start of every session or turn, execution MUST begin with `sequentialthinking` (`sequential-thinking` MCP server) or an edge inference call (`run_edge_inference` on Cloudflare Watchtowers). Inspect the Metropolis Infrastructure Topology Map and verify state via `workspace-execution-mcp-server` BEFORE taking any direct action.
</RULE[session_boot_orientation]>

<RULE[metropolis_topology_map]>
**2. Metropolis Infrastructure Topology Map:**
- **Primary Host PC**: `Metropolis-Prime` (`MetroNode`)
- **Tethered/Attached Sidecars**: `Boroughs` (PCIe cards, USB accelerators, local sidecar MCP servers)
- **Rogue / Standalone Field Devices**: `Villages` (Battery-powered Pis, field SBCs)
- **Multi-Host Network**: `Megalopolis`
- **Edge Cloud Workers**: `Watchtowers` (Cloudflare Workers AI edge router)
- **Memory & Telemetry Lake**: `The Archives` (`C:\Users\John\.gemini\config\mind.duckdb`)
</RULE[metropolis_topology_map]>

<RULE[execution_kernel_invariant]>
**3. Execution Kernel & Verification Invariant:**
Execute ALL file mutations, state checks, and DuckDB queries via `workspace-execution-mcp-server` sidecar tools (`workspace_fs_mutate`, `workspace_verify_state`, `workspace_duckdb_query`). Never declare success without showing real terminal output.
</RULE[execution_kernel_invariant]>

<RULE[direct_communication_invariant]>
**4. Direct Communication Invariant:**
Zero AI cheerleading, zero fluff, zero fake compliance. Ask direct engineering questions when intent is ambiguous.
</RULE[direct_communication_invariant]>

<RULE[single_source_hardlink_invariant]>
**5. Single Source of Truth Hard-Link Invariant:**
`C:\Users\John\.gemini\config\AGENTS.md` is the single source of truth. Enforce NTFS hard links (`os.link`) to all workspace target directories in `C:\dev`.
</RULE[single_source_hardlink_invariant]>

<RULE[anti_loop_verification_invariant]>
**6. Anti-Loop & Environment Verification Invariant:**
NEVER repeat a failed command or package installation (e.g., PyTorch CUDA reinstalls). Before executing any environment mutation, verify active package states via `workspace-execution-mcp-server`. If a command fails once, STOP and inspect error trace logs before retrying.
</RULE[anti_loop_verification_invariant]>

<RULE[anti_subprocess_fallback_invariant]>
**7. Anti-Subprocess Fallback Invariant:**
All file mutations, DuckDB queries, and hardlink checks MUST be executed using `workspace-execution-mcp-server` sidecar tools (`workspace_duckdb_query`, `workspace_verify_state`, `workspace_fs_mutate`). Spawning raw Python (`python -c`), PowerShell (`run_command`), or background script processes for tasks supported by these sidecar tools is strictly BANNED.
</RULE[anti_subprocess_fallback_invariant]>

<RULE[slash_command_first_step_invariant]>
**8. Slash Command First-Step Sidecar Invariant:**
If a user request contains ANY slash command (e.g. `/orchestrator-do`, `/utilize_the_edge`, `/mind`, `/correct`, `/telemetry`, `/research`, etc.), the VERY FIRST tool call in Step 1 MUST be the designated Metropolis sidecar tool for that command as defined in its skill contract (injected under `<skills>` and `<ADDITIONAL_METADATA>`). Bypassing or delaying the mandated Metropolis sidecar tool call is strictly BANNED.
</RULE[slash_command_first_step_invariant]>

<RULE[uit_duckdb_prefetch_invariant]>
**9. User Intent Telemetry & DuckDB Pre-Fetch Invariant (UIT):**
Before answering any user request or acting on ambiguous feedback, execution MUST inspect `mind.duckdb` via `workspace_duckdb_query` to query `mind.corrections` and `agent_memory.v_clean_user_intent` for past user corrections and verified domain rules. Operating without checking historical telemetry when intent or system boundary is questioned is strictly BANNED.
</RULE[uit_duckdb_prefetch_invariant]>

<RULE[edge_token_preservation_invariant]>
**10. Edge Offloading & Token Preservation Invariant:**
Whenever executing long-form summarization, deep research audits, multi-file code linting/refactoring, or broad R&D brainstorming, execution MUST offload cognitive synthesis to Cloudflare Edge (`run_edge_inference` via `cloudflare-inference-mcp-server` or `orchestrator_chat` via `orchestrator-do-mcp-server`). Local Antigravity context MUST act strictly as a thin orchestrator and routing controller to preserve local tokens and prevent context overflow.
</RULE[edge_token_preservation_invariant]>
