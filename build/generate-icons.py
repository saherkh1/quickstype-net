#!/usr/bin/env python3
"""Generate placeholder tray icons for QuickSType.

States: idle (grey ring), recording (red filled), processing (orange ring).
Run from repo root:
    python3 build/generate-icons.py
"""
from __future__ import annotations
from pathlib import Path
from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent.parent / "src" / "QuickSType.UI" / "Assets"
OUT.mkdir(parents=True, exist_ok=True)

STATES = {
    "idle":       {"fill": None, "outline": (140, 140, 140, 255), "width": 2},
    "recording":  {"fill": (220, 50, 50, 255), "outline": (220, 50, 50, 255), "width": 2},
    "processing": {"fill": None, "outline": (240, 180, 30, 255), "width": 3},
}

SIZES = [(16, ""), (32, "@2x"), (44, "@3x")]


def make(name: str, size: int, fill, outline, width: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pad = max(2, size // 8)
    d.ellipse((pad, pad, size - pad, size - pad), fill=fill, outline=outline, width=width)
    return img


def main() -> None:
    for state, props in STATES.items():
        for size, suffix in SIZES:
            img = make(state, size, props["fill"], props["outline"], props["width"])
            path = OUT / f"tray-{state}{suffix}.png"
            img.save(path, "PNG")
            print(f"wrote {path}")

    icon = make("idle", 256, (50, 100, 200, 255), (50, 100, 200, 255), 4)
    icon_path = OUT / "AppIcon.png"
    icon.save(icon_path, "PNG")
    print(f"wrote {icon_path}")


if __name__ == "__main__":
    main()
