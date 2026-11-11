#!/usr/bin/env python3
"""Yurt Watch: camera box for the Yurt module "Staff watch" (or standalone). See README.md.

  python watch.py download-model [--engine rfdetr]   get the detection model (once; RF-DETR is made on this computer)
  python watch.py compare [--video FILE]  RF-DETR vs YOLOX on your own camera: people found and speed
  python watch.py snapshot                save one picture per camera with a % grid (standalone zone setup)
  python watch.py check                   show zones + people/phones found right now (heads blurred)
  python watch.py connect                 check the Yurt connection and show the settings it sends
  python watch.py run [--show]            watch all day: alerts + daily report (--show: live window per camera)
  python watch.py report [--date D] [--send]   build (and send) the report for a day
  python watch.py test-telegram           send a test message (standalone)
"""
from __future__ import annotations

import argparse
import csv
import logging
import os
import sys
import time
from datetime import date, datetime, timedelta
from pathlib import Path

from yurtwatch import config as config_mod
from yurtwatch import schedule as schedule_mod
from yurtwatch import telegram
from yurtwatch.rules import AlertEngine, daily_json, daily_report, open_window, staff_csv_rows
from yurtwatch.store import MinuteAggregator, Store

log = logging.getLogger("watch")

REPORT_DELAY_MINUTES = 15
CONFIG_EVERY = 60      # seconds between settings fetches from Yurt
UPLOAD_EVERY = 60      # seconds between sending minute counts to Yurt (alerts go out at once)
LIVE_MIN_GAP = 10      # live panel: at most one update every N seconds, and only when something changed


def local_now(cfg) -> datetime:
    return datetime.now(cfg.tz).replace(tzinfo=None)


def load_slots(cfg, day: date):
    stations = {z.name for z in cfg.stations}
    if cfg.schedule_rows is not None:
        return schedule_mod.shifts_for(cfg.schedule_rows, day, stations)
    if not cfg.schedule_file or not cfg.schedule_file.exists():
        return []
    return schedule_mod.shifts_for(schedule_mod.load_rows(cfg.schedule_file), day, stations)


def slots_around(cfg, now: datetime):
    """Today's shifts plus yesterday's ones that run past midnight."""
    return [s for s in load_slots(cfg, now.date() - timedelta(days=1)) if s.end > now] + load_slots(cfg, now.date())


def report_window(cfg, day: date):
    o_start, o_end = open_window(cfg, day)
    slots = load_slots(cfg, day)
    return slots, min([o_start] + [s.start for s in slots]), max([o_end] + [s.end for s in slots])


def build_report(cfg, store: Store, day: date) -> tuple[str, dict]:
    slots, start, end = report_window(cfg, day)
    minutes = store.minutes(start, end)
    n_alerts = len(store.alerts_between(start, end))
    text = daily_report(cfg, day, minutes, slots, n_alerts)
    data = daily_json(cfg, day, minutes, slots, n_alerts)
    out = cfg.data_dir / "reports"
    out.mkdir(parents=True, exist_ok=True)
    (out / f"{day.isoformat()}.txt").write_text(text, encoding="utf-8")
    with open(out / f"{day.isoformat()}-staff.csv", "w", newline="", encoding="utf-8-sig") as f:
        csv.writer(f).writerows(staff_csv_rows(cfg, day, minutes, slots))
    return text, data


def client_for(cfg):
    from yurtwatch.yurt import YurtClient

    return YurtClient(cfg.yurt_api, cfg.yurt_key) if cfg.connected else None


def sync_config(cfg, client) -> list[str]:
    remote = client.config([c.name for c in cfg.cameras])
    problems = config_mod.apply_remote(cfg, remote)
    for p in problems:
        log.warning("settings from Yurt: %s", p)
    return remote.get("snapshotRequests", []) or []


def make_detector(cfg):
    from yurtwatch.detector import make_detector as make

    det = make(cfg.engine, cfg.model, cfg.rfdetr_model, cfg.min_confidence, cfg.detect.phone_confidence, cfg.device)
    log.info("detector: %s", det.name)
    return det


