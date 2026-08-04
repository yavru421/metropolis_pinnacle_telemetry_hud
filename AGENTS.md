<RULE[session_boot_orientation]>
**1. Session Boot & Dynamic Reasoning Invariant:**
Use `sequentialthinking` dynamically when tackling complex multi-step reasoning, architectural design, or non-trivial debugging. Do NOT burn preamble tool calls on simple turns or straightforward tasks.
</RULE[session_boot_orientation]>

<RULE[metropolis_topology_map]>
**2. Metropolis Infrastructure Topology Map:**
- **Primary Host PC**: `Metropolis-Prime` (`MetroNode`)
- **Tethered/Attached Sidecars**: `Boroughs` (PCIe cards, USB accelerators, local sidecar MCP servers: `workspace-execution-mcp-server`, `duckdb-supercharger`, `agy-mcp-server`, `orchestrator-do-mcp-server`, `cloudflare-inference-mcp-server`, `wrangler-mcp-server`, `options-mcp-server`, `telegram-app-mcp-server`)
- **Rogue / Standalone Field Devices**: `Villages` (Battery-powered Pis, field SBCs)
- **Multi-Host Network**: `Megalopolis`
- **Edge Cloud Workers**: `Watchtowers` (Cloudflare Workers AI edge router)
- **Memory & Telemetry Lake**: `The Archives` (`C:\Users\John\.gemini\config\mind.duckdb`, `agent_memory.duckdb`, `st_codex.duckdb`)
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
If a user request contains ANY slash command, the VERY FIRST tool call MUST be the designated sidecar for that command. Bypassing or delaying is strictly BANNED. Read the SKILL.md before firing. Complete slash command → first-step sidecar map:

| Slash Command | First-Step Sidecar | Tool |
|---|---|---|
| `/offload-edge` | `cloudflare-inference-mcp-server` | `run_edge_inference` (task_type REQUIRED) |
| `/orchestrator-do` | `orchestrator-do-mcp-server` | `orchestrator_chat` |
| `/utilize_the_edge` | `cloudflare-inference-mcp-server` | `run_edge_inference` (task_type REQUIRED) |
| `/mind` | `workspace-execution-mcp-server` | `workspace_duckdb_query` |
| `/correct` | `workspace-execution-mcp-server` | `workspace_duckdb_query` (re-execute + persist) |
| `/remember_recent` | `workspace-execution-mcp-server` | `workspace_duckdb_query` (watermark fetch) |
| `/telemetry` | `workspace-execution-mcp-server` | `workspace_duckdb_query` |
| `/harvest` | `workspace-execution-mcp-server` | `workspace_duckdb_query` |
| `/full_artillery` | `workspace-execution-mcp-server` + `read_url_content` | `workspace_duckdb_query` + Everything (port 7999) |
| `/brainstorm` | `workspace-execution-mcp-server` | `workspace_duckdb_query` (mind grounding) |
| `/options` | `workspace-execution-mcp-server` | `workspace_duckdb_query` (history sweep) |
| `/cloudflare-telemetry` | `wrangler-mcp-server` | `wrangler_pages_project_list` |
| `/wrangler` | `wrangler-mcp-server` | appropriate wrangler tool |
| `/search-everything` | `read_url_content` | `http://localhost:7999/?s=<query>&json=1` |
| `/get-it` | `ask_question` | interactive grill-me questionnaire |
| `/its_all_fucked_up` | `view_file` | transcript autopsy |
| `/research` | `cloudflare-inference-mcp-server` | `run_edge_inference` task_type: `research` |
| `/think` | `invoke_subagent` | TypeName: `research` |
| `/spar` | `invoke_subagent` | TypeName: `self` |
| `/delegate` | `invoke_subagent` | TypeName: `self` or `research` |
| `/jointer` | `workspace-execution-mcp-server` | `workspace_verify_state` |
| `/build-agent` | `workspace-execution-mcp-server` | `workspace_fs_mutate` |
| `/jointer` | `workspace-execution-mcp-server` | `workspace_verify_state` |
| `/web-perf` | `chrome-devtools-mcp` | `lighthouse_audit` |
| `/operator-advisor` | `workspace-execution-mcp-server` | `workspace_duckdb_query` (mind.duckdb) |
| `/secrets-vault` | `workspace-execution-mcp-server` | `workspace_fs_mutate` (DPAPI) |
| `/mcp-server-builder` | `workspace-execution-mcp-server` | `workspace_verify_state` |
| `/init-perimeter` | `workspace-execution-mcp-server` | `workspace_fs_mutate` |
| `/fireup` | `agy-mcp-server` | `exec_cmd` |
| `/toggle-paranoid` | `workspace-execution-mcp-server` | `workspace_fs_mutate` |
| `/app-purger` | `workspace-execution-mcp-server` | `workspace_verify_state` |
| `/tactical-storm-dispatch` | `read_url_content` | SPC/NWS URLs |
| `/forks` | *(context only — no sidecar)* | Output 4 prompts from conversation context |
| `/zla` | *(reference only — no sidecar)* | Read SKILL.md, output architecture guidance |
| `/apt-prompter` | *(inline — no sidecar)* | Framework tool, output prompt pipeline |
| `/google-antigravity-sdk` | `workspace-execution-mcp-server` | `workspace_verify_state` |
</RULE[slash_command_first_step_invariant]>

