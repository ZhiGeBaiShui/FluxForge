"""把概念 B 的构造导出成 16×16 透明 tile + manifest（用于验收脚本 dry-run 与盲测）。

只读 concepts/b/render_b.py，不改它。
"""
import json
import sys
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
B_DIR = HERE.parent.parent / "docs/flow-visual/concepts/b"
OUT = HERE.parent.parent / "docs/flow-visual/round2/blind-b"
sys.path.insert(0, str(B_DIR))

import render_b as rb  # noqa: E402


def make_tile(comp, tier, temp, state, direction, frame):
    roles = rb.build(comp, tier, temp, state, direction, frame)
    cell = rb.colorize(roles, comp, temp)
    px = [(0, 0, 0, 0) for _ in range(rb.W * rb.W)]
    for (x, y), (c, a, _k) in cell.items():
        if 0 <= x < rb.W and 0 <= y < rb.W:
            px[y * rb.W + x] = (c[0], c[1], c[2], a)
    img = Image.new("RGBA", (rb.W, rb.W))
    img.putdata(px)
    return img


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    tiles, manifest = [], []

    def add(name, group, answer, **kw):
        img = make_tile(kw.get("comp", "water"), kw.get("tier", "large"),
                        kw.get("temp", "ambient"), kw.get("state", "flow"),
                        kw.get("direction", "right"), kw.get("frame", 0))
        p = OUT / f"{name}.png"
        img.save(p)
        tiles.append(p)
        manifest.append({"path": str(p.relative_to(HERE.parent.parent)).replace("\\", "/"),
                         "group": group, "answer": answer,
                         "meta": json.dumps(kw, ensure_ascii=False)})

    for c in ["water", "poison", "oil", "blood", "acid", "energy"]:
        add(f"comp-{c}", "comp", c, comp=c)

    for t in ["cold", "ambient", "hot"]:
        add(f"temp-{t}", "temp", t, temp=t)

    for tier in ["empty", "small", "mid", "large"]:
        add(f"content-{tier}", "content", tier, tier=tier)

    for d in ["up", "right", "down", "left"]:
        add(f"dir-{d}", "dir", d, direction=d)

    for s in ["flow", "blocked", "split", "merge", "collect"]:
        add(f"state-{s}", "state", s, state=s)

    (OUT / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"wrote {len(tiles)} tiles -> {OUT}")


if __name__ == "__main__":
    main()
