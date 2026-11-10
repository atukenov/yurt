"""Zones are drawn in percent of the picture (0..100), so they work for any camera resolution.

Kinds:
  station  bar, till: the floor behind the counter. Anyone there counts as staff.
  queue    the floor where guests wait.
  door     a line across the entrance (2 points) plus an `inside` point: people crossing towards it are visitors.
"""
from __future__ import annotations

from dataclasses import dataclass

KINDS = ("station", "queue", "door")


@dataclass
class Zone:
    name: str
    kind: str
    camera: str
    points: list[tuple[float, float]]
    queue: str | None = None  # for a station: the queue zone whose guests it serves
    label: str = ""
    inside: tuple[float, float] | None = None  # for a door: any point on the inside of the line

    def __post_init__(self) -> None:
        if self.kind not in KINDS:
            raise ValueError(f"zone {self.name}: kind must be one of {', '.join(KINDS)}, not {self.kind!r}")
        if self.kind == "door":
            if len(self.points) != 2 or self.inside is None:
                raise ValueError(f"zone {self.name}: a door is a line of 2 points plus an 'inside' point")
            if line_side(self.inside, self.points[0], self.points[1]) == 0:
                raise ValueError(f"zone {self.name}: the 'inside' point must not lie on the door line")
        elif len(self.points) < 3:
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


def line_side(p, a, b) -> int:
    """+1 / -1 for the two sides of the line a-b, 0 on it."""
    v = (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])
    return (v > 0) - (v < 0)


def segments_cross(p1, p2, a, b) -> bool:
    """Does the step p1->p2 cross the segment a-b (not just the infinite line)?"""
    d1, d2 = line_side(p1, a, b), line_side(p2, a, b)
    d3, d4 = line_side(a, p1, p2), line_side(b, p1, p2)
    return d1 != d2 and d1 != 0 and d2 != 0 and d3 != d4


def foot_point(box, width: int, height: int) -> tuple[float, float]:
    """Where a person stands: bottom centre of the box, in percent of the picture.

    Using the feet (not the box centre) means a barista leaning over the counter still counts where they stand.
    """
    x1, y1, x2, y2 = box[:4]
    return ((x1 + x2) / 2 / width * 100.0, y2 / height * 100.0)


def in_zone(box, width: int, height: int, zone: Zone) -> bool:
    if zone.kind == "door":
        return False
    fx, fy = foot_point(box, width, height)
    return point_in_polygon(fx, fy, zone.points)


def count_in_zones(boxes, width: int, height: int, zones: list[Zone]) -> dict[str, int]:
    return {z.name: sum(1 for b in boxes if in_zone(b, width, height, z)) for z in zones if z.kind != "door"}
