"""Minute counts, kept on the computer in the cafe (SQLite) and, when connected, sent to Yurt. Frames are never stored."""
from __future__ import annotations

import sqlite3
from dataclasses import dataclass
from datetime import datetime, timedelta
from pathlib import Path

SCHEMA = """
CREATE TABLE IF NOT EXISTS minutes (
  minute TEXT NOT NULL,          -- local time 'YYYY-MM-DD HH:MM'
  zone TEXT NOT NULL,
  seconds INTEGER NOT NULL,      -- seconds with a camera picture
  occupied INTEGER NOT NULL,     -- seconds with at least one person in the zone
  people_sum INTEGER NOT NULL,   -- sum of counts (average = people_sum / seconds)
  people_max INTEGER NOT NULL,
  PRIMARY KEY (minute, zone)
);
CREATE TABLE IF NOT EXISTS alerts (
  at TEXT NOT NULL, kind TEXT NOT NULL, zone TEXT NOT NULL, text TEXT NOT NULL
);
"""
# columns added after the first pilot version; added in place on old databases
EXTRA_COLUMNS = {
    "phone": "INTEGER NOT NULL DEFAULT 0",    # seconds someone in the zone held a phone
    "still": "INTEGER NOT NULL DEFAULT 0",    # seconds people were in the zone and nobody moved
    "entered": "INTEGER NOT NULL DEFAULT 0",  # door: visitors in
    "left_n": "INTEGER NOT NULL DEFAULT 0",   # door: people out
    "sent": "INTEGER NOT NULL DEFAULT 0",     # 1 = delivered to Yurt
}
ALERT_EXTRA = {"sent": "INTEGER NOT NULL DEFAULT 0"}

KEEP_DAYS = 90


@dataclass
class Minute:
    minute: datetime
    zone: str
    seconds: int
    occupied: int
    people_sum: int
    people_max: int
    phone: int = 0
    still: int = 0
    entered: int = 0
    left: int = 0

    @property
    def has_data(self) -> bool:
        return self.seconds >= 20

    @property
    def covered(self) -> bool:
        """Someone was in the zone for at least half of the minute."""
        return self.has_data and self.occupied * 2 >= self.seconds

    @property
    def on_phone(self) -> bool:
        """Someone held a phone for at least half of the time people were there."""
        return self.covered and self.phone * 2 >= self.occupied

    @property
    def is_still(self) -> bool:
        """Nobody moved for most of the minute (sleeping, or just standing very still)."""
        return self.covered and self.still * 10 >= self.occupied * 8

    @property
    def avg(self) -> float:
        return self.people_sum / self.seconds if self.seconds else 0.0

    def to_json(self) -> dict:
        return {"minute": self.minute.strftime("%Y-%m-%dT%H:%M"), "zone": self.zone, "seconds": self.seconds,
                "occupied": self.occupied, "peopleSum": self.people_sum, "peopleMax": self.people_max,
                "phone": self.phone, "still": self.still, "entered": self.entered, "left": self.left}


class MinuteAggregator:
    """One look per second per zone for presence, plus door crossings as they happen. Closes a minute when the clock moves on."""

    def __init__(self):
        self.current: datetime | None = None
        self.acc: dict[str, list[int]] = {}

    def _roll(self, now: datetime) -> list[Minute]:
        m = now.replace(second=0, microsecond=0)
        closed = []
        if self.current is not None and m != self.current:
            closed = self.flush()
        self.current = m
        return closed

    def _a(self, zone: str) -> list[int]:
        return self.acc.setdefault(zone, [0] * 8)

    def add(self, now: datetime, samples: dict) -> list[Minute]:
        """samples[zone] = ZoneSample, a plain people count, or None when the camera had no picture."""
        closed = self._roll(now)
        for zone, s in samples.items():
            a = self._a(zone)
            if s is None:
                continue
            n = s if isinstance(s, int) else s.people
            a[0] += 1
            a[1] += 1 if n > 0 else 0
            a[2] += n
            a[3] = max(a[3], n)
            if not isinstance(s, int):
                a[4] += 1 if s.phone else 0
                a[5] += 1 if s.still else 0
        return closed

    def add_crossings(self, now: datetime, zone: str, entered: int, left: int) -> list[Minute]:
        closed = self._roll(now)
        a = self._a(zone)
        a[6] += entered
        a[7] += left
        return closed

    def flush(self) -> list[Minute]:
        if self.current is None:
            return []
        out = [Minute(self.current, z, *a) for z, a in self.acc.items()]
        self.acc = {}
        return out


