#!/usr/bin/env python3
"""Yurt Watch pilot. See README.md.

  python watch.py download-model          get the person-detection model (once)
  python watch.py snapshot                save one picture per camera with a % grid, to draw zones
  python watch.py check                   show zones + people found right now (heads blurred)
  python watch.py run                     watch all day: alerts + daily report to Telegram
  python watch.py report [--date D] [--send]   build (and send) the report for a day
  python watch.py test-telegram           send a test message
"""
from __future__ import annotations

import argparse
import csv
import logging
import sys
import time
from datetime import date, datetime, timedelta
from pathlib import Path

from yurtwatch import config as config_mod
from yurtwatch import schedule as schedule_mod
from yurtwatch import telegram
from yurtwatch.rules import AlertEngine, daily_report, open_window, staff_csv_rows
from yurtwatch.store import MinuteAggregator, Store
from yurtwatch.zones import count_in_zones

log = logging.getLogger("watch")

REPORT_DELAY_MINUTES = 15


def local_now(cfg) -> datetime:
    return datetime.now(cfg.tz).replace(tzinfo=None)


def load_slots(cfg, day: date):
    if not cfg.schedule_file or not cfg.schedule_file.exists():
        return []
    rows = schedule_mod.load_rows(cfg.schedule_file)
    return schedule_mod.shifts_for(rows, day, {z.name for z in cfg.stations})


def slots_around(cfg, now: datetime):
    """Today's shifts plus yesterday's ones that run past midnight."""
    return [s for s in load_slots(cfg, now.date() - timedelta(days=1)) if s.end > now] + load_slots(cfg, now.date())


def build_report(cfg, store: Store, day: date) -> str:
    o_start, o_end = open_window(cfg, day)
    slots = load_slots(cfg, day)
    start = min([o_start] + [s.start for s in slots])
    end = max([o_end] + [s.end for s in slots])
    minutes = store.minutes(start, end)
    alerts = store.alerts_between(start, end)
    text = daily_report(cfg, day, minutes, slots, len(alerts))
    out = cfg.data_dir / "reports"
    out.mkdir(parents=True, exist_ok=True)
    (out / f"{day.isoformat()}.txt").write_text(text, encoding="utf-8")
    with open(out / f"{day.isoformat()}-staff.csv", "w", newline="", encoding="utf-8-sig") as f:
        csv.writer(f).writerows(staff_csv_rows(cfg, day, minutes, slots))
    return text


def cmd_download_model(cfg, _args):
    from yurtwatch.detector import download_model

    if cfg.model.exists():
        print(f"model already here: {cfg.model}")
        return
    print("downloading YOLOX-Tiny (about 20 MB)...")
    download_model(cfg.model)
    print(f"saved {cfg.model}")


def cmd_snapshot(cfg, _args):
    import cv2

    from yurtwatch.camera import grab_one
    from yurtwatch.pictures import draw_grid

    out = cfg.data_dir / "snapshots"
    out.mkdir(parents=True, exist_ok=True)
    for cam in cfg.cameras:
        f = grab_one(cam.url)
        if f is None:
            print(f"{cam.name}: no picture (check the address, login and that this computer is on the cafe network)")
            continue
        p = out / f"{cam.name}.jpg"
        cv2.imwrite(str(p), draw_grid(f))
        print(f"{cam.name}: {f.shape[1]}x{f.shape[0]} saved to {p}")


def cmd_check(cfg, _args):
    import cv2

    from yurtwatch.camera import grab_one
    from yurtwatch.detector import PersonDetector
    from yurtwatch.pictures import draw_check

    det = PersonDetector(cfg.model, cfg.min_confidence)
    out = cfg.data_dir / "snapshots"
    out.mkdir(parents=True, exist_ok=True)
    for cam in cfg.cameras:
        f = grab_one(cam.url)
        if f is None:
            print(f"{cam.name}: no picture")
            continue
        zones = [z for z in cfg.zones if z.camera == cam.name]
        t0 = time.perf_counter()
        boxes = det.detect(f)
        ms = (time.perf_counter() - t0) * 1000
        counts = count_in_zones(boxes, f.shape[1], f.shape[0], zones)
        p = out / f"{cam.name}-check.jpg"
        cv2.imwrite(str(p), draw_check(f, zones, boxes, counts))
        print(f"{cam.name}: {len(boxes)} people in picture, by zone {counts}, detection {ms:.0f} ms -> {p}")


def cmd_report(cfg, args):
    store = Store(cfg.data_dir / "watch.db")
    day = date.fromisoformat(args.date) if args.date else local_now(cfg).date()
    text = build_report(cfg, store, day)
    print(text)
    if args.send:
        telegram.send(cfg.telegram_token, cfg.telegram_chat, text)


def cmd_test_telegram(cfg, _args):
    ok = telegram.send(cfg.telegram_token, cfg.telegram_chat, f"✅ Yurt Watch: {cfg.cafe} — test message")
    print("sent" if ok else "not sent (check bot_token and chat_id; see the log above)")


