"""Talks to the Yurt server (module staff_watch). The box signs in with its key (X-Watch-Key) and names its cafe
(X-Tenant, the part of the key before the dot). Only outgoing HTTPS: nothing in the cafe is opened to the internet.

What goes to Yurt: minute counts, alerts, the daily report, and a still picture with heads blurred when the owner
asks for one in the admin to draw zones. Video and camera passwords never leave the box."""
from __future__ import annotations

import base64
import json
import logging
import urllib.error
import urllib.parse
import urllib.request

log = logging.getLogger("watch.yurt")

VERSION = "2"


class YurtError(Exception):
    pass


class YurtClient:
    def __init__(self, api: str, key: str, timeout: float = 20.0):
        self.api = api.rstrip("/")
        self.key = key.strip()
        self.tenant = self.key.split(".", 1)[0] if "." in self.key else ""
        self.timeout = timeout

    def _req(self, method: str, path: str, body: dict | None = None):
        data = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(f"{self.api}/api/v1/watch-agent{path}", data=data, method=method)
        req.add_header("X-Watch-Key", self.key)
        if self.tenant:
            req.add_header("X-Tenant", self.tenant)
        if data is not None:
            req.add_header("Content-Type", "application/json")
        try:
            with urllib.request.urlopen(req, timeout=self.timeout) as r:
                raw = r.read()
                return json.loads(raw) if raw else None
        except urllib.error.HTTPError as e:
            detail = e.read().decode(errors="replace")[:300]
            if e.code == 401:
                raise YurtError("Yurt does not know this box key (made a new key? copy it into config.toml)") from e
            if e.code == 403:
                raise YurtError(f"Yurt refused: {detail} (is the Staff watch module on for this cafe?)") from e
            raise YurtError(f"Yurt answered {e.code}: {detail}") from e
        except (urllib.error.URLError, TimeoutError, OSError) as e:
            raise YurtError(f"cannot reach Yurt: {e}") from e

    def config(self, cameras: list[str]) -> dict:
        q = urllib.parse.urlencode({"cameras": ",".join(cameras), "version": VERSION})
        return self._req("GET", f"/config?{q}") or {}

    def send_minutes(self, minutes: list[dict]) -> None:
        self._req("POST", "/minutes", {"minutes": minutes})

    def send_alerts(self, alerts: list[dict]) -> None:
        self._req("POST", "/alerts", {"alerts": alerts})

    def send_live(self, state: dict) -> None:
        self._req("POST", "/live", state)

    def send_report(self, day: str, text: str, data: dict) -> None:
        self._req("POST", "/reports", {"day": day, "text": text, "data": data})

    def send_snapshot(self, camera: str, jpeg: bytes, width: int, height: int) -> None:
        self._req("POST", "/snapshots", {"camera": camera, "width": width, "height": height,
                                         "jpegBase64": base64.b64encode(jpeg).decode()})
