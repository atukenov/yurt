"""Settings. Standalone: everything comes from config.toml. Connected to Yurt: config.toml holds only the Yurt address,
the box key and the camera addresses (camera passwords never leave the cafe); zones, shifts, opening hours and alert
settings are edited in the Yurt admin and fetched every minute (see yurt.py, apply_remote)."""
from __future__ import annotations

import tomllib
from dataclasses import dataclass, field, fields
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
    fps: float = 1.0  # looks per second; a door camera needs about 5 to follow people across the line


@dataclass
class Alerts:
    empty_while_queue_minutes: int = 3
    queue_min_guests: int = 2
    empty_on_shift_minutes: int = 10
    empty_minutes: int = 15          # station empty this long in open hours, when it has no shift plan
    late_grace_minutes: int = 10
    phone_minutes: int = 5           # phone in hand this long without a break
    still_minutes: int = 10          # nobody at the station moved this long (sleeping?)


@dataclass
class Detect:
    phones: bool = True
    still: bool = True
    still_threshold: float = 0.01    # share of changed pixels below which a person counts as not moving
    phone_confidence: float = 0.3


def _dc(cls, raw: dict | None):
    names = {f.name for f in fields(cls)}
    return cls(**{k: v for k, v in (raw or {}).items() if k in names})


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
    detect: Detect = field(default_factory=Detect)
    schedule_rows: list[dict] | None = None   # from Yurt; replaces schedule_file
    yurt_api: str = ""
    yurt_key: str = ""

    @property
    def stations(self) -> list[Zone]:
        return [z for z in self.zones if z.kind == "station"]

    @property
    def connected(self) -> bool:
        return bool(self.yurt_api and self.yurt_key)

    def zone(self, name: str) -> Zone:
        for z in self.zones:
            if z.name == name:
                return z
        raise KeyError(name)

    def check(self) -> None:
        names = {c.name for c in self.cameras}
        znames = {z.name for z in self.zones}
        for z in self.zones:
            if z.camera not in names:
                raise ValueError(f"zone {z.name}: camera {z.camera!r} is not in [[cameras]]")
            if z.queue and z.queue not in znames:
                raise ValueError(f"zone {z.name}: queue zone {z.queue!r} does not exist")


def zone_from(z: dict) -> Zone:
    inside = z.get("inside")
    return Zone(
        name=z["name"],
        kind=z["kind"],
        camera=z["camera"],
        points=[(float(x), float(y)) for x, y in z["points"]],
        queue=z.get("queue") or None,
        label=z.get("label", "") or "",
        inside=(float(inside[0]), float(inside[1])) if inside else None,
    )


def load(path: str | Path) -> Config:
    path = Path(path).resolve()
    raw = tomllib.loads(path.read_text(encoding="utf-8"))
    base = path.parent

    def rel(p: str) -> Path:
        q = Path(p)
        return q if q.is_absolute() else base / q

    cameras = [Camera(c["name"], c["url"], float(c.get("fps", 1.0))) for c in raw.get("cameras", [])]
    zones = [zone_from(z) for z in raw.get("zones", [])]
    tg = raw.get("telegram", {})
    yurt = raw.get("yurt", {})
    sched = raw.get("schedule_file")
    cfg = Config(
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
        alerts=_dc(Alerts, raw.get("alerts")),
        cameras=cameras,
        zones=zones,
        schedule_file=rel(sched) if sched else None,
        detect=_dc(Detect, raw.get("detect")),
        yurt_api=str(yurt.get("api", "")).rstrip("/"),
        yurt_key=str(yurt.get("key", "")),
    )
    if not cfg.connected:
        cfg.check()
    return cfg


def apply_remote(cfg: Config, remote: dict) -> list[str]:
    """Takes zones, shifts, hours and settings from Yurt. Returns problems (e.g. a zone on a camera this box doesn't have)."""
    problems = []
    cfg.cafe = remote.get("cafe") or cfg.cafe
    if remote.get("timezone"):
        cfg.tz = ZoneInfo(remote["timezone"])
    if remote.get("open") and remote.get("close"):
        cfg.open, cfg.close = _hhmm(remote["open"]), _hhmm(remote["close"])
    cfg.language = remote.get("language") or cfg.language
    cfg.alerts = _dc(Alerts, remote.get("alerts"))
    cfg.detect = _dc(Detect, remote.get("detect"))
    have = {c.name for c in cfg.cameras}
    zones = []
    for cam in remote.get("cameras", []):
        if cam["name"] not in have:
            problems.append(f"camera {cam['name']!r} is set up in Yurt but not in this box's config.toml")
            continue
        for z in cam.get("zones", []):
            try:
                zones.append(zone_from({**z, "camera": cam["name"]}))
            except (ValueError, KeyError, TypeError) as e:
                problems.append(str(e))
    names = {z.name for z in zones}
    for z in zones:
        if z.queue and z.queue not in names:
            z.queue = None
    cfg.zones = zones
    cfg.schedule_rows = [
        {"day": r["day"], "name": r["name"], "station": r["station"], "start": r["start"], "end": r["end"]}
        for r in remote.get("shifts", []) if r.get("station") in {z.name for z in zones if z.kind == "station"}
    ]
    return problems
