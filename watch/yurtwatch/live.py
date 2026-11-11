"""What is happening right now, second by second: is each station staffed, how many guests wait, and are guests
waiting with nobody at the station ("unattended queue"). The minute counts in store.py stay for reports; this is for
fast alerts and the live panel in Yurt.

Flicker guards: a barista hidden for a moment behind the coffee machine is not "away" until `away_grace_seconds`
pass, and a waiting queue only resets after it has been clear for a few seconds.
"""
from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime

from .config import Config
from .rules import Alert
from .schedule import ShiftSlot, on_duty
from .texts import t

QUEUE_CLEAR_SECONDS = 5


@dataclass
class StationState:
    present: bool = False
    last_seen: datetime | None = None
    away_since: datetime | None = None
    phone: bool = False
    still: bool = False
    known: bool = False    # the camera gave a picture recently
    unattended_since: datetime | None = None  # guests wait in this station's queue and nobody is here


@dataclass
class QueueState:
    people: int = 0
    waiting_since: datetime | None = None      # guests (>= queue_min_guests) are waiting
    clear_since: datetime | None = None
    known: bool = False


class LiveEngine:
    def __init__(self, cfg: Config):
        self.cfg = cfg
        self.stations: dict[str, StationState] = {}
        self.queues: dict[str, QueueState] = {}
        self.fired: set[str] = set()     # stations whose unattended alert fired and waits for the queue to clear
        self.visitors_in = 0
        self.visitors_out = 0
        self.day = None

    def set_visitors(self, day, entered: int, left: int) -> None:
        self.day, self.visitors_in, self.visitors_out = day, entered, left

    def add_crossings(self, now: datetime, entered: int, left: int) -> None:
        if self.day != now.date():
            self.set_visitors(now.date(), 0, 0)
        self.visitors_in += entered
        self.visitors_out += left

    def update(self, now: datetime, samples: dict, slots: list[ShiftSlot]) -> list[Alert]:
        """samples[zone] = ZoneSample or None (no picture). Called about once a second."""
        a = self.cfg.alerts
        grace = max(0, int(getattr(a, "away_grace_seconds", 20)))
        wait = max(0, int(getattr(a, "queue_wait_seconds", 90)))
        if self.day != now.date():
            self.set_visitors(now.date(), 0, 0)
        names = {z.name for z in self.cfg.zones}
        self.stations = {k: v for k, v in self.stations.items() if k in names}
        self.queues = {k: v for k, v in self.queues.items() if k in names}

        for st in self.cfg.stations:
            s = self.stations.setdefault(st.name, StationState())
            got = samples.get(st.name)
            s.known = got is not None
            if got is None:
                continue
            if got.people > 0:
                s.present, s.last_seen, s.away_since = True, now, None
                s.phone, s.still = got.phone, got.still
            else:
                s.phone = s.still = False
                gone_for = (now - s.last_seen).total_seconds() if s.last_seen else grace
                if gone_for >= grace:
                    if s.present or s.away_since is None:
                        s.away_since = s.last_seen or now
                    s.present = False

        for z in self.cfg.zones:
            if z.kind != "queue":
                continue
            q = self.queues.setdefault(z.name, QueueState())
            got = samples.get(z.name)
            q.known = got is not None
            if got is None:
                continue
            q.people = got.people
            if got.people >= max(1, a.queue_min_guests):
                q.clear_since = None
                q.waiting_since = q.waiting_since or now
            elif q.waiting_since is not None:
                q.clear_since = q.clear_since or now
                if (now - q.clear_since).total_seconds() >= QUEUE_CLEAR_SECONDS:
                    q.waiting_since = q.clear_since = None

        out: list[Alert] = []
        lang = self.cfg.language
        for st in self.cfg.stations:
            if not st.queue:
                continue
            s, q = self.stations.get(st.name), self.queues.get(st.queue)
            if not s or not q or not s.known or not q.known:
                continue
            if q.waiting_since and not s.present:
                start = max(q.waiting_since, s.away_since or q.waiting_since)
                s.unattended_since = s.unattended_since or start
            else:
                s.unattended_since = None
                self.fired.discard(st.name)
                continue
            waited = (now - s.unattended_since).total_seconds()
            if wait and waited >= wait and st.name not in self.fired:
                self.fired.add(st.name)
                people = on_duty(slots, st.name, now)
                duty = t(lang, "on_duty", names=", ".join(people)) if people else ""
                out.append(Alert(now, "unattended_queue", st.name,
                                 t(lang, "unattended_queue", station=st.label, seconds=int(waited), guests=q.people, duty=duty)))
        return out

    def lines(self, now: datetime, camera: str) -> list[tuple[str, tuple[int, int, int]]]:
        """Status text for the live window of one camera (colours are BGR)."""
        green, amber, red, white = (80, 220, 80), (0, 190, 255), (60, 60, 255), (255, 255, 255)

        def clock(since: datetime | None) -> str:
            s = int((now - since).total_seconds()) if since else 0
            return f"{s // 60}:{s % 60:02d}"

        out: list[tuple[str, tuple[int, int, int]]] = []
        for z in self.cfg.zones:
            if z.camera != camera:
                continue
            if z.kind == "station":
                s = self.stations.get(z.name)
                if not s or not s.known:
                    out.append((f"{z.label}: no picture", white))
                elif s.unattended_since:
                    out.append((f"{z.label}: GUESTS WAIT, NOBODY HERE {clock(s.unattended_since)}", red))
                elif s.present:
                    extra = (" phone" if s.phone else "") + (" still" if s.still else "")
                    out.append((f"{z.label}: active{extra}", green))
                else:
                    out.append((f"{z.label}: away {clock(s.away_since)}", amber))
            elif z.kind == "queue":
                q = self.queues.get(z.name)
                waiting = f", waiting {clock(q.waiting_since)}" if q and q.waiting_since else ""
                out.append((f"{z.label}: {q.people if q else 0} in queue{waiting}", white))
            elif z.kind == "door":
                out.append((f"{z.label}: in {self.visitors_in} / out {self.visitors_out} today", white))
        return out

    def state(self, now: datetime) -> dict:
        """What the Yurt live panel shows. Only facts that change rarely, so it can be sent only on change."""

        def iso(d):
            return d.strftime("%Y-%m-%dT%H:%M:%S") if d else None

        stations = []
        for st in self.cfg.stations:
            s = self.stations.get(st.name)
            if not s:
                continue
            stations.append({
                "zone": st.name, "label": st.label, "known": s.known, "present": s.present,
                "awaySince": iso(s.away_since) if not s.present else None,
                "phone": s.phone, "still": s.still,
                "unattendedSince": iso(s.unattended_since),
            })
        queues = [{"zone": z.name, "label": z.label, "known": self.queues[z.name].known, "people": self.queues[z.name].people,
                   "waitingSince": iso(self.queues[z.name].waiting_since)}
                  for z in self.cfg.zones if z.kind == "queue" and z.name in self.queues]
        return {"stations": stations, "queues": queues, "visitorsIn": self.visitors_in, "visitorsOut": self.visitors_out}
