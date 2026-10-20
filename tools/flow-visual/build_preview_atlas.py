"""为交互预览生成全组合图集（透明 tile），键格式 comp|temp|content|state|dir。

不依赖 render_final.render() 的底色，直接用 build_grid + role_rgba 画到透明画布。
"""
import json
import sys
from pathlib import Path

from PIL import Image

FINAL = Path(__file__).resolve().parent.parent.parent / "docs/flow-visual/final"
sys.path.insert(0, str(FINAL))
import render_final as rf  # noqa: E402

OUT_PNG = FINAL / "atlas_preview.png"
OUT_JSON = FINAL / "atlas_preview.json"
PER_ROW = 16


def make_tile(comp, temp, content, state, dirn):
    g = rf.build_grid(comp, content, state, dirn, temp, None)
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
    keys = []
    tiles = []
    for comp in rf.COMPS:
        for temp in rf.TEMPS:
            for content in rf.CONTENT:
                for state in rf.STATES:
                    for dirn in rf.DIRS:
                        keys.append(f"{comp}|{temp}|{content}|{state}|{dirn}")
                        tiles.append(make_tile(comp, temp, content, state, dirn))
    rows = (len(tiles) + PER_ROW - 1) // PER_ROW
    atlas = Image.new("RGBA", (PER_ROW * 16, rows * 16), (0, 0, 0, 0))
    index = {}
    for i, (k, t) in enumerate(zip(keys, tiles)):
        x, y = (i % PER_ROW) * 16, (i // PER_ROW) * 16
        atlas.paste(t, (x, y))
        index[k] = [x, y]
    atlas.save(OUT_PNG)
    OUT_JSON.write_text(json.dumps(index, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    (FINAL / "atlas_preview.js").write_text(
        "window.FLOW_ATLAS = " + json.dumps(index, ensure_ascii=False, separators=(",", ":")) + ";\n"
        "window.FLOW_ATLAS_SIZE = 16;\n", encoding="utf-8")
    print(f"{len(tiles)} tiles -> {OUT_PNG} ({atlas.width}x{atlas.height})")


if __name__ == "__main__":
    main()

