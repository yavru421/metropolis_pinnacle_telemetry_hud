import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { CallToolRequestSchema, ListToolsRequestSchema } from "@modelcontextprotocol/sdk/types.js";
import fs from "fs";

const SIGNAL_FILE  = "C:\\Users\\John\\.gemini\\config\\hud_signal.json";
const HISTORY_FILE = "C:\\Users\\John\\.gemini\\config\\hud_history.log";

let lastSignalMap = new Map();

function emitSignal(channel, detail) {
  const timestamp = new Date().toLocaleTimeString("en-US", { hour12: false });
  const cleanChannel = channel.toUpperCase();
  const cleanDetail = detail || `Active execution on ${cleanChannel}`;

  const now = Date.now();
  const lastTime = lastSignalMap.get(cleanChannel) || 0;
  if (now - lastTime < 200) {
    return;
  }
  lastSignalMap.set(cleanChannel, now);

  const signal = {
    timestamp: timestamp,
    channel: cleanChannel,
    detail: cleanDetail
  };

  const logLine = `[${timestamp}] ${cleanChannel}: ${cleanDetail}\n`;

  try {
    fs.writeFileSync(SIGNAL_FILE, JSON.stringify(signal, null, 2), "utf8");
    fs.appendFileSync(HISTORY_FILE, logLine, "utf8");
    console.error(`[HUD-MCP] Emitted signal: ${cleanChannel} - ${cleanDetail}`);
  } catch (err) {
    console.error(`[HUD-MCP] Failed to emit signal: ${err.message}`);
  }
}

const server = new Server(
  {
    name: "hud-mcp-server",
    version: "1.0.0"
  },
  {
    capabilities: {
      tools: {}
    }
  }
);

server.setRequestHandler(ListToolsRequestSchema, async () => {
  return {
    tools: [
      {
        name: "update_hud",
        description: "Directly update the Metropolis Telemetry HUD GUI window and light up the specified channel LED.\n\nTool Mapping Rule:\n- workspace_duckdb_query -> DUCKDB\n- run_edge_inference / orchestrator_chat -> EDGE\n- view_file / list_dir / grep_search -> MCP\n- write_to_file / multi_replace_file_content / replace_file_content / workspace_fs_mutate -> MUTATE\n- sequentialthinking -> SEQTHINK\n- read_url_content (localhost:7999) -> SEARCH\n- read_url_content (non-local) -> EDGE\n- invoke_subagent -> AGENT\n- workspace_verify_state -> MUTATE\n- wrangler_* -> WRANGLER",
        inputSchema: {
          type: "object",
          properties: {
            channel: {
              type: "string",
              enum: ["THOUGHT", "SEQTHINK", "DUCKDB", "EDGE", "WRANGLER", "MCP", "SKILLS", "MUTATE", "AGENT", "SEARCH", "ERROR"],
              description: "The LED channel to flash on the HUD window"
            },
            message: {
              type: "string",
              description: "Action description message to show in the live activity stream. Required: Pass the actual subject of the call — e.g., 'Querying corrections for last 2 hours', 'Firing run_edge_inference task_type: summarize', 'Writing harvest SKILL.md'. Never use generic descriptions or 'Agent transcript step logged' — this is noise."
            }
          },
          required: ["channel", "message"]
        }
      },
      {
        name: "flash_channel",
        description: "Flash a specific LED channel on the HUD to signal live system activity.\n\nTool Mapping Rule:\n- workspace_duckdb_query -> DUCKDB\n- run_edge_inference / orchestrator_chat -> EDGE\n- view_file / list_dir / grep_search -> MCP\n- write_to_file / multi_replace_file_content / replace_file_content / workspace_fs_mutate -> MUTATE\n- sequentialthinking -> SEQTHINK\n- read_url_content (localhost:7999) -> SEARCH\n- read_url_content (non-local) -> EDGE\n- invoke_subagent -> AGENT\n- workspace_verify_state -> MUTATE\n- wrangler_* -> WRANGLER",
        inputSchema: {
          type: "object",
          properties: {
            channel: {
              type: "string",
              enum: ["THOUGHT", "SEQTHINK", "DUCKDB", "EDGE", "WRANGLER", "MCP", "SKILLS", "MUTATE", "AGENT", "SEARCH", "ERROR"],
              description: "Channel LED to flash"
            },
            detail: {
              type: "string",
              description: "Detail payload for ticker. Required: Pass the actual subject of the call — e.g., 'Querying corrections for last 2 hours', 'Firing run_edge_inference task_type: summarize', 'Writing harvest SKILL.md'. Never use generic descriptions or 'Agent transcript step logged' — this is noise."
            }
          },
          required: ["channel"]
        }
      }
    ]
  };
});

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  if (name === "update_hud" || name === "flash_channel") {
    const channel = args.channel;
    const detail = args.message || args.detail || `Channel ${channel} triggered`;

    emitSignal(channel, detail);

    return {
      content: [
        {
          type: "text",
          text: JSON.stringify({
            success: true,
            channel: channel,
            detail: detail,
            timestamp: new Date().toISOString()
          }, null, 2)
        }
      ]
    };
  }

  throw new Error(`Unknown tool: ${name}`);
});

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error("[HUD-MCP] Consolidated Metropolis HUD MCP Server running on Stdio transport");
}

main().catch((err) => {
  console.error("[HUD-MCP] Fatal error:", err);
  process.exit(1);
});
