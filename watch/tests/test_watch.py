"""Run from the watch/ folder:  python -m unittest discover -s tests -t ."""
from __future__ import annotations

import tempfile
import unittest
from datetime import date, datetime, timedelta
from pathlib import Path

from yurtwatch import config as config_mod
from yurtwatch.rules import AlertEngine, daily_report, daily_stats
from yurtwatch.schedule import ShiftSlot, on_duty, shifts_for
from yurtwatch.store import Minute, MinuteAggregator, Store
from yurtwatch.zones import Zone, count_in_zones, point_in_polygon

HERE = Path(__file__).resolve().parent.parent
DAY = date(2026, 11, 12)  # a Thursday


def cfg(lang="en"):
    c = config_mod.load(HERE / "config.example.toml")
    c.language = lang
    c.schedule_file = None
    return c


def mins(zone, start, values, seconds=60):
    """values: per minute, (occupied_seconds, people_max) or None for no camera picture."""
    out = []
    for i, v in enumerate(values):
        at = start + timedelta(minutes=i)
        if v is None:
            out.append(Minute(at, zone, 0, 0, 0, 0))
        else:
            occ, mx = v
            out.append(Minute(at, zone, seconds, occ, occ * mx, mx))
    return out


class ZoneTests(unittest.TestCase):
    def test_point_in_polygon(self):
        sq = [(0, 0), (10, 0), (10, 10), (0, 10)]
        self.assertTrue(point_in_polygon(5, 5, sq))
        self.assertFalse(point_in_polygon(15, 5, sq))

    def test_counts_by_feet(self):
        z = Zone("till", "station", "c", [(0, 50), (50, 50), (50, 100), (0, 100)])
        # head is outside the zone, feet inside -> counted
        boxes = [(10, 10, 30, 80), (60, 10, 90, 80)]
        self.assertEqual(count_in_zones(boxes, 100, 100, [z]), {"till": 1})

    def test_bad_zone(self):
        with self.assertRaises(ValueError):
            Zone("x", "kitchen", "c", [(0, 0), (1, 1), (2, 0)])


class AggregatorTests(unittest.TestCase):
    def test_minute_closes(self):
        agg = MinuteAggregator()
        t0 = datetime(2026, 11, 12, 9, 0, 0)
        for s in range(60):
            self.assertEqual(agg.add(t0 + timedelta(seconds=s), {"till": 1 if s < 40 else 0, "queue": None}), [])
        closed = {m.zone: m for m in agg.add(t0 + timedelta(minutes=1), {"till": 0, "queue": 0})}
        self.assertEqual(closed["till"].seconds, 60)
        self.assertEqual(closed["till"].occupied, 40)
        self.assertTrue(closed["till"].covered)
        self.assertFalse(closed["queue"].has_data)

    def test_store_roundtrip(self):
        with tempfile.TemporaryDirectory() as d:
            st = Store(Path(d) / "w.db")
            t0 = datetime(2026, 11, 12, 9, 0)
            st.save(mins("till", t0, [(60, 1), (0, 0)]))
            got = st.minutes(t0, t0 + timedelta(hours=1))
            self.assertEqual([m.covered for m in got], [True, False])


class ScheduleTests(unittest.TestCase):
    def test_dated_row_replaces_weekly(self):
        rows = [
            {"day": "daily", "name": "A", "station": "till", "start": "08:00", "end": "16:00"},
            {"day": "thu", "name": "B", "station": "bar", "start": "16:00", "end": "01:00"},
            {"day": "2026-11-12", "name": "A", "station": "bar", "start": "10:00", "end": "14:00"},
        ]
        slots = shifts_for(rows, DAY, {"till", "bar"})
        self.assertEqual([(s.name, s.station, s.start.hour) for s in slots], [("A", "bar", 10), ("B", "bar", 16)])
        self.assertEqual(slots[1].end, datetime(2026, 11, 13, 1, 0))  # past midnight
        self.assertEqual(on_duty(slots, "bar", datetime(2026, 11, 12, 23, 0)), ["B"])

    def test_unknown_station(self):
        with self.assertRaises(ValueError):
            shifts_for([{"day": "daily", "name": "A", "station": "kitchen", "start": "08:00", "end": "09:00"}], DAY, {"till"})