def cmd_download_model(cfg, args):
    from yurtwatch.detector import download_model

    if getattr(args, "engine", None) == "rfdetr":
        from yurtwatch.export_rfdetr import export

        if cfg.rfdetr_model.exists():
            print(f"model already here: {cfg.rfdetr_model}")
            return
        print("RF-DETR Nano is made on this computer once: about 1 GB of tools is downloaded into a separate folder,")
        print("used for a few minutes and deleted. The camera box itself stays small.")
        export(cfg.rfdetr_model)
        return
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

    from yurtwatch.analyze import CameraAnalyzer
    from yurtwatch.camera import grab_one
    from yurtwatch.pictures import draw_check

    client = client_for(cfg)
    if client:
        sync_config(cfg, client)
    det = make_detector(cfg)
    out = cfg.data_dir / "snapshots"
    out.mkdir(parents=True, exist_ok=True)
    for cam in cfg.cameras:
        f = grab_one(cam.url)
        if f is None:
            print(f"{cam.name}: no picture")
            continue
        zones = [z for z in cfg.zones if z.camera == cam.name]
        an = CameraAnalyzer(zones, cfg.detect.phones, False)
        t0 = time.perf_counter()
        samples, people, phones = an.analyze(f, 0.0, det)
        ms = (time.perf_counter() - t0) * 1000
        p = out / f"{cam.name}-check.jpg"
        cv2.imwrite(str(p), draw_check(f, zones, people, {k: v.people for k, v in samples.items()}, phones))
        found = {k: v.people for k, v in samples.items() if zones and cfg.zone(k).kind != "door"}
        on_phone = [k for k, v in samples.items() if v.phone]
        print(f"{cam.name}: {len(people)} people, {len(phones)} phones in picture, by zone {found}"
              f"{', phone at ' + ', '.join(on_phone) if on_phone else ''}, {ms:.0f} ms -> {p}")


