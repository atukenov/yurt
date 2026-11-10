"""Reads one camera in the background and keeps only the newest picture. Video (and audio) is never saved."""
from __future__ import annotations

import logging
import os
import threading
import time

import cv2

# RTSP over TCP is steadier on cafe Wi-Fi than UDP. Audio is not requested from the stream.
os.environ.setdefault("OPENCV_FFMPEG_CAPTURE_OPTIONS", "rtsp_transport;tcp")

log = logging.getLogger("watch.camera")


class CameraReader(threading.Thread):
    def __init__(self, name: str, url: str):
        super().__init__(daemon=True, name=f"cam-{name}")
        self.cam_name = name
        self.url = url
        self._lock = threading.Lock()
        self._frame = None
        self._at = 0.0
        self._stop = threading.Event()
        # A number (0, 1, ...) is a camera plugged into this computer (USB webcam, laptop camera).
        self.device = int(url) if url.strip().isdigit() else None
        self.is_file = self.device is None and not url.lower().startswith(("rtsp://", "rtsps://", "http://", "https://"))

    def latest(self, max_age: float = 5.0):
        with self._lock:
            if self._frame is None or time.monotonic() - self._at > max_age:
                return None
            return self._frame

    def stop(self) -> None:
        self._stop.set()

    def run(self) -> None:
        backoff = 2.0
        while not self._stop.is_set():
            cap = cv2.VideoCapture(self.device) if self.device is not None else cv2.VideoCapture(self.url, cv2.CAP_FFMPEG)
            if not cap.isOpened():
                log.warning("camera %s: cannot open, retry in %.0fs", self.cam_name, backoff)
                self._stop.wait(backoff)
                backoff = min(backoff * 2, 60)
                continue
            log.info("camera %s: connected", self.cam_name)
            backoff = 2.0
            fps = cap.get(cv2.CAP_PROP_FPS) or 25
            while not self._stop.is_set():
                ok, frame = cap.read()
                if not ok:
                    if self.is_file:  # a test video: start again from the beginning
                        cap.set(cv2.CAP_PROP_POS_FRAMES, 0)
                        continue
                    log.warning("camera %s: stream lost", self.cam_name)
                    break
                with self._lock:
                    self._frame = frame
                    self._at = time.monotonic()
                if self.is_file:
                    time.sleep(1.0 / fps)  # play a file at its real speed
            cap.release()


def grab_one(url: str, timeout: float = 15.0):
    """One picture from a camera, for setup commands."""
    reader = CameraReader("setup", url)
    reader.start()
    end = time.monotonic() + timeout
    while time.monotonic() < end:
        f = reader.latest()
        if f is not None:
            reader.stop()
            return f
        time.sleep(0.2)
    reader.stop()
    return None
