from __future__ import annotations

import tomllib
from dataclasses import dataclass, field
from datetime import time
from pathlib import Path
from zoneinfo import ZoneInfo

from .zones import Zone


def _hhmm(text: str) -> time:
    h, m = text.strip().split(":")
    return time(int(h), int(m))


@dataclass
class Camera:
    name: str
    url: str


@dataclass
class Alerts:
    empty_while_queue_minutes: int = 3
    queue_min_guests: int = 2
    empty_on_shift_minutes: int = 10
    late_grace_minutes: int = 10


@dataclass
class Config:
    cafe: str
    tz: ZoneInfo
    open: time
    close: time
    base_dir: Path
    data_dir: Path
    model: Path
    sample_seconds: float
    min_confidence: float
    check_picture_minutes: int
    keep_pictures_days: int
    language: str
    telegram_token: str
    telegram_chat: str
    alerts: Alerts
    cameras: list[Camera]
    zones: list[Zone]
    schedule_file: Path | None = None

    @property
    def stations(self) -> list[Zone]:
        return [z for z in self.zones if z.kind == "station"]

    def zone(self, name: str) -> Zone:
        for z in self.zones:
            if z.name == name:
                return z
        raise KeyError(name)


def load(path: str | Path) -> Config:
    path = Path(path).resolve()
    raw = tomllib.loads(path.read_text(encoding="utf-8"))
    base = path.parent

    def rel(p: str) -> Path:
        q = Path(p)
        return q if q.is_absolute() else base / q

    cameras = [Camera(c["name"], c["url"]) for c in raw.get("cameras", [])]
    zones = [
        Zone(
            name=z["name"],
            kind=z["kind"],
            camera=z["camera"],
            points=[(float(x), float(y)) for x, y in z["points"]],
            queue=z.get("queue"),
            label=z.get("label", ""),
        )
        for z in raw.get("zones", [])
    ]
    names = {c.name for c in cameras}
    znames = {z.name for z in zones}
    for z in zones:
        if z.camera not in names:
            raise ValueError(f"zone {z.name}: camera {z.camera!r} is not in [[cameras]]")
        if z.queue and z.queue not in znames:
            raise ValueError(f"zone {z.name}: queue zone {z.queue!r} does not exist")
    tg = raw.get("telegram", {})
    sched = raw.get("schedule_file")
    return Config(
        cafe=raw.get("cafe", "Cafe"),
        tz=ZoneInfo(raw.get("timezone", "Asia/Almaty")),
        open=_hhmm(raw.get("open", "08:00")),
        close=_hhmm(raw.get("close", "22:00")),
        base_dir=base,
        data_dir=rel(raw.get("data_dir", "data")),
        model=rel(raw.get("model", "models/yolox_tiny.onnx")),
        sample_seconds=float(raw.get("sample_seconds", 1.0)),
        min_confidence=float(raw.get("min_confidence", 0.45)),
        check_picture_minutes=int(raw.get("check_picture_minutes", 30)),
        keep_pictures_days=int(raw.get("keep_pictures_days", 7)),
        language=raw.get("language", "ru"),
        telegram_token=tg.get("bot_token", ""),
        telegram_chat=str(tg.get("chat_id", "")),
        alerts=Alerts(**raw.get("alerts", {})),
        cameras=cameras,
        zones=zones,
        schedule_file=rel(sched) if sched else None,
    )
