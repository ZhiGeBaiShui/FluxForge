"""流源视觉验收脚本：面积/包围盒/调色板/色盲分组/quads 统计。

设计稿通过 `manifest.json` 提供 tile 列表：
    [{"path":"...png","answer":"water","meta":"cold_mid_range3"},
     {"path":"...png","answer":"cold","group":"temp"}, ...]
可选的 `quads.json`：
    {"static": 24, "peak": 31, "parts": [["body", 22], ["highlight", 1]]}

用法：
    python verify.py --manifest <manifest.json> --tiles <dir> --out <dir>
    python verify.py --all           # 用默认路径
"""
import argparse
import csv
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
DEFAULT_MANIFEST = ROOT / "docs/flow-visual/final/tiles/manifest.json"
DEFAULT_OUT = ROOT / "docs/flow-visual/final/png"
DEFAULT_QUADS = ROOT / "docs/flow-visual/final/tiles/quads.json"

BG = np.array([25, 21, 20], dtype=float)


# ── 颜色 ────────────────────────────────────────────────────
def srgb_to_linear(c):
    c = np.asarray(c, dtype=float) / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def rgb_to_lab(rgb):
    lin = srgb_to_linear(rgb)
    m = np.array([[0.4124564, 0.3575761, 0.1804375],
                  [0.2126729, 0.7151522, 0.0721750],
                  [0.0193339, 0.1191920, 0.9503041]])
    xyz = m @ lin
    white = np.array([0.95047, 1.0, 1.08883])
    t = xyz / white
    f = np.where(t > 0.008856, np.cbrt(t), 7.787 * t + 16 / 116)
    return np.array([116 * f[1] - 16, 500 * (f[0] - f[1]), 200 * (f[1] - f[2])])


def delta_e(a, b):
    return float(np.linalg.norm(rgb_to_lab(a) - rgb_to_lab(b)))


DEUTAN = np.array([[0.367322, 0.860646, -0.227968],
                   [0.280085, 0.672501, 0.047413],
                   [-0.011820, 0.042940, 0.968881]])


def simulate(rgb, kind="deutan"):
    lin = srgb_to_linear(rgb).reshape(3)
    out = np.clip(DEUTAN @ lin, 0, 1)
    srgb = np.where(out <= 0.0031308, out * 12.92, 1.055 * out ** (1 / 2.4) - 0.055) * 255
    return np.round(srgb)


def wcag_y(rgb):
    lin = srgb_to_linear(rgb)
    return float(0.2126 * lin[0] + 0.7152 * lin[1] + 0.0722 * lin[2])


def contrast(a, b):
    ya, yb = wcag_y(a), wcag_y(b)
    hi, lo = max(ya, yb), min(ya, yb)
    return (hi + 0.05) / (lo + 0.05)


# ── tile 度量 ───────────────────────────────────────────────
def resolve_tile(it, tiles_dir):
    p = Path(it["path"])
    candidates = [p] if p.is_absolute() else [tiles_dir / p, ROOT / p, p]
    for c in candidates:
        if c.exists():
            return c
    return tiles_dir / p


def load_tile(path):
    return np.asarray(Image.open(path).convert("RGBA"))


def alpha_mask(tile):
    return tile[:, :, 3] > 0


def tile_area(tile):
    return int(alpha_mask(tile).sum())


def tile_bbox(tile):
    ys, xs = np.where(alpha_mask(tile))
    if len(xs) == 0:
        return (0, 0, 0, 0)
    return (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max()))


def bbox_center(tile):
    x0, y0, x1, y1 = tile_bbox(tile)
    return ((x0 + x1) / 2.0, (y0 + y1) / 2.0)


def dominant_colors(tile, top=6):
    m = alpha_mask(tile)
    px = tile[m][:, :3]
    if len(px) == 0:
        return []
    cols, counts = np.unique(px, axis=0, return_counts=True)
    order = np.argsort(-counts)[:top]
    return [(tuple(int(v) for v in cols[i]), int(counts[i])) for i in order]