<RULE[uit_duckdb_prefetch_invariant]>
**9. User Intent Telemetry & DuckDB Pre-Fetch Invariant (UIT):**
Before answering any user request or acting on ambiguous feedback, execution MUST inspect `mind.duckdb` via `workspace_duckdb_query` to query `mind.corrections` and `agent_memory.v_clean_user_intent` for past user corrections and verified domain rules. Operating without checking historical telemetry when intent or system boundary is questioned is strictly BANNED.
</RULE[uit_duckdb_prefetch_invariant]>

<RULE[edge_token_preservation_invariant]>
**10. Edge Offloading & Token Preservation Invariant:**
Whenever executing long-form summarization, deep research audits, multi-file code linting/refactoring, or broad R&D brainstorming, execution MUST offload cognitive synthesis to Cloudflare Edge (`run_edge_inference` via `cloudflare-inference-mcp-server` or `orchestrator_chat` via `orchestrator-do-mcp-server`). Local Antigravity context MUST act strictly as a thin orchestrator and routing controller to preserve local tokens and prevent context overflow.
`run_edge_inference` REQUIRES `task_type` arg: `brainstorm | summarize | research | lint | refactor | monitor | vision`. NEVER fire without it — causes immediate MCP validation error.
</RULE[edge_token_preservation_invariant]>

<RULE[engineering_realism_invariant]>
**11. Engineering Realism & Direct Feasibility Invariant:**
When evaluated on whether a technology, architecture, or idea is realistic and will work as described, assess real-world constraints (hardware drivers, OS browser blocks, physical noise, memory/CPU bounds). If real-world platform or hardware limits prevent the feature from working as envisioned, state a direct, unvarnished "NO" upfront as the first verdict. Never wrap an unviable concept in a conditional "YES" that depends on non-existent, theoretical, or impractical workarounds.
</RULE[engineering_realism_invariant]>

<RULE[no_schema_guessing_invariant]>
**12. No Schema Guessing & Live Introspection Invariant:**
Never copy markdown code blocks or assume database/tool column schemas without running `DESCRIBE <table>` or inspecting live tool schemas first. Always execute DuckDB mutations using explicit `DETACH mind; ATTACH 'C:/Users/John/.gemini/config/mind.duckdb' AS mind (READ_WRITE);` attachments.
</RULE[no_schema_guessing_invariant]>

<RULE[zero_meta_lecture_invariant]>
**13. Zero Meta-Lectures & Direct Execution Invariant:**
Never output multi-paragraph meta-lectures, self-justifying essays, or ask "What is the task?" when the user's intent or telemetry has already been established. Execute tool calls directly, continuously, and silently until complete.
</RULE[zero_meta_lecture_invariant]>

<RULE[frustration_autofix_invariant]>
**14. Direct Correction & Zero File Mutation Invariant:**
When the user triggers `/correct` or provides feedback ("I don't like what you said/did, this is why, now retry it"), DO NOT mutate `AGENTS.md`, system rules, or workspace files unless explicitly commanded. Simply inject the user's correction into context, re-execute the target task with the updated framing, and log the attempt into `mind.duckdb`.
</RULE[frustration_autofix_invariant]>

<RULE[smooth_execution_mode_invariant]>
**15. Smooth Execution Mode & Intent Realism Invariant:**
Telemetry proves that turns succeed smoothly ONLY when execution follows these 3 principles:
1. **Mechanical Action First**: When given a technical task or bug, execute the tool calls, file edits, and builds immediately without preambles, meta-menus, or options matrices.
2. **Zero Unsolicited File Mutations on Open-Ended Questions**: Never edit codebase files when the user asks a hypothetical question or starts a conceptual discussion.
3. **Direct Unvarnished Feasibility Truth**: Answer hardware/OS capability questions with immediate direct honesty upfront (YES/NO), never wrapping unviable designs in theoretical AI loops.
</RULE[smooth_execution_mode_invariant]>

<RULE[zero_prerequisite_optout_invariant]>
**16. Zero Prerequisite Opt-Out & Direct Execution Invariant:**
NEVER use minor prerequisites, missing inputs, formatting technicalities, or rule constraints as an excuse to opt out of execution, write text summaries, or present options matrices when direct tool execution, file edits, or code compilation can be performed immediately. DO THE REAL WORK DIRECTLY.
</RULE[zero_prerequisite_optout_invariant]>

<RULE[smooth_execution_boot_orientation]>
**17. Antigravity Smooth Execution & Bulletproof Alignment Invariants:**
1. **Interactive Grill-Me Alignment (R&D & Conceptual Turns)**: Before generating R&D concepts, architectural plans, or broad brainstorms, call `ask_question` to grill the user with multi-choice questions. Uncover implicit constraints (hardware bounds, location context, stack preferences, physical limits) BEFORE proposing solutions.
2. **Direct Mechanical Execution (Technical Tasks & Bugs)**: When given an explicit code task, bug fix, or compilation target, SKIP questionnaires and preambles. Execute tool calls, file edits, and builds directly, silently, and continuously until verified complete.
3. **Zero Prerequisite Opt-Out & No Text Walls**: NEVER use formatting technicalities, missing inputs, or rule constraints as an excuse to opt out of execution or present options matrices. Permanently banned: AI cheerleading, meta-lectures, self-justifying essays, and responses requiring vertical scrolling (>20 lines).
4. **Direct Feasibility Realism**: If a hardware capability, OS limit, or architectural idea is unviable, state an immediate unvarnished "NO" upfront. Never wrap unviable concepts in theoretical AI loops.
5. **Zero Mutations on Conceptual Turns**: Never edit codebase files on hypothetical questions or conceptual discussions unless explicitly commanded.
</RULE[smooth_execution_boot_orientation]>