def cmd_run(cfg, _args):
    import cv2

    from yurtwatch.camera import CameraReader
    from yurtwatch.detector import PersonDetector
    from yurtwatch.pictures import draw_check, prune

    if not cfg.model.exists():
        sys.exit("model missing: run  python watch.py download-model")
    store = Store(cfg.data_dir / "watch.db")
    det = PersonDetector(cfg.model, cfg.min_confidence)
    readers = {c.name: CameraReader(c.name, c.url) for c in cfg.cameras}
    for r in readers.values():
        r.start()
    agg = MinuteAggregator()
    engine = AlertEngine(cfg)
    sent_file = cfg.data_dir / "reports-sent.txt"
    sent = set(sent_file.read_text().split()) if sent_file.exists() else set()
    last_picture: datetime | None = None
    last_prune: date | None = None
    slots, slots_at = [], None
    log.info("watching %d camera(s), %d zone(s)", len(readers), len(cfg.zones))

    while True:
        tick = time.monotonic()
        now = local_now(cfg)
        counts: dict[str, int | None] = {}
        frames = {}
        for cam in cfg.cameras:
            zones = [z for z in cfg.zones if z.camera == cam.name]
            f = readers[cam.name].latest()
            if f is None:
                counts.update({z.name: None for z in zones})
                continue
            boxes = det.detect(f)
            frames[cam.name] = (f, boxes, zones)
            counts.update(count_in_zones(boxes, f.shape[1], f.shape[0], zones))

        closed = agg.add(now, counts)
        if closed:
            store.save(closed)
            if slots_at is None or now - slots_at >= timedelta(minutes=1):
                try:
                    slots = slots_around(cfg, now)
                except Exception as e:  # noqa: BLE001 - a typo in the schedule must not stop watching
                    log.error("schedule file problem: %s", e)
                slots_at = now
            for a in engine.on_minutes(closed, slots):
                log.info("alert: %s", a.text)
                store.log_alert(a.at, a.kind, a.zone, a.text)
                telegram.send(cfg.telegram_token, cfg.telegram_chat, a.text)
                cam = cfg.zone(a.zone).camera if a.zone in {z.name for z in cfg.zones} else a.zone
                if cam in frames:
                    f, boxes, zones = frames[cam]
                    d = cfg.data_dir / "checks" / now.date().isoformat()
                    d.mkdir(parents=True, exist_ok=True)
                    cv2.imwrite(str(d / f"{now:%H%M}-alert-{a.kind}-{a.zone}.jpg"),
                                draw_check(f, zones, boxes, count_in_zones(boxes, f.shape[1], f.shape[0], zones)))

        # a picture every N minutes, to check by eye that the counting is right
        if frames and (last_picture is None or now - last_picture >= timedelta(minutes=cfg.check_picture_minutes)):
            last_picture = now
            d = cfg.data_dir / "checks" / now.date().isoformat()
            d.mkdir(parents=True, exist_ok=True)
            for name, (f, boxes, zones) in frames.items():
                cv2.imwrite(str(d / f"{now:%H%M}-{name}.jpg"),
                            draw_check(f, zones, boxes, count_in_zones(boxes, f.shape[1], f.shape[0], zones)))

        # daily report shortly after closing (and after the last shift)
        for day in (now.date() - timedelta(days=1), now.date()):
            if day.isoformat() in sent:
                continue
            _, o_end = open_window(cfg, day)
            last_end = max([o_end] + [s.end for s in load_slots(cfg, day)]) if cfg.schedule_file else o_end
            if now >= last_end + timedelta(minutes=REPORT_DELAY_MINUTES) and now - last_end < timedelta(hours=12):
                text = build_report(cfg, store, day)
                telegram.send(cfg.telegram_token, cfg.telegram_chat, text)
                sent.add(day.isoformat())
                sent_file.write_text("\n".join(sorted(sent)))

        if last_prune != now.date():
            last_prune = now.date()
            store.prune(now)
            prune(cfg.data_dir / "checks", now.date(), cfg.keep_pictures_days)

        time.sleep(max(0.0, cfg.sample_seconds - (time.monotonic() - tick)))


def main(argv=None):
    p = argparse.ArgumentParser(description="Yurt Watch pilot")
    p.add_argument("command", choices=["download-model", "snapshot", "check", "run", "report", "test-telegram"])
    p.add_argument("--config", default="config.toml")
    p.add_argument("--date", help="report day, YYYY-MM-DD (default today)")
    p.add_argument("--send", action="store_true", help="also send the report to Telegram")
    args = p.parse_args(argv)
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
    cfg = config_mod.load(args.config)
    {
        "download-model": cmd_download_model,
        "snapshot": cmd_snapshot,
        "check": cmd_check,
        "run": cmd_run,
        "report": cmd_report,
        "test-telegram": cmd_test_telegram,
    }[args.command](cfg, args)


if __name__ == "__main__":
    main()
