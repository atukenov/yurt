from __future__ import annotations

import json
import logging
import urllib.parse
import urllib.request
import uuid

log = logging.getLogger("watch.telegram")


def send(token: str, chat_id: str, text: str) -> bool:
    if not token or not chat_id:
        log.info("telegram not set up; message:\n%s", text)
        return False
    data = urllib.parse.urlencode({"chat_id": chat_id, "text": text[:4000], "disable_web_page_preview": "true"}).encode()
    try:
        with urllib.request.urlopen(f"https://api.telegram.org/bot{token}/sendMessage", data=data, timeout=15) as r:
            ok = json.loads(r.read().decode()).get("ok", False)
            if not ok:
                log.warning("telegram refused the message")
            return ok
    except Exception as e:  # noqa: BLE001 - never stop watching because Telegram is down
        log.warning("telegram send failed: %s", e)
        return False


def send_photo(token: str, chat_id: str, text: str, jpeg: bytes) -> bool:
    """A message with a picture; falls back to plain text if the picture is refused."""
    if not token or not chat_id:
        log.info("telegram not set up; message:\n%s", text)
        return False
    boundary = "yurtwatch" + uuid.uuid4().hex
    parts = []
    for name, value in (("chat_id", chat_id), ("caption", text[:1000])):
        parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode())
    parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="photo"; filename="alert.jpg"\r\n'
                 f"Content-Type: image/jpeg\r\n\r\n".encode() + jpeg + b"\r\n")
    parts.append(f"--{boundary}--\r\n".encode())
    req = urllib.request.Request(f"https://api.telegram.org/bot{token}/sendPhoto", data=b"".join(parts), method="POST")
    req.add_header("Content-Type", f"multipart/form-data; boundary={boundary}")
    try:
        with urllib.request.urlopen(req, timeout=20) as r:
            if json.loads(r.read().decode()).get("ok", False):
                return True
    except Exception as e:  # noqa: BLE001
        log.warning("telegram photo failed: %s", e)
    return send(token, chat_id, text)
