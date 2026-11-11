"""Turns one camera picture into per-zone facts: how many people, is someone holding a phone, is someone not moving,
how many people walked in through the door. Pure logic on boxes; the detector is passed in."""
from __future__ import annotations

from dataclasses import dataclass, field

import cv2
import numpy as np

from . import tracking
from .zones import Zone, foot_point, in_zone, line_side, segments_cross


@dataclass
class ZoneSample:
    people: int = 0
    phone: bool = False   # someone in the zone holds a phone
    still: bool = False   # at least one person in the zone has not moved for STILL_AFTER seconds
    entered: int = 0      # door: people who crossed the line inwards
    left: int = 0         # door: people who crossed it outwards


@dataclass
class Track:
    id: int
    pos: tuple[float, float]
    last: float
    counted_in: bool = False   # each followed person counts at most once each way, so
    counted_out: bool = False  # someone shuffling on the line is not counted again and again


@dataclass
class DoorCounter:
    """Follows people near a door line between pictures (5 a second is enough) and counts crossings."""
    zone: Zone
    max_step: float = 12.0   # percent of the picture a person can move between two looks
    forget_after: float = 2.0
    tracks: list[Track] = field(default_factory=list)
    _next: int = 1

    def update(self, feet: list[tuple[float, float]], t: float) -> tuple[int, int]:
        a, b = self.zone.points
        inside = line_side(self.zone.inside, a, b)
        entered = left = 0
        free = list(range(len(feet)))
        # nearest pairs first
        pairs = sorted(
            ((abs(tr.pos[0] - feet[i][0]) + abs(tr.pos[1] - feet[i][1]), k, i) for k, tr in enumerate(self.tracks) for i in free),
        )
        used_t, used_f = set(), set()
        for d, k, i in pairs:
            if d > self.max_step or k in used_t or i in used_f:
                continue
            used_t.add(k)
            used_f.add(i)
            tr = self.tracks[k]
            if segments_cross(tr.pos, feet[i], a, b):
                if line_side(feet[i], a, b) == inside:
                    if not tr.counted_in:
                        entered += 1
                        tr.counted_in = True
                elif not tr.counted_out:
                    left += 1
                    tr.counted_out = True
            tr.pos, tr.last = feet[i], t
        for i, f in enumerate(feet):
            if i not in used_f:
                self.tracks.append(Track(self._next, f, t))
                self._next += 1
        self.tracks = [tr for tr in self.tracks if t - tr.last <= self.forget_after]
        return entered, left


STILL_AFTER = 10.0  # seconds one person must stay unmoved before the zone counts as "someone not moving"


def _iou(a, b) -> float:
    x1, y1, x2, y2 = max(a[0], b[0]), max(a[1], b[1]), min(a[2], b[2]), min(a[3], b[3])
    inter = max(0.0, x2 - x1) * max(0.0, y2 - y1)
    union = (a[2] - a[0]) * (a[3] - a[1]) + (b[2] - b[0]) * (b[3] - b[1]) - inter
    return inter / union if union > 0 else 0.0


@dataclass
class StillPerson:
    box: tuple
    since: float | None  # when this person last moved (None: moved in the last look)


class StillWatch:
    """Follows each person at a station between looks (by box overlap) and remembers how long each one has not
    moved, so one barista asleep in the corner is noticed even while a colleague works next to them."""

    def __init__(self):
        self.people: list[StillPerson] = []

    def update(self, boxes: list, moved: list[bool], prev_t: float, t: float, use_prev: bool) -> bool:
        left = list(self.people)
        now: list[StillPerson] = []
        for box, m in zip(boxes, moved):
            best = max(left, key=lambda o: _iou(o.box, box), default=None)
            if best is not None and _iou(best.box, box) >= 0.3:
                left.remove(best)
                since = best.since
            else:
                since = None
            if not use_prev or m:
                since = None
            elif since is None:
                since = prev_t
            now.append(StillPerson(tuple(box[:4]), since))
        self.people = now
        return any(p.since is not None and t - p.since >= STILL_AFTER for p in now)


def _holds_phone(person, phones) -> bool:
    x1, y1, x2, y2 = person[:4]
    pad = (x2 - x1) * 0.15
    for p in phones:
        cx, cy = (p[0] + p[2]) / 2, (p[1] + p[3]) / 2
        if x1 - pad <= cx <= x2 + pad and y1 <= cy <= y2:
            return True
    return False


