from __future__ import annotations

import json
import logging
import urllib.parse
import urllib.request

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