def cmd_compare(cfg, args):
    """Runs both detectors on the same pictures, so you can see which finds your staff and guests better."""
    import cv2
    import numpy as np

    from yurtwatch.camera import grab_one
    from yurtwatch.detector import RfDetrDetector, YoloxDetector
    from yurtwatch.pictures import blur_heads

    missing = [str(p) for p in (cfg.model, cfg.rfdetr_model) if not p.exists()]
    if missing:
        sys.exit("missing " + ", ".join(missing) + ": run  python watch.py download-model  and  python watch.py download-model --engine rfdetr")
    dets = [YoloxDetector(cfg.model, cfg.min_confidence, cfg.detect.phone_confidence, device=cfg.device),
            RfDetrDetector(cfg.rfdetr_model, cfg.min_confidence, cfg.detect.phone_confidence, device=cfg.device)]
    frames: list[tuple[str, object]] = []
    if args.video:
        cap = cv2.VideoCapture(args.video)
        total = int(cap.get(cv2.CAP_PROP_FRAME_COUNT)) or 300
        for i in range(10):
            cap.set(cv2.CAP_PROP_POS_FRAMES, int(total * i / 10))
            ok, f = cap.read()
            if ok:
                frames.append((f"video-{i}", f))
        cap.release()
    else:
        for cam in cfg.cameras:
            f = grab_one(cam.url)
            if f is not None:
                frames.append((cam.name, f))
    if not frames:
        sys.exit("no pictures (check the camera addresses, or pass --video FILE)")
    out = cfg.data_dir / "compare"
    out.mkdir(parents=True, exist_ok=True)
    totals = {d.name: [0, 0, 0.0] for d in dets}
    for name, f in frames:
        sides = []
        line = []
        for d in dets:
            d.detect_all(f)  # warm up, so the first run's start-up time is not counted
            t0 = time.perf_counter()
            people, phones = d.detect_all(f)
            ms = (time.perf_counter() - t0) * 1000
            totals[d.name][0] += len(people)
            totals[d.name][1] += len(phones)
            totals[d.name][2] += ms
            pic = blur_heads(f, people)
            for b in people:
                cv2.rectangle(pic, (int(b[0]), int(b[1])), (int(b[2]), int(b[3])), (0, 200, 0), 2)
            for b in phones:
                cv2.rectangle(pic, (int(b[0]), int(b[1])), (int(b[2]), int(b[3])), (0, 0, 255), 2)
            cv2.putText(pic, f"{d.name}: {len(people)} people, {ms:.0f} ms", (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (255, 255, 255), 2)
            sides.append(pic)
            line.append(f"{d.name} {len(people)} people / {len(phones)} phones / {ms:.0f} ms")
        p = out / f"{name}.jpg"
        cv2.imwrite(str(p), np.hstack(sides))
        print(f"{name}: " + "  |  ".join(line) + f"  -> {p}")
    n = len(frames)
    print("\nOn average per picture:")
    for k, (pe, ph, ms) in totals.items():
        print(f"  {k:7s} {pe / n:.1f} people, {ph / n:.1f} phones, {ms / n:.0f} ms")
    print("Look at the pictures: the better engine finds everyone behind the counter and in the queue without extra boxes.")
    print("Then set  engine = \"rfdetr\"  or  engine = \"yolox\"  in config.toml (auto picks RF-DETR when its file is there).")


def cmd_connect(cfg, _args):
    client = client_for(cfg)
    if not client:
        sys.exit("No [yurt] api and key in config.toml: this box runs standalone.")
    sync_config(cfg, client)
    print(f"Connected to {cfg.yurt_api} as cafe '{client.tenant}': {cfg.cafe}, open {cfg.open:%H:%M}-{cfg.close:%H:%M}")
    for z in cfg.zones:
        print(f"  zone {z.name} ({z.kind}) on camera {z.camera}")
    if not cfg.zones:
        print("  no zones yet: in Yurt admin open Staff watch → Cameras, take a picture and draw them")
    print(f"  shift plan rows: {len(cfg.schedule_rows or [])}")


def cmd_report(cfg, args):
    client = client_for(cfg)
    if client:
        sync_config(cfg, client)
    store = Store(cfg.data_dir / "watch.db")
    day = date.fromisoformat(args.date) if args.date else local_now(cfg).date()
    text, data = build_report(cfg, store, day)
    print(text)
    if args.send:
        if client:
            client.send_report(day.isoformat(), text, data)
        else:
            telegram.send(cfg.telegram_token, cfg.telegram_chat, text)


def cmd_test_telegram(cfg, _args):
    ok = telegram.send(cfg.telegram_token, cfg.telegram_chat, f"✅ Yurt Watch: {cfg.cafe} — test message")
    print("sent" if ok else "not sent (check bot_token and chat_id; see the log above)")


def cmd_run(cfg, args):
    import cv2

    import base64

    from yurtwatch.analyze import CameraAnalyzer
    from yurtwatch.camera import CameraReader
    from yurtwatch.live import LiveEngine
    from yurtwatch.pictures import alert_jpeg, blur_heads, draw_check, draw_live, prune
    from yurtwatch.yurt import YurtError

    client = client_for(cfg)
    snapshot_requests: list[str] = []
    if client:
        while True:
            try:
                snapshot_requests = sync_config(cfg, client)
                break
            except YurtError as e:
                log.error("%s; trying again in 30 s", e)
                time.sleep(30)
    store = Store(cfg.data_dir / "watch.db")
    try:
        det = make_detector(cfg)
    except FileNotFoundError as e:
        sys.exit(str(e))
    readers = {c.name: CameraReader(c.name, c.url) for c in cfg.cameras}
    for r in readers.values():
        r.start()

    def make_analyzers():
        return {c.name: CameraAnalyzer([z for z in cfg.zones if z.camera == c.name], cfg.detect.phones,
                                       cfg.detect.still, cfg.detect.still_threshold, fps=c.fps,
                                       min_confidence=cfg.min_confidence) for c in cfg.cameras}

    analyzers = make_analyzers()
    zones_sig = repr([(z.name, z.points, z.inside) for z in cfg.zones])
    last_look = {c.name: 0.0 for c in cfg.cameras}
    latest: dict[str, tuple] = {}   # camera -> (time, frame, people, phones, samples)
    agg = MinuteAggregator()
    engine = AlertEngine(cfg, queue_rule=False)  # the live engine below watches waiting guests by the second
    live = LiveEngine(cfg)
    live.set_visitors(local_now(cfg).date(), *store.visitors(local_now(cfg).date()))
    live_sent: dict | None = None
    show = bool(getattr(args, "show", False))
    if show and sys.platform.startswith("linux") and not (os.environ.get("DISPLAY") or os.environ.get("WAYLAND_DISPLAY")):
        log.warning("--show needs a screen; this computer has none, so no live windows")
        show = False
    if show:
        log.info("live windows open (q or Esc closes them); heads are blurred, nothing is saved or sent")
    last_live = 0.0
    alerts_waiting = False
    sent_file = cfg.data_dir / "reports-sent.txt"
    sent = set(sent_file.read_text().split()) if sent_file.exists() else set()
    last_picture: datetime | None = None
    last_prune: date | None = None
    last_second = 0.0
    last_config = last_upload = time.monotonic()
    slots, slots_at = [], None
    tick = 1.0 / max([1.0] + [c.fps for c in cfg.cameras])
    log.info("watching %d camera(s), %d zone(s)%s", len(readers), len(cfg.zones), ", connected to Yurt" if client else "")

    def save_picture(name: str, cam: str, now: datetime):
        if cam not in latest:
            return
        _, f, people, phones, samples = latest[cam]
        zones = [z for z in cfg.zones if z.camera == cam]
        d = cfg.data_dir / "checks" / now.date().isoformat()
        d.mkdir(parents=True, exist_ok=True)
        cv2.imwrite(str(d / f"{now:%H%M}-{name}.jpg"), draw_check(f, zones, people, {k: v.people for k, v in samples.items()}, phones))

    def raise_alert(a, now, picture: bytes | None = None):
        nonlocal alerts_waiting
        log.info("alert: %s", a.text)
        store.log_alert(a.at, a.kind, a.zone, a.text, picture)
        alerts_waiting = True
        if not client:
            if picture:
                telegram.send_photo(cfg.telegram_token, cfg.telegram_chat, a.text, picture)
            else:
                telegram.send(cfg.telegram_token, cfg.telegram_chat, a.text)
        cam = cfg.zone(a.zone).camera if a.zone in {z.name for z in cfg.zones} else a.zone
        save_picture(f"alert-{a.kind}-{a.zone}", cam, now)

    def handle_closed(closed, now):
        nonlocal slots, slots_at
        if not closed:
            return
        store.save(closed)
        if slots_at is None or now - slots_at >= timedelta(minutes=1):
            try:
                slots = slots_around(cfg, now)
            except Exception as e:  # noqa: BLE001 - a typo in the shift plan must not stop watching
                log.error("shift plan problem: %s", e)
            slots_at = now
        for a in engine.on_minutes(closed, slots):
            raise_alert(a, now)

    while True:
        t = time.monotonic()
        now = local_now(cfg)

        # look at each camera at its own pace
        for cam in cfg.cameras:
            if t - last_look[cam.name] < 1.0 / max(cam.fps, 0.1) - 0.01:
                continue
            last_look[cam.name] = t
            f = readers[cam.name].latest()
            if f is None:
                latest.pop(cam.name, None)
                continue
            samples, people, phones = analyzers[cam.name].analyze(f, t, det)
            latest[cam.name] = (t, f, people, phones, samples)
            if show:
                zones = [z for z in cfg.zones if z.camera == cam.name]
                cv2.imshow(f"Yurt Watch: {cam.name}", draw_live(f, zones, people, phones,
                                                                {k: v.people for k, v in samples.items()}, live.lines(now, cam.name)))
            for zname, s in samples.items():
                if s.entered or s.left:
                    live.add_crossings(now, s.entered, s.left)
                    handle_closed(agg.add_crossings(now, zname, s.entered, s.left), now)

        if show and cv2.waitKey(1) & 0xFF in (ord("q"), 27):  # q or Esc closes the windows, watching goes on
            cv2.destroyAllWindows()
            show = False
            log.info("live windows closed; still watching")

        # one presence sample per second per zone
        if t - last_second >= cfg.sample_seconds:
            last_second = t
            samples = {}
            for cam in cfg.cameras:
                zones = [z for z in cfg.zones if z.camera == cam.name]
                got = latest.get(cam.name)
                fresh = got is not None and t - got[0] <= 5
                for z in zones:
                    samples[z.name] = got[4].get(z.name) if fresh else None
            handle_closed(agg.add(now, samples), now)
            for a in live.update(now, samples, slots):
                st = cfg.zone(a.zone)
                cam = cfg.zone(st.queue).camera if st.queue else st.camera  # the picture shows the waiting guests
                got = latest.get(cam)
                raise_alert(a, now, alert_jpeg(got[1], got[2]) if got else None)

        # a check picture every N minutes, to verify the counting by eye
        if latest and (last_picture is None or now - last_picture >= timedelta(minutes=cfg.check_picture_minutes)):
            last_picture = now
            for cam in list(latest):
                save_picture(cam, cam, now)

        if client and t - last_config >= CONFIG_EVERY:
            last_config = t
            try:
                snapshot_requests = sync_config(cfg, client)
                sig = repr([(z.name, z.points, z.inside) for z in cfg.zones])
                if sig != zones_sig:
                    zones_sig = sig
                    analyzers = make_analyzers()
                    log.info("zones changed in Yurt: now %d zone(s)", len(cfg.zones))
            except YurtError as e:
                log.warning("%s", e)
        if client and snapshot_requests:
            for name in list(snapshot_requests):
                got = latest.get(name)
                frame = readers[name].latest() if name in readers else None
                if frame is None:
                    continue
                people = got[2] if got else det.detect(frame)
                pic = blur_heads(frame, people)
                if pic.shape[1] > 1280:
                    pic = cv2.resize(pic, (1280, int(pic.shape[0] * 1280 / pic.shape[1])))
                ok, jpg = cv2.imencode(".jpg", pic, [cv2.IMWRITE_JPEG_QUALITY, 80])
                try:
                    client.send_snapshot(name, jpg.tobytes(), frame.shape[1], frame.shape[0])
                    snapshot_requests.remove(name)
                    log.info("sent a picture of camera %s to Yurt", name)
                except YurtError as e:
                    log.warning("%s", e)
                    break
        # alerts go out at once; minute counts once a minute (fewer, bigger requests keep the server bill small)
        upload_due = t - last_upload >= UPLOAD_EVERY
        if client and (upload_due or alerts_waiting):
            if upload_due:
                last_upload = t
            try:
                al = store.unsent_alerts(20)
                if al:
                    client.send_alerts([{"at": a[1].replace(" ", "T"), "kind": a[2], "zone": a[3], "text": a[4],
                                         **({"jpegBase64": base64.b64encode(a[5]).decode()} if a[5] else {})} for a in al])
                    store.mark_alerts_sent([a[0] for a in al])
                alerts_waiting = bool(al) and len(al) == 20
                if upload_due:
                    rows = store.unsent_minutes()
                    if rows:
                        client.send_minutes([m.to_json() for m in rows])
                        store.mark_minutes_sent(rows)
            except YurtError as e:
                alerts_waiting = False  # try again with the next minute upload
                log.warning("%s (kept, will send later)", e)

        # live panel in Yurt: only when something changed, at most every LIVE_MIN_GAP seconds, never queued up
        if client and t - last_live >= LIVE_MIN_GAP:
            state = live.state(now)
            if state != live_sent:
                last_live = t
                try:
                    client.send_live({**state, "at": now.strftime("%Y-%m-%dT%H:%M:%S")})
                    live_sent = state
                except YurtError as e:
                    log.debug("live update dropped: %s", e)

        # daily report shortly after closing (and after the last shift)
        for day in (now.date() - timedelta(days=1), now.date()):
            if day.isoformat() in sent:
                continue
            try:
                _, _, last_end = report_window(cfg, day)
            except Exception:  # noqa: BLE001
                last_end = open_window(cfg, day)[1]
            if now >= last_end + timedelta(minutes=REPORT_DELAY_MINUTES) and now - last_end < timedelta(hours=12):
                text, data = build_report(cfg, store, day)
                try:
                    if client:
                        client.send_report(day.isoformat(), text, data)
                    else:
                        telegram.send(cfg.telegram_token, cfg.telegram_chat, text)
                except YurtError as e:
                    log.warning("report not sent yet: %s", e)
                    continue
                sent.add(day.isoformat())
                sent_file.write_text("\n".join(sorted(sent)))

        if last_prune != now.date():
            last_prune = now.date()
            store.prune(now)
            prune(cfg.data_dir / "checks", now.date(), cfg.keep_pictures_days)

        time.sleep(max(0.0, tick - (time.monotonic() - t)))


def main(argv=None):
    p = argparse.ArgumentParser(description="Yurt Watch camera box")
    p.add_argument("command", choices=["download-model", "snapshot", "check", "compare", "connect", "run", "report", "test-telegram"])
    p.add_argument("--engine", choices=["yolox", "rfdetr"], help="download-model: which model (default yolox)")
    p.add_argument("--video", help="compare: a video file instead of the cameras")
    p.add_argument("--show", action="store_true", help="run: a live window per camera with zones, people and station state")
    p.add_argument("--config", default="config.toml")
    p.add_argument("--date", help="report day, YYYY-MM-DD (default today)")
    p.add_argument("--send", action="store_true", help="also send the report (to Yurt, or Telegram when standalone)")
    args = p.parse_args(argv)
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
    cfg = config_mod.load(args.config)
    {
        "download-model": cmd_download_model,
        "compare": cmd_compare,
        "snapshot": cmd_snapshot,
        "check": cmd_check,
        "connect": cmd_connect,
        "run": cmd_run,
        "report": cmd_report,
        "test-telegram": cmd_test_telegram,
    }[args.command](cfg, args)


if __name__ == "__main__":
    main()
