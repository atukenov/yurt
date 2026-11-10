"""Turns one camera picture into per-zone facts: how many people, is someone holding a phone, is everyone still,
how many people walked in through the door. Pure logic on boxes; the detector is passed in."""
from __future__ import annotations

from dataclasses import dataclass, field

import cv2
import numpy as np

from .zones import Zone, foot_point, in_zone, line_side, segments_cross


@dataclass
class ZoneSample:
    people: int = 0
    phone: bool = False   # someone in the zone holds a phone
    still: bool = False   # people are in the zone and none of them moved since the last look
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
    prev_gray: np.ndarray | None = None
    prev_t: float = 0.0
    doors: dict[str, DoorCounter] = field(default_factory=dict)

    def __post_init__(self):
        self.doors = {z.name: DoorCounter(z) for z in self.zones if z.kind == "door"}

    def analyze(self, frame: np.ndarray, t: float, detector) -> tuple[dict[str, ZoneSample], list, list]:
        h, w = frame.shape[:2]
        people, phones = detector.detect_all(frame)
        gray = _gray(frame) if self.detect_still else None
        use_prev = gray is not None and self.prev_gray is not None and self.prev_gray.shape == gray.shape and t - self.prev_t <= 3
        out: dict[str, ZoneSample] = {}
        for z in self.zones:
            s = ZoneSample()
            if z.kind == "door":
                feet = [foot_point(p, w, h) for p in people]
                s.entered, s.left = self.doors[z.name].update(feet, t)
                out[z.name] = s
                continue
            mine = [p for p in people if in_zone(p, w, h, z)]
            s.people = len(mine)
            if z.kind == "station" and mine:
                if self.detect_phones:
                    found = list(phones)
                    for p in mine:
                        if not _holds_phone(p, found):
                            found += detector.phones_near(frame, p)
                    s.phone = any(_holds_phone(p, found) for p in mine)
                if use_prev:
                    s.still = all(moved_fraction(self.prev_gray, gray, p, w, h) < self.still_threshold for p in mine)
            out[z.name] = s
        if gray is not None:
            self.prev_gray, self.prev_t = gray, t
        return out, people, phones
