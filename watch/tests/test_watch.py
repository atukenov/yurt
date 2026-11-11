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


def mins(zone, start, values, seconds=60, phone=0, still=0):
    """values: per minute, (occupied_seconds, people_max) or None for no camera picture."""
    out = []
    for i, v in enumerate(values):
        at = start + timedelta(minutes=i)
        if v is None:
            out.append(Minute(at, zone, 0, 0, 0, 0))
        else:
            occ, mx = v
            out.append(Minute(at, zone, seconds, occ, occ * mx, mx, min(phone, occ), min(still, occ)))
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


class NewSignalsTests(unittest.TestCase):
    """Visitors at the door, phone in hand, nobody moving, station empty without a shift plan."""

    def test_door_counts_only_inward_crossings_once(self):
        from yurtwatch.analyze import DoorCounter

        door = Zone("door", "door", "c", [(20, 50), (80, 50)], inside=(50, 90))
        dc = DoorCounter(door)
        ins = outs = 0
        # one person walks in (y 30 -> 70), one walks out (y 70 -> 30), one wanders outside the line
        paths = [[(40, 30 + 8 * i), (60, 70 - 8 * i), (10, 20 + i)] for i in range(6)]
        for i, feet in enumerate(paths):
            e, l = dc.update(feet, i * 0.2)
            ins, outs = ins + e, outs + l
        self.assertEqual((ins, outs), (1, 1))
        # same person stepping back and forth on the line counts once
        dc2 = DoorCounter(door)
        total = 0
        for i, y in enumerate([45, 55, 45, 55, 45, 55]):
            total += dc2.update([(50, y)], i * 0.2)[0]
        self.assertEqual(total, 1)

    def test_door_needs_inside_point(self):
        with self.assertRaises(ValueError):
            Zone("door", "door", "c", [(0, 50), (100, 50)])

    def test_analyzer_phone_and_still(self):
        import numpy as np

        from yurtwatch.analyze import CameraAnalyzer

        class FakeDet:
            def __init__(self, people, phones):
                self.people, self.phones = people, phones

            def detect_all(self, frame):
                return self.people, self.phones

            def phones_near(self, frame, person):
                return []

        till = Zone("till", "station", "c", [(0, 50), (50, 50), (50, 100), (0, 100)])
        an = CameraAnalyzer([till])
        frame = np.full((200, 200, 3), 120, np.uint8)
        person = (20, 20, 60, 180, 0.9)
        phone = (35, 80, 45, 95, 0.5)
        s1, _, _ = an.analyze(frame, 0.0, FakeDet([person], [phone]))
        self.assertTrue(s1["till"].phone)
        self.assertFalse(s1["till"].still)  # no earlier picture yet
        s2, _, _ = an.analyze(frame.copy(), 1.0, FakeDet([person], []))
        self.assertTrue(s2["till"].still)
        self.assertFalse(s2["till"].phone)
        moved = frame.copy()
        moved[40:160, 25:55] = 255
        s3, _, _ = an.analyze(moved, 2.0, FakeDet([person], []))
        self.assertFalse(s3["till"].still)

    def test_aggregator_keeps_phone_still_crossings(self):
        from yurtwatch.analyze import ZoneSample

        agg = MinuteAggregator()
        t0 = datetime(2026, 11, 12, 9, 0, 0)
        for sec in range(60):
            agg.add(t0 + timedelta(seconds=sec), {"till": ZoneSample(1, phone=sec < 40, still=sec < 10), "door": ZoneSample()})
        agg.add_crossings(t0 + timedelta(seconds=30), "door", 3, 1)
        m = {x.zone: x for x in agg.add(t0 + timedelta(minutes=1), {})}
        self.assertEqual((m["till"].phone, m["till"].still), (40, 10))
        self.assertTrue(m["till"].on_phone)
        self.assertFalse(m["till"].is_still)
        self.assertEqual((m["door"].entered, m["door"].left), (3, 1))

    def test_phone_still_and_empty_alerts(self):
        c = cfg()
        t0 = datetime(2026, 11, 12, 10, 0)
        data = {
            "bar": mins("bar", t0, [(60, 1)] * 16, phone=50, still=55),
            "till": mins("till", t0, [(0, 0)] * 16),
            "queue": mins("queue", t0, [(0, 0)] * 16),
        }
        alerts = AlertTests.feed(self, AlertEngine(c), data)
        self.assertEqual(sorted(a.kind for a in alerts), ["empty", "phone", "still"])
        # switched off in settings -> no phone / still alerts
        c.detect.phones = c.detect.still = False
        alerts = AlertTests.feed(self, AlertEngine(c), data)
        self.assertEqual([a.kind for a in alerts], ["empty"])

    def test_report_visitors_and_phone(self):
        c = cfg("en")
        c.zones.append(Zone("door", "door", "counter", [(0, 50), (100, 50)], inside=(50, 90)))
        o = datetime(2026, 11, 12, 8, 0)
        door = mins("door", o, [(0, 0)] * 180)
        door[5].entered, door[70].entered, door[75].entered = 2, 3, 4
        minutes = door + mins("bar", o, [(60, 1)] * 180, phone=40) + mins("till", o, [(60, 1)] * 180)
        text = daily_report(c, DAY, minutes, [], 0)
        self.assertIn("Visitors: 9, busiest at 09:00 (7)", text)
        self.assertIn("on the phone 180 min", text)
        from yurtwatch.rules import daily_json

        data = daily_json(c, DAY, minutes, [], 0)
        self.assertEqual(data["visitorsByHour"], {"08": 2, "09": 7})

    def test_apply_remote(self):
        c = cfg()
        problems = config_mod.apply_remote(c, {
            "cafe": "Blue Cafe", "open": "09:00", "close": "21:00", "language": "en",
            "alerts": {"phone_minutes": 7}, "detect": {"phones": False},
            "cameras": [
                {"name": "counter", "zones": [
                    {"name": "till", "kind": "station", "points": [[0, 0], [50, 0], [50, 50]], "queue": "q"},
                    {"name": "door", "kind": "door", "points": [[0, 80], [100, 80]], "inside": [50, 50]},
                ]},
                {"name": "kitchen", "zones": []},
            ],
            "shifts": [{"day": "daily", "name": "A", "station": "till", "start": "09:00", "end": "17:00"},
                       {"day": "daily", "name": "B", "station": "gone", "start": "09:00", "end": "17:00"}],
        })
        self.assertEqual(len(problems), 1)  # kitchen camera is not in this box's config
        self.assertEqual([z.name for z in c.zones], ["till", "door"])
        self.assertIsNone(c.zone("till").queue)  # unknown queue zone dropped
        self.assertEqual(c.alerts.phone_minutes, 7)
        self.assertFalse(c.detect.phones)
        self.assertEqual([r["name"] for r in c.schedule_rows], ["A"])
        self.assertEqual(c.open.hour, 9)


