import time
import json

pipe_name = r'\\.\pipe\MetropolisHUDPipe'

def send_pipe_signal(channel, detail):
    try:
        with open(pipe_name, 'w', encoding='utf-8') as f:
            payload = {
                "channel": channel,
                "detail": detail,
                "timestamp": time.strftime("%H:%m:%S")
            }
            f.write(json.dumps(payload) + "\n")
            f.flush()
            print(f"[PIPE TEST SUCCESS] Emitted {channel}: {detail}")
    except Exception as e:
        print(f"[PIPE TEST FALLBACK/NOTICE] {e}")

if __name__ == "__main__":
    print("=== EXECUTING METROPOLIS NAMED PIPE TELEMETRY SIGNAL TEST ===")
    send_pipe_signal("THOUGHT", "Named Pipe IPC signal verification pass 1")
    time.sleep(0.2)
    send_pipe_signal("EDGE", "Cloudflare Workers AI edge router IPC packet acknowledged")
    time.sleep(0.2)
    send_pipe_signal("DUCKDB", "mind.duckdb IPC query stream verified")
