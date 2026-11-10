"""Zones are polygons drawn in percent of the picture (0..100), so they work for any camera resolution."""
from __future__ import annotations

from dataclasses import dataclass, field


@dataclass
class Zone:
    name: str
    kind: str  # "station" (bar, till: anyone inside counts as staff) or "queue" (guests waiting)
    camera: str
    points: list[tuple[float, float]]
    queue: str | None = None  # for a station: the queue zone whose guests it serves
    label: str = ""

    def __post_init__(self) -> None:
        if self.kind not in ("station", "queue"):
            raise ValueError(f"zone {self.name}: kind must be 'station' or 'queue', not {self.kind!r}")
        if len(self.points) < 3:
            raise ValueError(f"zone {self.name}: needs at least 3 points")
        self.label = self.label or self.name


def point_in_polygon(x: float, y: float, poly: list[tuple[float, float]]) -> bool:
    """Ray casting. x, y and poly in the same units."""
    inside = False
    n = len(poly)
    j = n - 1
    for i in range(n):
        xi, yi = poly[i]
        xj, yj = poly[j]
        if (yi > y) != (yj > y):
            cross = (xj - xi) * (y - yi) / (yj - yi) + xi
            if x < cross:
                inside = not inside
        j = i
    return inside


def foot_point(box: tuple[float, float, float, float], width: int, height: int) -> tuple[float, float]:
    """Where a person stands: bottom centre of the box, in percent of the picture.

    Using the feet (not the box centre) means a barista leaning over the counter still counts where they stand.
    """
    x1, y1, x2, y2 = box[:4]
    return ((x1 + x2) / 2 / width * 100.0, y2 / height * 100.0)


def count_in_zones(boxes, width: int, height: int, zones: list[Zone]) -> dict[str, int]:
    counts = {z.name: 0 for z in zones}
    for box in boxes:
        fx, fy = foot_point(box, width, height)
        for z in zones:
            if point_in_polygon(fx, fy, z.points):
                counts[z.name] += 1
    return counts
