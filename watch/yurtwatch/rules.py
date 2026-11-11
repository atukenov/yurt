"""Turns minute counts into live alerts and the daily report. Pure logic: easy to test without cameras."""
from __future__ import annotations

from collections import defaultdict, deque
from dataclasses import dataclass, field
from datetime import date, datetime, timedelta

from .config import Config
from .schedule import ShiftSlot, on_duty
from .store import Minute
from .texts import t

CAMERA_DOWN_MINUTES = 5


def open_window(cfg: Config, day: date) -> tuple[datetime, datetime]:
    start = datetime.combine(day, cfg.open)
    end = datetime.combine(day, cfg.close)
    if end <= start:
        end += timedelta(days=1)
    return start, end


@dataclass
class Alert:
    at: datetime
    kind: str
    zone: str
    text: str


@dataclass
class AlertEngine:
    cfg: Config
    history: dict[str, deque] = field(default_factory=lambda: defaultdict(lambda: deque(maxlen=24 * 60)))
    fired: set = field(default_factory=set)  # (kind, key) that already fired and wait to reset
    queue_rule: bool = True  # minute-based "empty while guests wait"; off when the live engine (seconds) runs

    def _last(self, zone: str, n: int) -> list[Minute]:
        h = self.history[zone]
        return list(h)[-n:] if len(h) >= n else []

    def _streak_ok(self, mins: list[Minute], n: int) -> bool:
        if len(mins) < n:
            return False
        # the minutes must be consecutive
        return (mins[-1].minute - mins[0].minute) == timedelta(minutes=n - 1)

    def _watched_from_start(self, s: ShiftSlot) -> bool:
        h = self.history[s.station]
        return bool(h) and h[0].minute <= s.start + timedelta(minutes=1)

    def _arrived_or_unknown(self, s: ShiftSlot, now: datetime) -> bool:
        if not self._watched_from_start(s):
            return True  # the program started mid-shift: we can't know, so treat as arrived
        return any(m.covered for m in self.history[s.station] if s.start <= m.minute <= now)

    def on_minutes(self, closed: list[Minute], slots: list[ShiftSlot]) -> list[Alert]:
        if not closed:
            return []
        for m in closed:
            self.history[m.zone].append(m)
        now = max(m.minute for m in closed)
        start, end = open_window(self.cfg, now.date())
        is_open = start <= now < end or any(s.start <= now < s.end for s in slots)
        out: list[Alert] = []
        lang = self.cfg.language
        a = self.cfg.alerts

        # cameras without pictures: say so, so empty minutes are not blamed on staff
        for cam in self.cfg.cameras:
            zones = [z.name for z in self.cfg.zones if z.camera == cam.name]
            if not zones:
                continue
            last = self._last(zones[0], CAMERA_DOWN_MINUTES)
            down = self._streak_ok(last, CAMERA_DOWN_MINUTES) and not any(m.has_data for m in last)
            key = ("camera_down", cam.name)
            if down and is_open and key not in self.fired:
                self.fired.add(key)
                out.append(Alert(now, "camera_down", cam.name, t(lang, "camera_down", camera=cam.name, minutes=CAMERA_DOWN_MINUTES)))
            elif key in self.fired and last and last[-1].has_data:
                self.fired.discard(key)
                out.append(Alert(now, "camera_back", cam.name, t(lang, "camera_back", camera=cam.name)))

        for st in self.cfg.stations:
            hist = self.history[st.name]
            if hist and hist[-1].covered:
                self.fired.discard(("empty_while_queue", st.name))
                self.fired.discard(("empty_on_shift", st.name))
                self.fired.discard(("empty", st.name))
            if hist and not hist[-1].on_phone:
                self.fired.discard(("phone", st.name))
            if hist and not hist[-1].is_still:
                self.fired.discard(("still", st.name))
            names = on_duty(slots, st.name, now)
            duty = t(lang, "on_duty", names=", ".join(names)) if names else ""

            if self.queue_rule and st.queue and is_open and ("empty_while_queue", st.name) not in self.fired:
                n = a.empty_while_queue_minutes
                s_last, q_last = self._last(st.name, n), self._last(st.queue, n)
                if (
                    self._streak_ok(s_last, n)
                    and self._streak_ok(q_last, n)
                    and all(m.has_data and not m.covered for m in s_last)
                    and all(m.has_data and m.avg >= a.queue_min_guests - 0.5 for m in q_last)
                ):
                    self.fired.add(("empty_while_queue", st.name))
                    guests = max(m.people_max for m in q_last)
                    out.append(Alert(now, "empty_while_queue", st.name,
                                     t(lang, "empty_while_queue", station=st.label, minutes=n, guests=guests, duty=duty)))

            present = [x for x in slots if x.station == st.name and x.start <= now < x.end and self._arrived_or_unknown(x, now)]
            if present and ("empty_on_shift", st.name) not in self.fired:
                n = a.empty_on_shift_minutes
                s_last = self._last(st.name, n)
                if self._streak_ok(s_last, n) and all(m.has_data and not m.covered for m in s_last):
                    self.fired.add(("empty_on_shift", st.name))
                    out.append(Alert(now, "empty_on_shift", st.name, t(lang, "empty_on_shift", station=st.label, minutes=n, duty=duty)))

            # no shift plan for this station: still say when it stands empty a long time in open hours
            planned = any(x.station == st.name for x in slots)
            if not planned and is_open and a.empty_minutes > 0 and ("empty", st.name) not in self.fired:
                n = a.empty_minutes
                s_last = self._last(st.name, n)
                if self._streak_ok(s_last, n) and all(m.has_data and not m.covered for m in s_last):
                    self.fired.add(("empty", st.name))
                    out.append(Alert(now, "empty", st.name, t(lang, "empty", station=st.label, minutes=n)))

            if self.cfg.detect.phones and a.phone_minutes > 0 and ("phone", st.name) not in self.fired:
                n = a.phone_minutes
                s_last = self._last(st.name, n)
                if self._streak_ok(s_last, n) and all(m.on_phone for m in s_last):
                    self.fired.add(("phone", st.name))
                    out.append(Alert(now, "phone", st.name, t(lang, "phone", station=st.label, minutes=n, duty=duty)))

            if self.cfg.detect.still and a.still_minutes > 0 and ("still", st.name) not in self.fired:
                n = a.still_minutes
                s_last = self._last(st.name, n)
                if self._streak_ok(s_last, n) and all(m.is_still for m in s_last):
                    self.fired.add(("still", st.name))
                    out.append(Alert(now, "still", st.name, t(lang, "still", station=st.label, minutes=n, duty=duty)))

        # late: the person's station not covered once since their shift began, grace time passed
        for s in slots:
            key = ("late", s)
            due = s.start + timedelta(minutes=a.late_grace_minutes)
            if key in self.fired or not (due <= now < s.end) or not self._watched_from_start(s):
                continue
            seen = [m for m in self.history[s.station] if s.start <= m.minute <= now]
            if any(m.covered for m in seen):
                self.fired.add(key)  # arrived: never alert for this slot
                continue
            if sum(1 for m in seen if m.has_data) < a.late_grace_minutes // 2:
                continue  # camera mostly off: don't blame
            self.fired.add(key)
            st = self.cfg.zone(s.station)
            out.append(Alert(now, "late", s.station, t(lang, "late", name=s.name, station=st.label,
                                                       at=due.strftime("%H:%M"), start=s.start.strftime("%H:%M"))))
        return out


