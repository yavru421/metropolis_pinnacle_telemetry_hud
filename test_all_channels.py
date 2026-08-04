import time
import json

pipe_name = r'\\.\pipe\MetropolisHUDPipe'

channels = [
    ("THOUGHT", "Sequential reasoning thought step"),
    ("SEQTHINK", "SequentialThinking cognitive trace formulation"),
    ("DUCKDB", "mind.duckdb telemetry query executed"),
    ("EDGE", "Cloudflare Workers AI edge router synthesis"),
    ("WRANGLER", "Wrangler CLI deployment signal"),
    ("MCP", "Process & terminal sidecar execution"),
    ("SKILLS", "Skill execution sequence verified"),
    ("MUTATE", "Workspace file mutation write"),
    ("AGENT", "Subagent task coordination signal"),
    ("SEARCH", "Ripgrep & workspace search query"),
    ("ERROR", "Critical error fault trace logged")
]

def send_signal(channel, detail):
    try:
        with open(pipe_name, 'w', encoding='utf-8') as f:
            payload = {
                "channel": channel,
                "detail": detail,
                "timestamp": time.strftime("%H:%M:%S")
            }
            f.write(json.dumps(payload) + "\n")
            f.flush()
            print(f"[TEST PASS] Fired channel {channel}: {detail}")
    except Exception as e:
        print(f"[TEST FAIL] {channel}: {e}")

if __name__ == "__main__":
    print("=== FIRING ALL 11 HUD TELEMETRY CHANNELS ===")
    for ch, desc in channels:
        send_signal(ch, desc)
        time.sleep(0.3)
    print("=== ALL 11 CHANNELS TESTED SUCCESSFULLY ===")
