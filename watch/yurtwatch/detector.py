"""Person detector: YOLOX-Tiny (Apache-2.0 licence) through ONNX Runtime on the CPU. No faces, no identity."""
from __future__ import annotations

import hashlib
import urllib.request
from pathlib import Path

import cv2
import numpy as np

MODEL_URL = "https://github.com/Megvii-BaseDetection/YOLOX/releases/download/0.1.1rc0/yolox_tiny.onnx"
MODEL_SHA256 = "427cc366d34e27ff7a03e2899b5e3671425c262ea2291f88bb942bc1cc70b0f7"
PERSON = 0  # COCO class 0


def download_model(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(".part")
    urllib.request.urlretrieve(MODEL_URL, tmp)
    digest = hashlib.sha256(tmp.read_bytes()).hexdigest()
    if digest != MODEL_SHA256:
        tmp.unlink(missing_ok=True)
        raise RuntimeError(f"downloaded model has the wrong checksum ({digest}); try again")
    tmp.replace(path)


class PersonDetector:
    def __init__(self, model_path: str | Path, min_confidence: float = 0.45, threads: int = 2):
        import onnxruntime as ort

        opts = ort.SessionOptions()
        opts.intra_op_num_threads = threads
        self.session = ort.InferenceSession(str(model_path), opts, providers=["CPUExecutionProvider"])
        inp = self.session.get_inputs()[0]
        self.input_name = inp.name
        self.size = (int(inp.shape[2]), int(inp.shape[3]))  # (h, w)
        self.min_confidence = min_confidence
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

    def detect(self, frame: np.ndarray) -> list[tuple[float, float, float, float, float]]:
        """Returns person boxes (x1, y1, x2, y2, score) in frame pixels."""
        h, w = self.size
        ratio = min(h / frame.shape[0], w / frame.shape[1])
        resized = cv2.resize(frame, (int(frame.shape[1] * ratio), int(frame.shape[0] * ratio)))
        padded = np.full((h, w, 3), 114, dtype=np.uint8)
        padded[: resized.shape[0], : resized.shape[1]] = resized
        blob = padded.transpose(2, 0, 1)[None].astype(np.float32)

        out = self.session.run(None, {self.input_name: blob})[0][0]
        out[:, :2] = (out[:, :2] + self._grids) * self._strides
        out[:, 2:4] = np.exp(out[:, 2:4]) * self._strides

        scores = out[:, 4] * out[:, 5 + PERSON]
        keep = scores >= self.min_confidence
        if not keep.any():
            return []
        cx, cy, bw, bh = (out[keep, i] / ratio for i in range(4))
        scores = scores[keep]
        x1, y1 = cx - bw / 2, cy - bh / 2
        rects = [[float(a), float(b), float(c), float(d)] for a, b, c, d in zip(x1, y1, bw, bh)]
        idx = cv2.dnn.NMSBoxes(rects, scores.tolist(), self.min_confidence, 0.45)
        result = []
        for i in np.array(idx).flatten():
            x, y, bw_, bh_ = rects[i]
            result.append((max(0.0, x), max(0.0, y), min(frame.shape[1], x + bw_), min(frame.shape[0], y + bh_), float(scores[i])))
        return result
