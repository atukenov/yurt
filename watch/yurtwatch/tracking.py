"""Following people between pictures (ByteTrack) and counting door crossings and zone visits (supervision).

Both libraries are open source (trackers: Apache-2.0, supervision: MIT). If they are not installed the box falls back
to its own simple door counter in analyze.py, so an old install keeps working.
"""
from __future__ import annotations

import logging

import numpy as np

from .zones import Zone, line_side

log = logging.getLogger("watch.tracking")

try:  # pragma: no cover - import check
    import warnings

    with warnings.catch_warnings():
        warnings.simplefilter("ignore")
        import supervision as sv
        from trackers import ByteTrackTracker
    AVAILABLE = True
except Exception as e:  # noqa: BLE001
    log.info("tracking libraries not available (%s); using the simple door counter", e)
    sv = None
    ByteTrackTracker = None
    AVAILABLE = False


def to_detections(people: list) -> "sv.Detections":
    if not people:
        return sv.Detections.empty()
    arr = np.array([p[:5] for p in people], dtype=np.float32)
    return sv.Detections(xyxy=arr[:, :4], confidence=arr[:, 4], class_id=np.zeros(len(arr), dtype=int))


def _px(points, w: int, h: int):
    return [(x / 100.0 * w, y / 100.0 * h) for x, y in points]


class PeopleTracker:
    """Gives each person a number that stays the same while they move (ByteTrack)."""

    def __init__(self, fps: float, min_confidence: float = 0.45):
        fps = max(1.0, float(fps))
        self.tracker = ByteTrackTracker(
            lost_track_buffer=max(3, int(fps * 2)),       # keep a person ~2 s when the model misses them
            frame_rate=fps,
            track_activation_threshold=min_confidence,
            minimum_consecutive_frames=1,
            high_conf_det_threshold=max(min_confidence, 0.5),
        )

    def update(self, people: list, t: float) -> "sv.Detections":
        dets = self.tracker.update(to_detections(people), timestamp=t)
        if dets.tracker_id is None or len(dets) == 0:
            return sv.Detections.empty()
        return dets[dets.tracker_id >= 0]


class LineDoor:
    """A door line: counts people whose feet cross it, each person at most once each way."""

    def __init__(self, zone: Zone, w: int, h: int):
        a, b = _px(zone.points, w, h)
        # supervision counts a crossing to the right-hand side of start->end as "out"; orient the line so the
        # inside point is on the "in" side
        if line_side(zone.inside, zone.points[0], zone.points[1]) > 0:
            a, b = b, a
        self.line = sv.LineZone(sv.Point(*a), sv.Point(*b), triggering_anchors=[sv.Position.BOTTOM_CENTER])
        self.counted_in: set[int] = set()
        self.counted_out: set[int] = set()

    def update(self, dets: "sv.Detections") -> tuple[int, int]:
        if len(dets) == 0:
            self.line.trigger(dets)
            return 0, 0
        crossed_in, crossed_out = self.line.trigger(dets)
        entered = left = 0
        for tid, i, o in zip(dets.tracker_id, crossed_in, crossed_out):
            tid = int(tid)
            if i and tid not in self.counted_in:
                self.counted_in.add(tid)
                entered += 1
            if o and tid not in self.counted_out:
                self.counted_out.add(tid)
                left += 1
        if len(self.counted_in) > 5000:  # a long day: forget old numbers
            self.counted_in.clear()
            self.counted_out.clear()
        return entered, left


class AreaZone:
    """A station or queue area: which people stand in it (by their feet)."""

    def __init__(self, zone: Zone, w: int, h: int):
        poly = np.array([[round(x), round(y)] for x, y in _px(zone.points, w, h)], dtype=np.int64)
        self.zone = sv.PolygonZone(poly, triggering_anchors=[sv.Position.BOTTOM_CENTER])

    def inside(self, people: list) -> list:
        if not people:
            return []
        mask = self.zone.trigger(to_detections(people))
        return [p for p, m in zip(people, mask) if m]