class FakeDet:
    name = "fake"

    def __init__(self, people=(), phones=()):
        self.people, self.phones = list(people), list(phones)

    def detect_all(self, frame):
        return self.people, self.phones

    def phones_near(self, frame, person):
        return []


class EngineTests(unittest.TestCase):
    """Detector choice and hardware setting, without needing the model files."""

    def test_providers(self):
        from yurtwatch.detector import pick_providers

        cpu = ["CPUExecutionProvider"]
        self.assertEqual(pick_providers("auto", cpu), ["CPUExecutionProvider"])
        self.assertEqual(pick_providers("nvidia", cpu), ["CPUExecutionProvider"])  # not installed -> CPU
        got = pick_providers("auto", ["CUDAExecutionProvider", "CPUExecutionProvider"])
        self.assertEqual(got, ["CUDAExecutionProvider", "CPUExecutionProvider"])
        got = pick_providers("intel", ["OpenVINOExecutionProvider", "CPUExecutionProvider"])
        self.assertEqual(got[0][0], "OpenVINOExecutionProvider")
        self.assertEqual(got[-1], "CPUExecutionProvider")
        with self.assertRaises(ValueError):
            pick_providers("gpu", cpu)

    def test_rfdetr_decode(self):
        import numpy as np

        from yurtwatch.detector import RF_PERSON, RF_PHONE, rfdetr_decode

        boxes = np.array([[0.5, 0.5, 0.2, 0.4], [0.1, 0.1, 0.05, 0.05], [0.505, 0.5, 0.2, 0.4]], np.float32)
        logits = np.full((3, 91), -9.0, np.float32)
        logits[0, RF_PERSON] = 3.0      # sure person
        logits[2, RF_PERSON] = 1.0      # the same person again, less sure: removed as a duplicate
        logits[1, RF_PHONE] = 0.0       # 50%: a phone above 0.3
        people = rfdetr_decode(boxes, logits, 200, 100, RF_PERSON, 0.45)
        self.assertEqual(len(people), 1)
        x1, y1, x2, y2, sc = people[0]
        self.assertAlmostEqual(x1, 80, delta=0.5)
        self.assertAlmostEqual(y2, 70, delta=0.5)
        self.assertGreater(sc, 0.9)
        self.assertEqual(len(rfdetr_decode(boxes, logits, 200, 100, RF_PHONE, 0.3)), 1)
        self.assertEqual(rfdetr_decode(boxes, logits[:, :10], 200, 100, RF_PHONE, 0.3), [])

    def test_make_detector_falls_back(self):
        from yurtwatch.detector import make_detector

        missing = HERE / "models" / "nope.onnx"
        with self.assertRaises(FileNotFoundError):
            make_detector("rfdetr", HERE / "models" / "yolox_tiny.onnx", missing, 0.45, 0.3)
        with self.assertRaises(ValueError):
            make_detector("yolo26", missing, missing, 0.45, 0.3)
        yolox = HERE / "models" / "yolox_tiny.onnx"
        if yolox.exists():
            self.assertEqual(make_detector("auto", yolox, missing, 0.45, 0.3, "cpu").name, "yolox")

    def test_config_engine(self):
        c = cfg()
        self.assertEqual((c.engine, c.device), ("auto", "auto"))
        self.assertTrue(str(c.rfdetr_model).endswith("rfdetr_nano.onnx"))


