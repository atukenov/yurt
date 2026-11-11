"""Makes models/rfdetr_nano.onnx once, on this computer.

RF-DETR's weights are published as a PyTorch file, so turning them into ONNX needs PyTorch and the `rfdetr` package
(about 1 GB to download). To keep the box itself light, this runs in a separate folder (models/.rfdetr-export) that
can be deleted afterwards; the box only needs the .onnx file and onnxruntime.
"""
from __future__ import annotations

import os
import shutil
import subprocess
import sys
import venv
from pathlib import Path

RFDETR_VERSION = "1.12.0"

EXPORT_SCRIPT = """
import shutil, sys
from pathlib import Path
from rfdetr import RFDETRNano
out = Path(sys.argv[1])
model = RFDETRNano()
path = model.export(output_dir=str(out.parent / "rfdetr-export-out"), verbose=False)
path = Path(path)
if path.is_dir():
    path = next(path.glob("*.onnx"))
shutil.copyfile(path, out)
print("saved", out)
"""


def _python(env_dir: Path) -> Path:
    return env_dir / ("Scripts/python.exe" if os.name == "nt" else "bin/python")


def export(target: Path, keep_env: bool = False) -> None:
    target = target.resolve()
    target.parent.mkdir(parents=True, exist_ok=True)
    env_dir = target.parent / ".rfdetr-export"
    py = _python(env_dir)
    if not py.exists():
        print(f"making a separate Python folder for the one-time export: {env_dir}")
        venv.EnvBuilder(with_pip=True, clear=True).create(env_dir)

    def pip(*args: str) -> None:
        subprocess.run([str(py), "-m", "pip", "install", "--disable-pip-version-check", *args], check=True)

    pip("--upgrade", "pip")
    if sys.platform.startswith("linux"):
        # the default Linux PyTorch carries NVIDIA libraries (gigabytes); the CPU one is enough to export
        pip("torch", "torchvision", "--index-url", "https://download.pytorch.org/whl/cpu")
    pip(f"rfdetr[onnx]=={RFDETR_VERSION}")
    script = env_dir / "export_nano.py"
    script.write_text(EXPORT_SCRIPT, encoding="utf-8")
    print("downloading the RF-DETR Nano weights and exporting to ONNX (a few minutes)...")
    subprocess.run([str(py), str(script), str(target)], check=True, cwd=str(env_dir))
    shutil.rmtree(target.parent / "rfdetr-export-out", ignore_errors=True)
    if not keep_env:
        shutil.rmtree(env_dir, ignore_errors=True)
    print(f"ready: {target}")