def group_colors(colors, kind=None, threshold=25.0):
    """按 ΔE76 做并查集分组。"""
    cols = [simulate(np.array(c, float), kind) if kind else np.array(c, float) for c in colors]
    n = len(cols)
    parent = list(range(n))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[rb] = ra

    for i in range(n):
        for j in range(i + 1, n):
            if delta_e(cols[i], cols[j]) < threshold:
                union(i, j)

    groups = {}
    for i in range(n):
        groups.setdefault(find(i), []).append(i)
    return list(groups.values())


def write_csv(path, header, rows):
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(header)
        w.writerows(rows)


# ── 检查项 ──────────────────────────────────────────────────
def check_areas(items, tiles_dir, out_dir):
    rows = []
    for it in items:
        p = resolve_tile(it, tiles_dir)
        t = load_tile(p)
        rows.append([it.get("answer", ""), it.get("meta", ""), p.name,
                     tile_area(t), *tile_bbox(t)])
    write_csv(out_dir / "areas.csv",
              ["answer", "meta", "file", "area", "x0", "y0", "x1", "y1"], rows)
    return rows


def check_dirs(items, tiles_dir):
    by_answer = {}
    for it in items:
        if it.get("group") != "dir":
            continue
        p = resolve_tile(it, tiles_dir)
        by_answer.setdefault(it["answer"], []).append(bbox_center(load_tile(p)))
    if by_answer.get("up") and by_answer.get("right"):
        pts = [np.array(v[0]) for v in by_answer.values()]
        spread = np.ptp(np.array(pts), axis=0)
        return {"centers": {k: v[0] for k, v in by_answer.items()},
                "spread": [float(spread[0]), float(spread[1])]}
    return None


def check_palette(items, tiles_dir, kind="deutan"):
    cols = []
    for it in items:
        if it.get("group") != "comp":
            continue
        p = resolve_tile(it, tiles_dir)
        dom = dominant_colors(load_tile(p), top=1)
        if dom:
            cols.append((it.get("answer", ""), dom[0][0]))
    if not cols:
        return None
    groups = group_colors([c for _, c in cols], kind=None, threshold=25.0)
    groups_cb = group_colors([c for _, c in cols], kind=kind, threshold=25.0)
    detail = []
    for name, c in cols:
        detail.append([name, "#%02X%02X%02X" % c, round(wcag_y(c), 4),
                       round(contrast(c, BG), 2)])
    return {"colors": detail,
            "groups_normal": len(groups),
            "groups_" + kind: len(groups_cb),
            "group_members": [[cols[i][0] for i in g] for g in groups_cb]}


def check_quads(quads_path):
    if not quads_path.exists():
        return None
    return json.loads(quads_path.read_text(encoding="utf-8"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--manifest", default=str(DEFAULT_MANIFEST))
    ap.add_argument("--tiles", default=None)
    ap.add_argument("--out", default=str(DEFAULT_OUT))
    ap.add_argument("--quads", default=str(DEFAULT_QUADS))
    ap.add_argument("--all", action="store_true")
    args = ap.parse_args()

    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)
    manifest_path = Path(args.manifest)
    if not manifest_path.exists():
        print(f"manifest 不存在：{manifest_path}")
        return 2

    items = json.loads(manifest_path.read_text(encoding="utf-8"))
    tiles_dir = Path(args.tiles) if args.tiles else manifest_path.parent

    check_areas(items, tiles_dir, out_dir)
    dirs = check_dirs(items, tiles_dir)
    pal = check_palette(items, tiles_dir)
    quads = check_quads(Path(args.quads))

    report = {"tiles": len(items), "dirs": dirs, "palette": pal, "quads": quads}
    (out_dir / "verify.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    print(f"tiles: {len(items)}")
    print(f"areas.csv -> {out_dir / 'areas.csv'}")
    if dirs:
        print("方向中心偏移：", dirs["spread"])
    if pal:
        print("颜色分组：正常", pal["groups_normal"], "/ 色盲", pal["groups_deutan"])
        for row in pal["colors"]:
            print("  ", row)
    if quads:
        print("quads:", quads)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
