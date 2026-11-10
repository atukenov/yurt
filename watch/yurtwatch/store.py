"""Minute counts, kept only on the computer in the cafe (SQLite). Frames are never stored here."""
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

KEEP_DAYS = 90


@dataclass
class Minute:
    minute: datetime
    zone: str
    seconds: int
    occupied: int
    people_sum: int
    people_max: int

    @property
    def has_data(self) -> bool:
        return self.seconds >= 20

    @property
    def covered(self) -> bool:
        """Someone was in the zone for at least half of the minute."""
        return self.has_data and self.occupied * 2 >= self.seconds

    @property
    def avg(self) -> float:
        return self.people_sum / self.seconds if self.seconds else 0.0


class MinuteAggregator:
    """Collects one sample per second per zone and closes a minute when the clock moves on."""

    def __init__(self):
        self.current: datetime | None = None
        self.acc: dict[str, list[int]] = {}

    def add(self, now: datetime, counts: dict[str, int | None]) -> list[Minute]:
        """counts[zone] = people, or None when that zone's camera had no picture. Returns minutes that just closed."""
        m = now.replace(second=0, microsecond=0)
        closed = []
        if self.current is not None and m != self.current:
            closed = self.flush()
        self.current = m
        for zone, n in counts.items():
            a = self.acc.setdefault(zone, [0, 0, 0, 0])
            if n is None:
                continue
            a[0] += 1
            a[1] += 1 if n > 0 else 0
            a[2] += n
            a[3] = max(a[3], n)
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

    def save(self, minutes: list[Minute]) -> None:
        self.db.executemany(
            "INSERT OR REPLACE INTO minutes VALUES (?,?,?,?,?,?)",
            [(m.minute.strftime("%Y-%m-%d %H:%M"), m.zone, m.seconds, m.occupied, m.people_sum, m.people_max) for m in minutes],
        )
        self.db.commit()

    def log_alert(self, at: datetime, kind: str, zone: str, text: str) -> None:
        self.db.execute("INSERT INTO alerts VALUES (?,?,?,?)", (at.strftime("%Y-%m-%d %H:%M"), kind, zone, text))
        self.db.commit()

    def minutes(self, start: datetime, end: datetime) -> list[Minute]:
        rows = self.db.execute(
            "SELECT * FROM minutes WHERE minute >= ? AND minute < ? ORDER BY minute",
            (start.strftime("%Y-%m-%d %H:%M"), end.strftime("%Y-%m-%d %H:%M")),
        ).fetchall()
        return [Minute(datetime.strptime(r[0], "%Y-%m-%d %H:%M"), *r[1:]) for r in rows]

    def alerts_between(self, start: datetime, end: datetime) -> list[tuple[str, str, str, str]]:
        return self.db.execute(
            "SELECT * FROM alerts WHERE at >= ? AND at < ? ORDER BY at",
            (start.strftime("%Y-%m-%d %H:%M"), end.strftime("%Y-%m-%d %H:%M")),
        ).fetchall()

    def prune(self, now: datetime) -> None:
        cut = (now - timedelta(days=KEEP_DAYS)).strftime("%Y-%m-%d %H:%M")
        self.db.execute("DELETE FROM minutes WHERE minute < ?", (cut,))
        self.db.execute("DELETE FROM alerts WHERE at < ?", (cut,))
        self.db.commit()
