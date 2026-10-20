"""为交互预览烘 12 帧动效图集（仅 flow 状态），键：comp|temp|content|flow|dir|frame。"""
import json
import sys
from pathlib import Path

from PIL import Image

FINAL = Path(__file__).resolve().parent.parent.parent / "docs/flow-visual/final"
sys.path.insert(0, str(FINAL))
import render_final as rf  # noqa: E402

OUT_PNG = FINAL / "atlas_frames.png"
PER_ROW = 16


def make_tile(comp, temp, content, dirn, frame):
    g = rf.build_grid(comp, content, "flow", dirn, temp, frame)
    bbox = rf.bbox_of(g) or rf.CBBOX[content]
    tile = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    px = tile.load()
    rgb = rf.COMPS[comp]
    for y in range(16):
        for x in range(16):
            r = g[y][x]
            if r != ".":
                px[x, y] = rf.role_rgba(r, x, y, rgb, temp, bbox)
    return tile


def main():
    keys, tiles = [], []
    for comp in rf.COMPS:
        for temp in rf.TEMPS:
            for content in rf.CONTENT:
                for dirn in rf.DIRS:
                    for frame in range(12):
                        keys.append(f"{comp}|{temp}|{content}|flow|{dirn}|{frame}")
                        tiles.append(make_tile(comp, temp, content, dirn, frame))
    rows = (len(tiles) + PER_ROW - 1) // PER_ROW
    atlas = Image.new("RGBA", (PER_ROW * 16, rows * 16), (0, 0, 0, 0))
    index = {}
    for i, (k, t) in enumerate(zip(keys, tiles)):
        x, y = (i % PER_ROW) * 16, (i // PER_ROW) * 16
        atlas.paste(t, (x, y))
        index[k] = [x, y]
    atlas.save(OUT_PNG, optimize=True)
    (FINAL / "atlas_frames.js").write_text(
        "window.FLOW_FRAMES = " + json.dumps(index, ensure_ascii=False, separators=(",", ":")) + ";\n",
        encoding="utf-8")
    print(f"{len(tiles)} frames -> {OUT_PNG} ({atlas.width}x{atlas.height})")


if __name__ == "__main__":
    main()