class TrackingTests(unittest.TestCase):
    def setUp(self):
        from yurtwatch import tracking

        if not tracking.AVAILABLE:
            self.skipTest("supervision/trackers not installed")

    def _walk(self, inside, start_y, end_y, frames=12):
        import numpy as np

        from yurtwatch.analyze import CameraAnalyzer

        door = Zone("door", "door", "c", [(10, 50), (90, 50)], inside=inside)
        an = CameraAnalyzer([door], detect_phones=False, detect_still=False, fps=5)
        self.assertTrue(an.tracking)
        frame = np.zeros((200, 200, 3), np.uint8)
        ins = outs = 0
        for i in range(frames):
            y = start_y + (end_y - start_y) * i / (frames - 1)
            s, _, _ = an.analyze(frame, i * 0.2, FakeDet([(90, y - 60, 110, y, 0.9)]))
            ins, outs = ins + s["door"].entered, outs + s["door"].left
        return ins, outs

    def test_door_direction_follows_inside_point(self):
        # walking down the picture (y 40% -> 80%)
        self.assertEqual(self._walk((50, 90), 80, 160), (1, 0))   # inside is below the line: that is coming in
        self.assertEqual(self._walk((50, 10), 80, 160), (0, 1))   # inside is above: that is leaving
        self.assertEqual(self._walk((50, 90), 160, 80), (0, 1))

    def test_line_reversed_points(self):
        import numpy as np

        from yurtwatch.analyze import CameraAnalyzer

        door = Zone("door", "door", "c", [(90, 50), (10, 50)], inside=(50, 90))
        an = CameraAnalyzer([door], detect_phones=False, detect_still=False, fps=5)
        frame = np.zeros((200, 200, 3), np.uint8)
        total = 0
        for i in range(12):
            y = 80 + 80 * i / 11
            total += an.analyze(frame, i * 0.2, FakeDet([(90, y - 60, 110, y, 0.9)]))[0]["door"].entered
        self.assertEqual(total, 1)

    def test_area_zone_counts_by_feet(self):
        import numpy as np

        from yurtwatch.analyze import CameraAnalyzer

        till = Zone("till", "station", "c", [(0, 50), (50, 50), (50, 100), (0, 100)])
        an = CameraAnalyzer([till], detect_phones=False, detect_still=False)
        frame = np.zeros((100, 100, 3), np.uint8)
        s, _, _ = an.analyze(frame, 0.0, FakeDet([(10, 10, 30, 80, 0.9), (60, 10, 90, 80, 0.9)]))
        self.assertEqual(s["till"].people, 1)


def sample(people=0, phone=False, still=False):
    from yurtwatch.analyze import ZoneSample

    return ZoneSample(people=people, phone=phone, still=still)


