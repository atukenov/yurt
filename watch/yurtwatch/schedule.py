"""Who works where and when. The camera never identifies anyone: names come only from this schedule.

CSV columns: day,name,station,start,end
  day   = a date (2026-11-12) or a weekday (mon, tue, ... or "daily")
  start = 08:00, end = 16:00 (an end before the start means the shift runs past midnight)
A dated row replaces the weekday rows for that person on that date.
"""
from __future__ import annotations

import csv
from dataclasses import dataclass
from datetime import date, datetime, time, timedelta
from pathlib import Path

WEEKDAYS = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"]


@dataclass(frozen=True)
class ShiftSlot:
    name: str
    station: str
    start: datetime  # naive local time
    end: datetime


def _t(text: str) -> time:
    h, m = text.strip().split(":")
    return time(int(h), int(m))


def load_rows(path: Path) -> list[dict]:
    with open(path, newline="", encoding="utf-8-sig") as f:
        return [{k.strip().lower(): (v or "").strip() for k, v in row.items()} for row in csv.DictReader(f)]


def shifts_for(rows: list[dict], day: date, stations: set[str] | None = None) -> list[ShiftSlot]:
    iso = day.isoformat()
    wd = WEEKDAYS[day.weekday()]
    dated = [r for r in rows if r["day"] == iso]
    dated_names = {r["name"] for r in dated}
    weekly = [r for r in rows if r["day"].lower() in (wd, "daily") and r["name"] not in dated_names]
    out = []
    for r in dated + weekly:
        if stations is not None and r["station"] not in stations:
            raise ValueError(f"schedule: {r['name']} has station {r['station']!r}, which is not a station zone")
        start = datetime.combine(day, _t(r["start"]))
        end = datetime.combine(day, _t(r["end"]))
        if end <= start:
            end += timedelta(days=1)
        out.append(ShiftSlot(r["name"], r["station"], start, end))
    return sorted(out, key=lambda s: (s.start, s.name))


def on_duty(slots: list[ShiftSlot], station: str, at: datetime) -> list[str]:
    return [s.name for s in slots if s.station == station and s.start <= at < s.end]
