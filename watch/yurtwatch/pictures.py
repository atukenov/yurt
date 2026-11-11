"""Pictures for setup and for checking accuracy. Heads are blurred; nothing here leaves the computer."""
from __future__ import annotations

import shutil
from datetime import date, timedelta
from pathlib import Path

import cv2
import numpy as np

from .zones import Zone

COLORS = {"station": (60, 180, 75), "queue": (0, 140, 255), "door": (255, 0, 200)}


def blur_heads(frame: np.ndarray, boxes) -> np.ndarray:
    """Blurs the top third of every person box, so a saved picture shows where people are, not who they are."""
    out = frame.copy()
    for b in boxes:
        x1, y1, x2, y2 = (int(v) for v in b[:4])
        hy = y1 + max(1, (y2 - y1) // 3)
        roi = out[max(0, y1):hy, max(0, x1):x2]
        if roi.size:
            k = max(15, ((x2 - x1) // 3) | 1)
            out[max(0, y1):hy, max(0, x1):x2] = cv2.GaussianBlur(roi, (k, k), 0)
    return out


def alert_jpeg(frame: np.ndarray, people, max_width: int = 640, quality: int = 70) -> bytes:
    """A small picture for an alert (about 30-50 KB): heads blurred, at most 640 px wide."""
    pic = blur_heads(frame, people)
    if pic.shape[1] > max_width:
        pic = cv2.resize(pic, (max_width, int(pic.shape[0] * max_width / pic.shape[1])), interpolation=cv2.INTER_AREA)
    ok, jpg = cv2.imencode(".jpg", pic, [cv2.IMWRITE_JPEG_QUALITY, quality])
    return jpg.tobytes() if ok else b""


def draw_grid(frame: np.ndarray) -> np.ndarray:
    """10% grid with numbers: read zone corners off this picture."""
    out = frame.copy()
    h, w = out.shape[:2]
    for p in range(10, 100, 10):
        x, y = int(w * p / 100), int(h * p / 100)
        cv2.line(out, (x, 0), (x, h), (255, 255, 255), 1)
        cv2.line(out, (0, y), (w, y), (255, 255, 255), 1)
        cv2.putText(out, str(p), (x + 2, 14), cv2.FONT_HERSHEY_SIMPLEX, 0.45, (0, 255, 255), 1)
        cv2.putText(out, str(p), (2, y - 3), cv2.FONT_HERSHEY_SIMPLEX, 0.45, (0, 255, 255), 1)
    return out


def draw_check(frame: np.ndarray, zones: list[Zone], boxes, counts: dict[str, int], phones=()) -> np.ndarray:
    out = blur_heads(frame, boxes)
    h, w = out.shape[:2]
    for z in zones:
        pts = np.array([(int(x / 100 * w), int(y / 100 * h)) for x, y in z.points], np.int32)
        cv2.polylines(out, [pts], z.kind != "door", COLORS[z.kind], 2)
        if z.kind == "door" and z.inside:
            cv2.circle(out, (int(z.inside[0] / 100 * w), int(z.inside[1] / 100 * h)), 6, COLORS["door"], -1)
        cv2.putText(out, f"{z.name}: {counts.get(z.name, 0)}", tuple(pts[0] + [4, 18]),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.6, COLORS[z.kind], 2)
    for b in boxes:
        x1, y1, x2, y2 = (int(v) for v in b[:4])
        cv2.rectangle(out, (x1, y1), (x2, y2), (255, 255, 0), 1)
        cv2.circle(out, ((x1 + x2) // 2, y2), 4, (0, 0, 255), -1)  # the foot point that is counted
    for p in phones:
        x1, y1, x2, y2 = (int(v) for v in p[:4])
        cv2.rectangle(out, (x1, y1), (x2, y2), (0, 0, 255), 2)
    return out


def prune(folder: Path, today: date, keep_days: int) -> None:
    if not folder.exists():
        return
    cut = (today - timedelta(days=keep_days)).isoformat()
    for d in folder.iterdir():
        if d.is_dir() and d.name < cut:
            shutil.rmtree(d, ignore_errors=True)