class LiveTests(unittest.TestCase):
    """Seconds-based: guests waiting with nobody at the station."""

    def setUp(self):
        from yurtwatch.live import LiveEngine

        self.c = cfg()
        self.c.alerts.queue_wait_seconds = 60
        self.c.alerts.away_grace_seconds = 20
        self.c.alerts.queue_min_guests = 1
        self.live = LiveEngine(self.c)
        self.t0 = datetime(2026, 11, 12, 10, 0, 0)

    def run_for(self, start, seconds, bar, queue, slots=()):
        alerts = []
        for i in range(seconds):
            now = start + timedelta(seconds=i)
            alerts += self.live.update(now, {"bar": sample(bar), "till": sample(1), "queue": sample(queue)}, list(slots))
        return alerts

    def test_unattended_queue_fires_once_after_wait(self):
        a = self.run_for(self.t0, 30, bar=1, queue=2)
        self.assertEqual(a, [])
        # barista leaves after second 29: counts as away after the 20 s grace, and the wait is counted from
        # when they were last seen, so the alert comes 60 s after that (second 89), not 60 s after the grace
        a = self.run_for(self.t0 + timedelta(seconds=30), 50, bar=0, queue=2)
        self.assertEqual(a, [])
        a = self.run_for(self.t0 + timedelta(seconds=80), 55, bar=0, queue=2)
        self.assertEqual([x.kind for x in a], ["unattended_queue"])
        self.assertEqual(a[0].zone, "bar")
        self.assertIn("2", a[0].text)
        st = self.live.state(self.t0)
        bar = [s for s in st["stations"] if s["zone"] == "bar"][0]
        self.assertFalse(bar["present"])
        self.assertIsNotNone(bar["unattendedSince"])
        # barista back: resets, a new absence can fire again
        self.assertEqual(self.run_for(self.t0 + timedelta(seconds=135), 5, bar=1, queue=2), [])
        self.assertIsNone([s for s in self.live.state(self.t0)["stations"] if s["zone"] == "bar"][0]["unattendedSince"])
        a = self.run_for(self.t0 + timedelta(seconds=140), 90, bar=0, queue=2)
        self.assertEqual(len(a), 1)

    def test_short_hide_is_not_away(self):
        # barista hidden for 15 s at a time behind the machine: never away, never an alert
        a = []
        for k in range(10):
            a += self.run_for(self.t0 + timedelta(seconds=30 * k), 15, bar=1, queue=3)
            a += self.run_for(self.t0 + timedelta(seconds=30 * k + 15), 15, bar=0, queue=3)
        self.assertEqual(a, [])

    def test_no_guests_no_alert_and_camera_gap(self):
        self.assertEqual(self.run_for(self.t0, 300, bar=0, queue=0), [])
        # camera without pictures: nothing is known, nothing fires
        a = []
        for i in range(300):
            a += self.live.update(self.t0 + timedelta(seconds=i), {"bar": None, "till": None, "queue": None}, [])
        self.assertEqual(a, [])

    def test_live_window_lines_and_picture(self):
        import numpy as np

        from yurtwatch.pictures import draw_live

        self.run_for(self.t0, 30, bar=1, queue=2)
        self.run_for(self.t0 + timedelta(seconds=30), 100, bar=0, queue=2)
        now = self.t0 + timedelta(seconds=129)
        lines = dict((t.split(":")[0], t) for t, _ in self.live.lines(now, "counter"))
        self.assertIn("GUESTS WAIT", lines["Бар"])
        self.assertIn("active", lines["Касса"])
        self.assertIn("2 in queue", lines["Очередь"])
        frame = np.zeros((1080, 1920, 3), np.uint8)
        pic = draw_live(frame, self.c.zones, [(100, 100, 300, 900, 0.9)], [], {"bar": 0}, self.live.lines(now, "counter"))
        self.assertEqual(pic.shape[1], 960)

    def test_visitors_reset_each_day(self):
        self.live.set_visitors(self.t0.date(), 5, 3)
        self.live.add_crossings(self.t0, 1, 0)
        self.assertEqual(self.live.state(self.t0)["visitorsIn"], 6)
        self.live.add_crossings(self.t0 + timedelta(days=1), 1, 1)
        self.assertEqual(self.live.state(self.t0)["visitorsIn"], 1)

    def test_minute_rule_off(self):
        engine = AlertEngine(self.c, queue_rule=False)
        start = datetime.combine(DAY, datetime.min.time()).replace(hour=10)
        closed = mins("bar", start, [(0, 0)] * 10) + mins("till", start, [(60, 1)] * 10) + mins("queue", start, [(60, 3)] * 10)
        out = []
        for i in range(10):
            out += engine.on_minutes([m for m in closed if m.minute == start + timedelta(minutes=i)], [])
        self.assertNotIn("empty_while_queue", [a.kind for a in out])


class AlertPictureTests(unittest.TestCase):
    def test_picture_kept_until_sent(self):
        import numpy as np

        from yurtwatch.pictures import alert_jpeg

        frame = (np.random.default_rng(1).random((1080, 1920, 3)) * 255).astype(np.uint8)
        jpg = alert_jpeg(frame, [(100, 100, 300, 900, 0.9)])
        self.assertTrue(jpg.startswith(b"\xff\xd8"))
        self.assertLess(len(jpg), 200_000)
        with tempfile.TemporaryDirectory() as d:
            st = Store(Path(d) / "w.db")
            st.log_alert(datetime(2026, 11, 12, 10, 0), "unattended_queue", "bar", "x", jpg)
            rows = st.unsent_alerts()
            self.assertEqual(rows[0][5], jpg)
            st.mark_alerts_sent([rows[0][0]])
            self.assertIsNone(st.db.execute("SELECT picture FROM alerts").fetchone()[0])
            st.save([Minute(datetime(2026, 11, 12, 10, 1), "door", 60, 0, 0, 0, entered=4, left=2),
                     Minute(datetime(2026, 11, 13, 10, 1), "door", 60, 0, 0, 0, entered=9, left=9)])
            self.assertEqual(st.visitors(date(2026, 11, 12)), (4, 2))


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
