import json
import binascii
import ctypes
from ctypes import wintypes
import urllib.request

class DATA_BLOB(ctypes.Structure):
    _fields_ = [("cbData", wintypes.DWORD), ("pbData", ctypes.POINTER(ctypes.c_byte))]

def decrypt_dpapi(hex_str):
    try:
        encrypted_bytes = binascii.unhexlify(hex_str)
        in_blob = DATA_BLOB(len(encrypted_bytes), (ctypes.c_byte * len(encrypted_bytes))(*encrypted_bytes))
        out_blob = DATA_BLOB()
        if ctypes.windll.crypt32.CryptUnprotectData(ctypes.byref(in_blob), None, None, None, None, 0, ctypes.byref(out_blob)):
            decrypted_bytes = bytes(out_blob.pbData[:out_blob.cbData])
            ctypes.windll.kernel32.LocalFree(out_blob.pbData)
            # Trim null characters or UTF-16 BOM
            val = decrypted_bytes.decode('utf-16le', errors='ignore').rstrip('\x00')
            return val
    except Exception as e:
        print("Decrypt error:", e)
    return None

def main():
    vault_path = r"C:\Users\John\.gemini\config\secrets\vault.dat"
    with open(vault_path, "r", encoding="utf-8-sig") as f:
        vault = json.load(f)

    entry = vault.get("telegram/orchestrator-do-bot", {})
    encrypted_hex = entry.get("value", "")
    token = decrypt_dpapi(encrypted_hex)
    if not token:
        print("[ERROR] Failed to decrypt Telegram bot token.")
        return

    print("[SUCCESS] Decrypted Telegram Bot Token cleanly!")

    # Query active webhook info on Telegram API
    url_webhook = f"https://api.telegram.org/bot{token}/getWebhookInfo"
    try:
        req = urllib.request.urlopen(url_webhook)
        data = json.loads(req.read().decode('utf-8'))
        result = data.get('result', {})
        webhook_url = result.get('url', '')
        pending_count = result.get('pending_update_count', 0)
        print(f"[SUCCESS] Telegram Webhook Query Succeeded!")
        print(f"[PROVED] Live Webhook Target URL: {webhook_url}")
        print(f"[PROVED] Pending Updates: {pending_count}")

        # Emit signal to MetropolisHUD
        sig_file = r"C:\Users\John\.gemini\config\hud_signal.json"
        with open(sig_file, "w", encoding="utf-8") as f:
            json.dump({"timestamp": "05:57:05", "channel": "EDGE", "detail": f"[TELEGRAM VERIFIED] Live Webhook: {webhook_url}"}, f)

    except Exception as e:
        print(f"[ERROR] Webhook query failed: {e}")

if __name__ == "__main__":
    main()