def _gray(frame: np.ndarray) -> np.ndarray:
    small = cv2.resize(frame, (320, max(1, int(frame.shape[0] * 320 / frame.shape[1]))))
    return cv2.GaussianBlur(cv2.cvtColor(small, cv2.COLOR_BGR2GRAY), (5, 5), 0)


def moved_fraction(prev: np.ndarray, cur: np.ndarray, box, width: int, height: int) -> float:
    """Share of pixels inside the person's box that changed between two looks (0..1)."""
    sx, sy = cur.shape[1] / width, cur.shape[0] / height
    x1, y1 = int(box[0] * sx), int(box[1] * sy)
    x2, y2 = max(x1 + 1, int(box[2] * sx)), max(y1 + 1, int(box[3] * sy))
    a, b = prev[y1:y2, x1:x2], cur[y1:y2, x1:x2]
    if a.size == 0 or a.shape != b.shape:
        return 1.0
    return float((cv2.absdiff(a, b) > 18).mean())


@dataclass
class CameraAnalyzer:
    zones: list[Zone]
    detect_phones: bool = True
    detect_still: bool = True
    still_threshold: float = 0.01
    fps: float = 1.0
    min_confidence: float = 0.45
    use_tracking: bool = True   # ByteTrack + supervision when installed, else the simple DoorCounter
    prev_gray: np.ndarray | None = None
    prev_t: float = 0.0
    doors: dict[str, DoorCounter] = field(default_factory=dict)

    def __post_init__(self):
        self.doors = {z.name: DoorCounter(z) for z in self.zones if z.kind == "door"}
        self.tracking = self.use_tracking and tracking.AVAILABLE
        self._size: tuple[int, int] | None = None
        self._tracker = None
        self._lines: dict = {}
        self._areas: dict = {}
        self._still: dict[str, StillWatch] = {}

    def _setup(self, w: int, h: int) -> None:
        """Pixel shapes for supervision, made again when the picture size changes."""
        self._size = (w, h)
        self._areas = {z.name: tracking.AreaZone(z, w, h) for z in self.zones if z.kind != "door"}
        self._lines = {z.name: tracking.LineDoor(z, w, h) for z in self.zones if z.kind == "door"}
        self._tracker = tracking.PeopleTracker(self.fps, self.min_confidence) if self._lines else None

    def analyze(self, frame: np.ndarray, t: float, detector) -> tuple[dict[str, ZoneSample], list, list]:
        h, w = frame.shape[:2]
        people, phones = detector.detect_all(frame)
        if self.tracking and self._size != (w, h):
            self._setup(w, h)
        tracked = self._tracker.update(people, t) if self.tracking and self._tracker else None
        gray = _gray(frame) if self.detect_still else None
        use_prev = gray is not None and self.prev_gray is not None and self.prev_gray.shape == gray.shape and t - self.prev_t <= 3
        out: dict[str, ZoneSample] = {}
        for z in self.zones:
            s = ZoneSample()
            if z.kind == "door":
                if tracked is not None:
                    s.entered, s.left = self._lines[z.name].update(tracked)
                else:
                    feet = [foot_point(p, w, h) for p in people]
                    s.entered, s.left = self.doors[z.name].update(feet, t)
                out[z.name] = s
                continue
            mine = self._areas[z.name].inside(people) if self.tracking else [p for p in people if in_zone(p, w, h, z)]
            s.people = len(mine)
            if z.kind == "station" and mine:
                if self.detect_phones:
                    found = list(phones)
                    for p in mine:
                        if not _holds_phone(p, found):
                            found += detector.phones_near(frame, p)
                    s.phone = any(_holds_phone(p, found) for p in mine)
                if gray is not None:
                    moved = [not use_prev or moved_fraction(self.prev_gray, gray, p, w, h) >= self.still_threshold
                             for p in mine]
                    s.still = self._still.setdefault(z.name, StillWatch()).update(mine, moved, self.prev_t, t, use_prev)
            elif z.name in self._still:
                self._still[z.name].people = []
            out[z.name] = s
        if gray is not None:
            self.prev_gray, self.prev_t = gray, t
        return out, people, phones
