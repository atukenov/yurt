"""Detector: YOLOX-Tiny (Apache-2.0 licence) through ONNX Runtime on the CPU. Finds people and phones. No faces, no identity."""
from __future__ import annotations

import hashlib
import urllib.request
from pathlib import Path

import cv2
import numpy as np

MODEL_URL = "https://github.com/Megvii-BaseDetection/YOLOX/releases/download/0.1.1rc0/yolox_tiny.onnx"
MODEL_SHA256 = "427cc366d34e27ff7a03e2899b5e3671425c262ea2291f88bb942bc1cc70b0f7"
PERSON = 0  # COCO class ids
PHONE = 67


def download_model(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(".part")
    urllib.request.urlretrieve(MODEL_URL, tmp)
    digest = hashlib.sha256(tmp.read_bytes()).hexdigest()
    if digest != MODEL_SHA256:
        tmp.unlink(missing_ok=True)
        raise RuntimeError(f"downloaded model has the wrong checksum ({digest}); try again")
    tmp.replace(path)


Box = tuple  # (x1, y1, x2, y2, score)


class Detector:
    def __init__(self, model_path: str | Path, min_confidence: float = 0.45, phone_confidence: float = 0.3, threads: int = 2):
        import onnxruntime as ort

        opts = ort.SessionOptions()
        opts.intra_op_num_threads = threads
        self.session = ort.InferenceSession(str(model_path), opts, providers=["CPUExecutionProvider"])
        inp = self.session.get_inputs()[0]
        self.input_name = inp.name
        self.size = (int(inp.shape[2]), int(inp.shape[3]))  # (h, w)
        self.min_confidence = min_confidence
        self.phone_confidence = phone_confidence
        self._grids, self._strides = self._make_grids()

    def _make_grids(self):
        h, w = self.size
        grids, strides = [], []
        for s in (8, 16, 32):
            hs, ws = h // s, w // s
            xv, yv = np.meshgrid(np.arange(ws), np.arange(hs))
            grids.append(np.stack((xv, yv), 2).reshape(-1, 2))
            strides.append(np.full((hs * ws, 1), s))
        return np.concatenate(grids).astype(np.float32), np.concatenate(strides).astype(np.float32)

    def _raw(self, frame: np.ndarray):
        h, w = self.size
        ratio = min(h / frame.shape[0], w / frame.shape[1])
        resized = cv2.resize(frame, (max(1, int(frame.shape[1] * ratio)), max(1, int(frame.shape[0] * ratio))))
        padded = np.full((h, w, 3), 114, dtype=np.uint8)
        padded[: resized.shape[0], : resized.shape[1]] = resized
        out = self.session.run(None, {self.input_name: padded.transpose(2, 0, 1)[None].astype(np.float32)})[0][0]
        out[:, :2] = (out[:, :2] + self._grids) * self._strides
        out[:, 2:4] = np.exp(out[:, 2:4]) * self._strides
        return out, ratio

    def _pick(self, out, ratio, cls: int, conf: float, frame_shape) -> list[Box]:
        scores = out[:, 4] * out[:, 5 + cls]
        keep = scores >= conf
        if not keep.any():
            return []
        cx, cy, bw, bh = (out[keep, i] / ratio for i in range(4))
        scores = scores[keep]
        rects = [[float(a - c / 2), float(b - d / 2), float(c), float(d)] for a, b, c, d in zip(cx, cy, bw, bh)]
        idx = cv2.dnn.NMSBoxes(rects, scores.tolist(), conf, 0.45)
        res = []
        for i in np.array(idx).flatten():
            x, y, w_, h_ = rects[i]
            res.append((max(0.0, x), max(0.0, y), min(frame_shape[1], x + w_), min(frame_shape[0], y + h_), float(scores[i])))
        return res

    def detect(self, frame: np.ndarray) -> list[Box]:
        """People in the frame, (x1, y1, x2, y2, score) in pixels."""
        out, ratio = self._raw(frame)
        return self._pick(out, ratio, PERSON, self.min_confidence, frame.shape)

    def detect_all(self, frame: np.ndarray) -> tuple[list[Box], list[Box]]:
        """(people, phones) in the frame."""
        out, ratio = self._raw(frame)
        return (self._pick(out, ratio, PERSON, self.min_confidence, frame.shape),
                self._pick(out, ratio, PHONE, self.phone_confidence, frame.shape))

    def phones_near(self, frame: np.ndarray, person: Box) -> list[Box]:
        """A phone is small in a wide camera picture, so look again in a close-up of one person."""
        x1, y1, x2, y2 = (int(v) for v in person[:4])
        pw, ph = x2 - x1, y2 - y1
        cx1, cy1 = max(0, x1 - pw // 5), max(0, y1 - ph // 10)
        cx2, cy2 = min(frame.shape[1], x2 + pw // 5), min(frame.shape[0], y2 + ph // 10)
        crop = frame[cy1:cy2, cx1:cx2]
        if crop.shape[0] < 32 or crop.shape[1] < 16:
            return []
        out, ratio = self._raw(crop)
        return [(a + cx1, b + cy1, c + cx1, d + cy1, s) for a, b, c, d, s in self._pick(out, ratio, PHONE, self.phone_confidence, crop.shape)]


# kept for older imports
PersonDetector = Detector