class Store:
    def __init__(self, path: Path):
        path.parent.mkdir(parents=True, exist_ok=True)
        self.db = sqlite3.connect(path, check_same_thread=False)
        self.db.executescript(SCHEMA)
        for table, extra in (("minutes", EXTRA_COLUMNS), ("alerts", ALERT_EXTRA)):
            have = {r[1] for r in self.db.execute(f"PRAGMA table_info({table})")}
            for col, decl in extra.items():
                if col not in have:
                    self.db.execute(f"ALTER TABLE {table} ADD COLUMN {col} {decl}")
        self.db.commit()

    def save(self, minutes: list[Minute]) -> None:
        self.db.executemany(
            "INSERT OR REPLACE INTO minutes (minute, zone, seconds, occupied, people_sum, people_max, phone, still, entered, left_n, sent)"
            " VALUES (?,?,?,?,?,?,?,?,?,?,0)",
            [(m.minute.strftime("%Y-%m-%d %H:%M"), m.zone, m.seconds, m.occupied, m.people_sum, m.people_max,
              m.phone, m.still, m.entered, m.left) for m in minutes],
        )
        self.db.commit()

    def log_alert(self, at: datetime, kind: str, zone: str, text: str) -> None:
        self.db.execute("INSERT INTO alerts (at, kind, zone, text, sent) VALUES (?,?,?,?,0)", (at.strftime("%Y-%m-%d %H:%M"), kind, zone, text))
        self.db.commit()

    _COLS = "minute, zone, seconds, occupied, people_sum, people_max, phone, still, entered, left_n"

    def _rows(self, where: str, args, limit: int = -1) -> list[Minute]:
        rows = self.db.execute(f"SELECT {self._COLS} FROM minutes WHERE {where} ORDER BY minute LIMIT ?", (*args, limit)).fetchall()
        return [Minute(datetime.strptime(r[0], "%Y-%m-%d %H:%M"), *r[1:]) for r in rows]

    def minutes(self, start: datetime, end: datetime) -> list[Minute]:
        return self._rows("minute >= ? AND minute < ?", (start.strftime("%Y-%m-%d %H:%M"), end.strftime("%Y-%m-%d %H:%M")))

    def unsent_minutes(self, limit: int = 500) -> list[Minute]:
        return self._rows("sent = 0", (), limit)

    def mark_minutes_sent(self, minutes: list[Minute]) -> None:
        self.db.executemany("UPDATE minutes SET sent = 1 WHERE minute = ? AND zone = ?",
                            [(m.minute.strftime("%Y-%m-%d %H:%M"), m.zone) for m in minutes])
        self.db.commit()

    def unsent_alerts(self, limit: int = 50) -> list[tuple]:
        return self.db.execute("SELECT rowid, at, kind, zone, text FROM alerts WHERE sent = 0 ORDER BY at LIMIT ?", (limit,)).fetchall()

    def mark_alerts_sent(self, rowids: list[int]) -> None:
        self.db.executemany("UPDATE alerts SET sent = 1 WHERE rowid = ?", [(r,) for r in rowids])
        self.db.commit()

    def alerts_between(self, start: datetime, end: datetime) -> list[tuple[str, str, str, str]]:
        return self.db.execute(
            "SELECT at, kind, zone, text FROM alerts WHERE at >= ? AND at < ? ORDER BY at",
            (start.strftime("%Y-%m-%d %H:%M"), end.strftime("%Y-%m-%d %H:%M")),
        ).fetchall()

    def prune(self, now: datetime) -> None:
        cut = (now - timedelta(days=KEEP_DAYS)).strftime("%Y-%m-%d %H:%M")
        self.db.execute("DELETE FROM minutes WHERE minute < ?", (cut,))
        self.db.execute("DELETE FROM alerts WHERE at < ?", (cut,))
        self.db.commit()