class AlertTests(unittest.TestCase):
    def feed(self, engine, per_zone, slots=()):
        alerts = []
        n = len(next(iter(per_zone.values())))
        for i in range(n):
            alerts += engine.on_minutes([per_zone[z][i] for z in per_zone], list(slots))
        return alerts

    def test_empty_while_queue_fires_once(self):
        c = cfg()
        t0 = datetime(2026, 11, 12, 9, 0)
        data = {
            "till": mins("till", t0, [(60, 1)] + [(0, 0)] * 6),
            "bar": mins("bar", t0, [(60, 1)] * 7),
            "queue": mins("queue", t0, [(0, 0)] + [(60, 3)] * 6),
        }
        alerts = self.feed(AlertEngine(c), data)
        kinds = [(a.kind, a.zone) for a in alerts]
        self.assertEqual(kinds, [("empty_while_queue", "till")])
        self.assertIn("3 guests", alerts[0].text)

    def test_no_alert_when_quiet(self):
        c = cfg()
        t0 = datetime(2026, 11, 12, 15, 0)
        data = {
            "till": mins("till", t0, [(0, 0)] * 8),
            "bar": mins("bar", t0, [(60, 1)] * 8),
            "queue": mins("queue", t0, [(0, 0)] * 8),
        }
        self.assertEqual(self.feed(AlertEngine(c), data), [])

    def test_empty_on_shift_names_person(self):
        c = cfg()
        t0 = datetime(2026, 11, 12, 15, 0)
        slot = ShiftSlot("Aigerim", "till", datetime(2026, 11, 12, 8), datetime(2026, 11, 12, 16))
        data = {
            "till": mins("till", t0, [(0, 0)] * 10),
            "bar": mins("bar", t0, [(60, 1)] * 10),
            "queue": mins("queue", t0, [(0, 0)] * 10),
        }
        alerts = self.feed(AlertEngine(c), data, [slot])
        self.assertEqual([a.kind for a in alerts], ["empty_on_shift"])
        self.assertIn("Aigerim", alerts[0].text)

    def test_late_and_camera_down(self):
        c = cfg()
        t0 = datetime(2026, 11, 12, 8, 0)
        slot = ShiftSlot("Dan", "bar", t0, t0 + timedelta(hours=8))
        data = {
            "till": mins("till", t0, [(60, 1)] * 11),
            "bar": mins("bar", t0, [(0, 0)] * 11),
            "queue": mins("queue", t0, [(0, 0)] * 11),
        }
        alerts = self.feed(AlertEngine(c), data, [slot])
        self.assertEqual([a.kind for a in alerts], ["late"])
        self.assertIn("08:10", alerts[0].text)

        # camera off: no "late", but a camera alert
        data = {z: mins(z, t0, [None] * 11) for z in ("till", "bar", "queue")}
        alerts = self.feed(AlertEngine(c), data, [slot])
        self.assertEqual([a.kind for a in alerts], ["camera_down"])


class ReportTests(unittest.TestCase):
    def test_staff_numbers(self):
        c = cfg()
        o = datetime(2026, 11, 12, 8, 0)
        n = 14 * 60  # 08:00-22:00
        till = [(0, 0)] * 15 + [(60, 1)] * (n - 15)  # 15 min late
        till[300:320] = [(0, 0)] * 20  # away 20 min at 13:00
        queue = [(0, 0)] * n
        queue[300:310] = [(60, 3)] * 10  # 10 of those minutes with guests waiting
        minutes = mins("till", o, till) + mins("bar", o, [(60, 1)] * n) + mins("queue", o, queue)
        slot = ShiftSlot("Aigerim", "till", o, o + timedelta(hours=8))
        stations, staff, gap = daily_stats(c, DAY, minutes, [slot])
        s = staff[0]
        self.assertEqual(s.late_by, 15)
        self.assertEqual(s.empty_minutes, 35)
        self.assertEqual(s.empty_with_queue, 10)
        self.assertEqual(s.longest_empty, 20)
        self.assertEqual(s.covered_pct, round(100 * (480 - 35) / 480))
        self.assertEqual(gap, 0)
        text = daily_report(c, DAY, minutes, [slot], 2)
        self.assertIn("Aigerim", text)
        self.assertIn("late by 15 min", text)
        ru = daily_report(cfg("ru"), DAY, minutes, [slot], 2)
        self.assertIn("опоздал на 15 мин", ru)


class DetectorTests(unittest.TestCase):
    @unittest.skipUnless((HERE / "models" / "yolox_tiny.onnx").exists(), "model not downloaded")
    def test_finds_a_person(self):
        import cv2
        import numpy as np

        from yurtwatch.detector import PersonDetector

        det = PersonDetector(HERE / "models" / "yolox_tiny.onnx")
        blank = np.full((480, 640, 3), 200, np.uint8)
        self.assertEqual(det.detect(blank), [])
        sample = Path(__file__).with_name("people.jpg")
        if sample.exists():
            self.assertGreaterEqual(len(det.detect(cv2.imread(str(sample)))), 2)


if __name__ == "__main__":
    unittest.main()