# ---------- daily report ----------

def _runs(flags: list[bool]) -> int:
    best = cur = 0
    for f in flags:
        cur = cur + 1 if f else 0
        best = max(best, cur)
    return best


@dataclass
class StationStats:
    zone: str
    data_minutes: int
    covered_pct: int
    empty_with_queue: int
    queue_max: int
    first: str
    last: str
    phone_minutes: int = 0
    still_minutes: int = 0


@dataclass
class StaffStats:
    slot: ShiftSlot
    data_minutes: int
    covered_pct: int
    empty_minutes: int
    empty_with_queue: int
    longest_empty: int
    late_by: int
    early_by: int
    shared: bool
    seen: bool
    phone_minutes: int = 0
    still_minutes: int = 0


def _index(minutes: list[Minute]) -> dict[str, dict[datetime, Minute]]:
    idx: dict[str, dict[datetime, Minute]] = defaultdict(dict)
    for m in minutes:
        idx[m.zone][m.minute] = m
    return idx


def _steps(a: datetime, b: datetime):
    x = a
    while x < b:
        yield x
        x += timedelta(minutes=1)


def daily_stats(cfg: Config, day: date, minutes: list[Minute], slots: list[ShiftSlot]):
    idx = _index(minutes)
    o_start, o_end = open_window(cfg, day)
    a = cfg.alerts
    stations = []
    for st in cfg.stations:
        mine, q = idx.get(st.name, {}), idx.get(st.queue, {}) if st.queue else {}
        data = [mine[x] for x in _steps(o_start, o_end) if x in mine and mine[x].has_data]
        covered = [m for m in data if m.covered]
        ewq = sum(1 for m in data if not m.covered and m.minute in q and q[m.minute].has_data
                  and q[m.minute].avg >= a.queue_min_guests - 0.5)
        qmax = max((q[x].people_max for x in _steps(o_start, o_end) if x in q), default=0)
        stations.append(StationStats(
            st.name, len(data), round(100 * len(covered) / len(data)) if data else 0, ewq, qmax,
            covered[0].minute.strftime("%H:%M") if covered else "", covered[-1].minute.strftime("%H:%M") if covered else "",
            sum(1 for m in data if m.on_phone), sum(1 for m in data if m.is_still),
        ))

    staff = []
    for s in slots:
        st = cfg.zone(s.station)
        mine, q = idx.get(s.station, {}), idx.get(st.queue, {}) if st.queue else {}
        steps = list(_steps(s.start, s.end))
        data = [mine[x] for x in steps if x in mine and mine[x].has_data]
        empty_flags = [(x in mine and mine[x].has_data and not mine[x].covered) for x in steps]
        covered = [m for m in data if m.covered]
        ewq = sum(1 for m in data if not m.covered and m.minute in q and q[m.minute].has_data
                  and q[m.minute].avg >= a.queue_min_guests - 0.5)
        late = early = 0
        if covered:
            late = int((covered[0].minute - s.start).total_seconds() // 60)
            early = int((s.end - covered[-1].minute).total_seconds() // 60) - 1
        shared = any(o is not s and o.station == s.station and o.start < s.end and s.start < o.end for o in slots)
        staff.append(StaffStats(
            s, len(data), round(100 * len(covered) / len(data)) if data else 0,
            sum(empty_flags), ewq, _runs(empty_flags),
            late if late > a.late_grace_minutes else 0,
            early if early > a.late_grace_minutes else 0,
            shared, bool(covered),
            sum(1 for m in data if m.on_phone), sum(1 for m in data if m.is_still),
        ))
    gap = 0
    for cam in cfg.cameras:
        zones = [z.name for z in cfg.zones if z.camera == cam.name]
        if zones:
            mine = idx.get(zones[0], {})
            gap = max(gap, sum(1 for x in _steps(o_start, o_end) if not (x in mine and mine[x].has_data)))
    return stations, staff, gap


def visitors_by_hour(cfg: Config, day: date, minutes: list[Minute]) -> dict[int, int]:
    """People who walked in through any door line, per hour of the day (local time)."""
    doors = {z.name for z in cfg.zones if z.kind == "door"}
    o_start, o_end = open_window(cfg, day)
    out: dict[int, int] = {}
    for m in minutes:
        if m.zone in doors and o_start <= m.minute < o_end and m.entered:
            out[m.minute.hour] = out.get(m.minute.hour, 0) + m.entered
    return dict(sorted(out.items()))


def _mark(pct: int) -> str:
    return "🟢" if pct >= 90 else ("🟡" if pct >= 75 else "🔴")


def daily_report(cfg: Config, day: date, minutes: list[Minute], slots: list[ShiftSlot], alerts_count: int) -> str:
    lang = cfg.language
    stations, staff, gap = daily_stats(cfg, day, minutes, slots)
    dash = t(lang, "dash")
    lines = [t(lang, "report_title", cafe=cfg.cafe, day=day.strftime("%d.%m.%Y")), ""]
    for s in stations:
        label = cfg.zone(s.zone).label
        if not s.data_minutes:
            lines.append(t(lang, "no_data", station=label))
            continue
        lines.append(_mark(s.covered_pct) + " " + t(lang, "station_line", station=label, covered=s.covered_pct,
                                                    ewq=s.empty_with_queue, qmax=s.queue_max,
                                                    first=s.first or dash, last=s.last or dash))
        extra = []
        if cfg.detect.phones and s.phone_minutes:
            extra.append(t(lang, "phone_part", m=s.phone_minutes))
        if cfg.detect.still and s.still_minutes:
            extra.append(t(lang, "still_part", m=s.still_minutes))
        if extra:
            lines[-1] += ", " + ", ".join(extra)
    if any(z.kind == "door" for z in cfg.zones):
        vh = visitors_by_hour(cfg, day, minutes)
        if vh:
            peak = max(vh, key=vh.get)
            lines.append(t(lang, "visitors", n=sum(vh.values()), hour=f"{peak:02d}:00", peak=vh[peak]))
        else:
            lines.append(t(lang, "visitors_none"))
    if staff:
        lines += ["", t(lang, "staff_title")]
        for s in staff:
            sl = s.slot
            kw = dict(name=sl.name, station=cfg.zone(sl.station).label,
                      start=sl.start.strftime("%H:%M"), end=sl.end.strftime("%H:%M"))
            if s.data_minutes and not s.seen:
                lines.append(t(lang, "never_seen", mark="🔴", **kw))
                continue
            if not s.data_minutes:
                lines.append(t(lang, "staff_no_data", **kw))
                continue
            lines.append(t(
                lang, "staff_line", mark=_mark(s.covered_pct), covered=s.covered_pct, empty=s.empty_minutes,
                ewq=s.empty_with_queue, longest=s.longest_empty,
                late=t(lang, "late_part", m=s.late_by) if s.late_by else "",
                early=t(lang, "early_part", m=s.early_by) if s.early_by else "",
                shared=t(lang, "shared_part") if s.shared else "", **kw))
            extra = []
            if cfg.detect.phones and s.phone_minutes:
                extra.append(t(lang, "phone_part", m=s.phone_minutes))
            if cfg.detect.still and s.still_minutes:
                extra.append(t(lang, "still_part", m=s.still_minutes))
            if extra:
                lines[-1] += ", " + ", ".join(extra)
    lines.append("")
    if gap:
        lines.append(t(lang, "camera_gap", m=gap))
    lines.append(t(lang, "alerts_count", n=alerts_count))
    lines.append(t(lang, "footer"))
    return "\n".join(lines)


def daily_json(cfg: Config, day: date, minutes: list[Minute], slots: list[ShiftSlot], alerts_count: int) -> dict:
    """The same numbers as the text report, for the Yurt admin."""
    stations, staff, gap = daily_stats(cfg, day, minutes, slots)
    return {
        "day": day.isoformat(),
        "stations": [{"zone": s.zone, "label": cfg.zone(s.zone).label, "cameraMinutes": s.data_minutes,
                      "coveredPct": s.covered_pct, "emptyWithQueue": s.empty_with_queue, "queueMax": s.queue_max,
                      "first": s.first, "last": s.last, "phoneMinutes": s.phone_minutes, "stillMinutes": s.still_minutes}
                     for s in stations],
        "staff": [{"name": s.slot.name, "station": s.slot.station, "start": s.slot.start.strftime("%H:%M"),
                   "end": s.slot.end.strftime("%H:%M"), "cameraMinutes": s.data_minutes, "coveredPct": s.covered_pct,
                   "awayMinutes": s.empty_minutes, "awayWithQueue": s.empty_with_queue, "longestAway": s.longest_empty,
                   "lateBy": s.late_by, "leftEarlyBy": s.early_by, "shared": s.shared, "seen": s.seen,
                   "phoneMinutes": s.phone_minutes, "stillMinutes": s.still_minutes} for s in staff],
        "visitorsByHour": {f"{h:02d}": n for h, n in visitors_by_hour(cfg, day, minutes).items()},
        "cameraGapMinutes": gap,
        "alerts": alerts_count,
    }


def staff_csv_rows(cfg: Config, day: date, minutes: list[Minute], slots: list[ShiftSlot]) -> list[list]:
    _, staff, _ = daily_stats(cfg, day, minutes, slots)
    head = ["date", "name", "station", "start", "end", "camera_minutes", "at_station_pct", "away_min",
            "away_with_queue_min", "longest_away_min", "late_min", "left_early_min", "shared", "phone_min", "still_min"]
    rows = [head]
    for s in staff:
        rows.append([day.isoformat(), s.slot.name, s.slot.station, s.slot.start.strftime("%H:%M"), s.slot.end.strftime("%H:%M"),
                     s.data_minutes, s.covered_pct, s.empty_minutes, s.empty_with_queue, s.longest_empty,
                     s.late_by, s.early_by, int(s.shared), s.phone_minutes, s.still_minutes])
    return rows
