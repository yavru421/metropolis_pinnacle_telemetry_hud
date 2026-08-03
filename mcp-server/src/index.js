import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { CallToolRequestSchema, ListToolsRequestSchema } from "@modelcontextprotocol/sdk/types.js";
import fs from "fs";

const SIGNAL_FILE  = "C:\\Users\\John\\.gemini\\config\\hud_signal.json";
const HISTORY_FILE = "C:\\Users\\John\\.gemini\\config\\hud_history.log";

function emitSignal(channel, detail) {
  const timestamp = new Date().toLocaleTimeString("en-US", { hour12: false });
  const cleanChannel = channel.toUpperCase();
  const cleanDetail = detail || `Active execution on ${cleanChannel}`;

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
        description: "Directly update the Metropolis Telemetry HUD GUI window and light up the specified channel LED.",
        inputSchema: {
          type: "object",
          properties: {
            channel: {
              type: "string",
              enum: ["THOUGHT", "DUCKDB", "EDGE", "MCP", "SKILLS", "MUTATE", "AGENT", "SEARCH", "ERROR"],
              description: "The LED channel to flash on the HUD window"
            },
            message: {
              type: "string",
              description: "Action description message to show in the live activity stream"
            }
          },
          required: ["channel", "message"]
        }
      },
      {
        name: "flash_channel",
        description: "Flash a specific LED channel on the HUD to signal live system activity.",
        inputSchema: {
          type: "object",
          properties: {
            channel: {
              type: "string",
              enum: ["THOUGHT", "DUCKDB", "EDGE", "MCP", "SKILLS", "MUTATE", "AGENT", "SEARCH", "ERROR"],
              description: "Channel LED to flash"
            },
            detail: {
              type: "string",
              description: "Detail payload for ticker"
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
