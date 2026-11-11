"""Detectors: find people and phones in a picture. No faces, no identity.

Two models, both free for commercial use (Apache-2.0), both run through ONNX Runtime:
- RF-DETR Nano (Roboflow): newer, better with people standing close together or half hidden behind the coffee
  machine. The box makes the ONNX file itself once (`python watch.py download-model --engine rfdetr`), see
  export_rfdetr.py.
- YOLOX-Tiny (Megvii): small and fast, downloaded ready-made. The fallback.

`device` picks where the model runs: cpu, intel (OpenVINO; needs `pip install onnxruntime-openvino`) or nvidia
(CUDA; needs `pip install onnxruntime-gpu`). "auto" takes the best one installed.
"""
from __future__ import annotations

import hashlib
import logging
import urllib.request
from pathlib import Path

import cv2
import numpy as np

log = logging.getLogger("watch.detector")

MODEL_URL = "https://github.com/Megvii-BaseDetection/YOLOX/releases/download/0.1.1rc0/yolox_tiny.onnx"
MODEL_SHA256 = "427cc366d34e27ff7a03e2899b5e3671425c262ea2291f88bb942bc1cc70b0f7"
PERSON = 0  # COCO class ids as YOLOX numbers them (0..79)
PHONE = 67

Box = tuple  # (x1, y1, x2, y2, score) in pixels

PROVIDERS = {
    "cpu": "CPUExecutionProvider",
    "intel": "OpenVINOExecutionProvider",
    "nvidia": "CUDAExecutionProvider",
}


def download_model(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(".part")
    urllib.request.urlretrieve(MODEL_URL, tmp)
    digest = hashlib.sha256(tmp.read_bytes()).hexdigest()
    if digest != MODEL_SHA256:
        tmp.unlink(missing_ok=True)
        raise RuntimeError(f"downloaded model has the wrong checksum ({digest}); try again")
    tmp.replace(path)


def pick_providers(device: str, available: list[str]) -> list:
    """ONNX Runtime providers for a device setting, best first, always ending with the CPU."""
    device = (device or "auto").lower()
    if device == "auto":
        wanted = [p for p in (PROVIDERS["nvidia"], PROVIDERS["intel"]) if p in available]
    elif device in PROVIDERS:
        wanted = [PROVIDERS[device]]
        if wanted[0] not in available:
            hint = {"intel": "pip install onnxruntime-openvino", "nvidia": "pip install onnxruntime-gpu"}.get(device, "")
            log.warning("device %s is not available here (%s); using the CPU", device, hint)
            wanted = []
    else:
        raise ValueError(f"device must be auto, cpu, intel or nvidia, not {device!r}")
    out: list = []
    for p in wanted:
        if p == PROVIDERS["intel"]:
            out.append((p, {"device_type": "CPU"}))  # OpenVINO on the Intel CPU (and its built-in GPU with "GPU")
        else:
            out.append(p)
    if PROVIDERS["cpu"] not in wanted:
        out.append(PROVIDERS["cpu"])
    return out


def _session(model_path: Path, device: str, threads: int):
    import onnxruntime as ort

    opts = ort.SessionOptions()
    opts.intra_op_num_threads = threads
    providers = pick_providers(device, ort.get_available_providers())
    session = ort.InferenceSession(str(model_path), opts, providers=providers)
    log.info("model %s runs on %s", model_path.name, ", ".join(session.get_providers()))
    return session


def _nms(boxes: list[Box], iou: float = 0.6) -> list[Box]:
    if len(boxes) < 2:
        return boxes
    rects = [[b[0], b[1], b[2] - b[0], b[3] - b[1]] for b in boxes]
    idx = cv2.dnn.NMSBoxes(rects, [b[4] for b in boxes], 0.0, iou)
    return [boxes[i] for i in np.array(idx).flatten()]


class BaseDetector:
    name = "base"
    min_confidence: float
    phone_confidence: float

    def detect_raw(self, frame: np.ndarray) -> tuple[list[Box], list[Box]]:  # pragma: no cover - per model
        raise NotImplementedError

    def detect(self, frame: np.ndarray) -> list[Box]:
        """People in the frame."""
        return self.detect_raw(frame)[0]

    def detect_all(self, frame: np.ndarray) -> tuple[list[Box], list[Box]]:
        """(people, phones) in the frame."""
        return self.detect_raw(frame)

    def phones_near(self, frame: np.ndarray, person: Box) -> list[Box]:
        """A phone is small in a wide camera picture, so look again in a close-up of one person."""
        x1, y1, x2, y2 = (int(v) for v in person[:4])
        pw, ph = x2 - x1, y2 - y1
        cx1, cy1 = max(0, x1 - pw // 5), max(0, y1 - ph // 10)
        cx2, cy2 = min(frame.shape[1], x2 + pw // 5), min(frame.shape[0], y2 + ph // 10)
        crop = frame[cy1:cy2, cx1:cx2]
        if crop.shape[0] < 32 or crop.shape[1] < 16:
            return []
        phones = self.detect_raw(crop)[1]
        return [(a + cx1, b + cy1, c + cx1, d + cy1, s) for a, b, c, d, s in phones]


class YoloxDetector(BaseDetector):
    name = "yolox"

    def __init__(self, model_path: str | Path, min_confidence: float = 0.45, phone_confidence: float = 0.3,
                 threads: int = 2, device: str = "cpu"):
        self.session = _session(Path(model_path), device, threads)
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
        boxes = [
            (max(0.0, float(a - c / 2)), max(0.0, float(b - d / 2)),
             min(float(frame_shape[1]), float(a + c / 2)), min(float(frame_shape[0]), float(b + d / 2)), float(s))
            for a, b, c, d, s in zip(cx, cy, bw, bh, scores[keep])
        ]
        return _nms(boxes, 0.45)

    def detect_raw(self, frame: np.ndarray) -> tuple[list[Box], list[Box]]:
        out, ratio = self._raw(frame)
        return (self._pick(out, ratio, PERSON, self.min_confidence, frame.shape),
                self._pick(out, ratio, PHONE, self.phone_confidence, frame.shape))


# RF-DETR's official COCO weights keep the original COCO category ids (1..90).
RF_PERSON = 1
RF_PHONE = 77
IMAGENET_MEAN = np.array([0.485, 0.456, 0.406], dtype=np.float32)
IMAGENET_STD = np.array([0.229, 0.224, 0.225], dtype=np.float32)


def rfdetr_decode(boxes_cxcywh: np.ndarray, logits: np.ndarray, width: int, height: int, cls: int, conf: float) -> list[Box]:
    """RF-DETR output for one picture -> boxes of one class. Each class has its own sigmoid score (no softmax)."""
    if cls >= logits.shape[1]:
        return []
    scores = 1.0 / (1.0 + np.exp(-np.clip(logits[:, cls].astype(np.float32), -88, 88)))
    keep = scores > conf
    if not keep.any():
        return []
    cx, cy, bw, bh = boxes_cxcywh[keep].astype(np.float32).T
    out = []
    for a, b, c, d, s in zip(cx, cy, bw, bh, scores[keep]):
        x1, y1 = max(0.0, (a - c / 2) * width), max(0.0, (b - d / 2) * height)
        x2, y2 = min(float(width), (a + c / 2) * width), min(float(height), (b + d / 2) * height)
        if x2 > x1 and y2 > y1:
            out.append((float(x1), float(y1), float(x2), float(y2), float(s)))
    # RF-DETR needs no NMS by design; a light one only removes the rare duplicate query
    return _nms(sorted(out, key=lambda b: -b[4]), 0.7)


class RfDetrDetector(BaseDetector):
    name = "rfdetr"

    def __init__(self, model_path: str | Path, min_confidence: float = 0.45, phone_confidence: float = 0.3,
                 threads: int = 2, device: str = "cpu"):
        self.session = _session(Path(model_path), device, threads)
        inp = self.session.get_inputs()[0]
        self.input_name = inp.name
        h, w = inp.shape[2], inp.shape[3]
        self.size = (int(h) if isinstance(h, int) else 384, int(w) if isinstance(w, int) else 384)
        names = [o.name for o in self.session.get_outputs()]
        self._boxes_i = names.index("dets") if "dets" in names else 0
        self._logits_i = names.index("labels") if "labels" in names else 1
        self.min_confidence = min_confidence
        self.phone_confidence = phone_confidence

    def _input(self, frame: np.ndarray) -> np.ndarray:
        h, w = self.size
        rgb = cv2.cvtColor(cv2.resize(frame, (w, h), interpolation=cv2.INTER_LINEAR), cv2.COLOR_BGR2RGB)
        x = (rgb.astype(np.float32) / 255.0 - IMAGENET_MEAN) / IMAGENET_STD
        return x.transpose(2, 0, 1)[None]

    def detect_raw(self, frame: np.ndarray) -> tuple[list[Box], list[Box]]:
        out = self.session.run(None, {self.input_name: self._input(frame)})
        boxes, logits = out[self._boxes_i][0], out[self._logits_i][0]
        fh, fw = frame.shape[:2]
        return (rfdetr_decode(boxes, logits, fw, fh, RF_PERSON, self.min_confidence),
                rfdetr_decode(boxes, logits, fw, fh, RF_PHONE, self.phone_confidence))


def make_detector(engine: str, yolox_path: Path, rfdetr_path: Path, min_confidence: float, phone_confidence: float,
                  device: str = "auto", threads: int = 2) -> BaseDetector:
    """engine: rfdetr, yolox or auto (RF-DETR when its file is there, otherwise YOLOX)."""
    engine = (engine or "auto").lower()
    if engine not in ("auto", "rfdetr", "yolox"):
        raise ValueError(f"engine must be auto, rfdetr or yolox, not {engine!r}")
    if engine in ("auto", "rfdetr") and rfdetr_path.exists():
        return RfDetrDetector(rfdetr_path, min_confidence, phone_confidence, threads, device)
    if engine == "rfdetr":
        raise FileNotFoundError(f"{rfdetr_path} is missing: run  python watch.py download-model --engine rfdetr")
    if not yolox_path.exists():
        raise FileNotFoundError(f"{yolox_path} is missing: run  python watch.py download-model")
    return YoloxDetector(yolox_path, min_confidence, phone_confidence, threads, device)


# kept for older imports
Detector = YoloxDetector
PersonDetector = YoloxDetector
