#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""build_runtime_atlas.py — 把终稿视觉烘成 C# 运行时图集。

契约见 docs/flow-visual/final/IMPL-PLAN-A.md §1：
- 16×16 RGBA 透明 tile，只取 render_final.render() 的 `lay` 层：
  无背景、无图标、无射程点、无文字；
- 静态 comp|temp|content|state|dir = 6×3×4×5×4 = 1440 张；
- 动效 comp|temp|content|flow|dir|f0..f11 = 6×3×4×4×12 = 3456 张；
- 每行 16 个，x=(i%16)*16，y=(i//16)*16，合计 4896 张 → 256×4896 px；
- 索引 {"tiles": {"<key>": [x, y]}}，key 全小写、| 分隔、UTF-8 无 BOM。

像素来源只有一个：render_final.build_grid() + render_final.role_rgba()。
本脚本不重画任何形状，也不改 render_final.py 的一个字节。

确定性：同一份输入两次运行输出字节相同
（PNG 不写时间戳类元数据；JSON 按固定枚举顺序插入）。

用法：
    python tools/flow-visual/build_runtime_atlas.py
"""
import hashlib
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent.parent
FINAL = ROOT / "docs/flow-visual/final"
ASSETS = ROOT / "assets"
SAMPLE = FINAL / "png/runtime-atlas-sample.png"

sys.path.insert(0, str(FINAL))
sys.dont_write_bytecode = True  # 不在 final/ 下留 __pycache__（只写自己名下的文件）
import render_final as rf  # noqa: E402

TILE = 16
PER_ROW = 16
ATLAS_PNG = ASSETS / "flow_atlas.png"
ATLAS_JSON = ASSETS / "flow_atlas.json"

# 枚举顺序 = IMPL-PLAN-A §1 的书写顺序；索引的插入序由此固定，必须稳定。
COMPS = ("water", "poison", "oil", "blood", "acid", "energy")
TEMPS = ("cold", "normal", "hot")
CONTENT = ("empty", "small", "mid", "large")
STATES = ("flow", "blocked", "split", "merge", "collect")
DIRS = ("up", "right", "down", "left")
FRAME_COUNT = 12

STATIC_COUNT = len(COMPS) * len(TEMPS) * len(CONTENT) * len(STATES) * len(DIRS)
DYNAMIC_COUNT = len(COMPS) * len(TEMPS) * len(CONTENT) * len(DIRS) * FRAME_COUNT
TOTAL = STATIC_COUNT + DYNAMIC_COUNT
ROWS = (TOTAL + PER_ROW - 1) // PER_ROW

# 与渲染器对表：枚举字符串必须完全一致，避免两边各写一份而悄悄漂移。
assert tuple(rf.COMPS) == COMPS, tuple(rf.COMPS)
assert tuple(rf.TEMPS) == TEMPS
assert tuple(rf.CONTENT) == CONTENT
assert tuple(rf.STATES) == STATES
assert set(rf.DIRS) == set(DIRS), rf.DIRS


def iter_keys():
    """按固定顺序产出 (key, 渲染参数)；静态 1440 在前，动效 3456 在后。"""
    for comp in COMPS:
        for temp in TEMPS:
            for content in CONTENT:
                for state in STATES:
                    for dirn in DIRS:
                        key = f"{comp}|{temp}|{content}|{state}|{dirn}"
                        yield key, (comp, temp, content, state, dirn, None)
    for comp in COMPS:
        for temp in TEMPS:
            for content in CONTENT:
                for dirn in DIRS:
                    for frame in range(FRAME_COUNT):
                        key = f"{comp}|{temp}|{content}|flow|{dirn}|f{frame}"
                        yield key, (comp, temp, content, "flow", dirn, frame)


def make_tile(comp, temp, content, state, dirn, frame):
    """透明 tile：等价于 render() 里的 lay 层，只是不叠底色/图标/射程点。"""
    g = rf.build_grid(comp, content, state, dirn, temp, frame)
    # 与 render() 一致：bbox 取最终网格（旋转/泡沫/温度之后），空网格退回 CBBOX。
    bbox = rf.bbox_of(g) or rf.CBBOX[content]
    rgb = rf.COMPS[comp]
    tile = Image.new("RGBA", (TILE, TILE), (0, 0, 0, 0))
    px = tile.load()
    for y in range(TILE):
        row = g[y]
        for x in range(TILE):
            role = row[x]
            if role != ".":
                px[x, y] = rf.role_rgba(role, x, y, rgb, temp, bbox)
    return tile


def build_atlas():
    """按行铺满整张图集，返回 (atlas, index)。"""
    atlas = Image.new("RGBA", (PER_ROW * TILE, ROWS * TILE), (0, 0, 0, 0))
    index = {}
    i = 0
    for key, args in iter_keys():
        x, y = (i % PER_ROW) * TILE, (i // PER_ROW) * TILE
        atlas.paste(make_tile(*args), (x, y))
        index[key] = [x, y]
        i += 1
        if i % 1440 == 0:
            print(f"  ... {i}/{TOTAL} tiles", flush=True)
    assert i == TOTAL, i
    return atlas, index


# ---------------------------------------------------------------- 抽样图
def checker_bg(size, block=16):
    """棋盘底：用来证明 tile 是透明的（8× 下每格 2 个源像素）。"""
    im = Image.new("RGBA", (size, size), (0x2A, 0x26, 0x22, 255))
    px = im.load()
    for y in range(size):
        for x in range(size):
            if ((x // block) + (y // block)) % 2:
                px[x, y] = (0x3A, 0x34, 0x2E, 255)
    return im


def sample_rows():
    """4×4 = 16 张：4 方向 + 4 状态 + 3 温度 + 4 成分 + 1 动效帧，全部互不重复。"""
    water = "water|normal|large|flow|right"
    return [
        ("方向 dir", [(f"water|normal|large|flow|{d}", d) for d in DIRS]),
        ("状态 state", [(f"water|normal|large|{s}|right", s)
                        for s in ("blocked", "split", "merge", "collect")]),
        ("温度 temp", [(f"poison|{t}|large|flow|right", lab) for t, lab in
                       (("cold", "cold ≤5℃"), ("normal", "normal 21℃"), ("hot", "hot ≥60℃"))]
         + [(water + "|f6", "flow f6")]),
        ("成分 comp", [(f"{c}|normal|mid|flow|right", c) for c in ("water", "poison", "oil", "blood")]),
    ]


def make_sample(atlas, index):
    rows = sample_rows()
    cols = max(len(cells) for _, cells in rows)
    scale = 8
    cell = TILE * scale
    gap, pad, rowlab, head, rowh, foot = 10, 20, 132, 70, cell + 26, 52
    width = pad * 2 + rowlab + cols * cell + (cols - 1) * gap
    height = pad + head + len(rows) * rowh + foot + pad
    im = Image.new("RGBA", (width, height), rf.BG + (255,))
    d = ImageDraw.Draw(im)
    d.text((pad, pad), "运行时图集抽样 · 4×4", font=rf.F(20, True), fill=rf.INK + (255,))
    d.text((pad, pad + 30),
           f"直接裁自 assets/flow_atlas.png（{atlas.width}×{atlas.height} / {len(index)} tile）"
           f" · {scale}× NEAREST · 棋盘底 = alpha 0",
           font=rf.F(12), fill=rf.INK2 + (255,))

    checker = checker_bg(cell)
    y = pad + head
    for row_label, cells in rows:
        d.text((pad, y + cell // 2 - 8), row_label, font=rf.F(14, True), fill=rf.INK2 + (255,))
        x = pad + rowlab
        for key, caption in cells:
            assert key in index, f"抽样 key 不在索引里：{key}"
            bx, by = index[key]
            tile = atlas.crop((bx, by, bx + TILE, by + TILE))
            im.alpha_composite(checker, (x, y))
            im.alpha_composite(tile.resize((cell, cell), Image.NEAREST), (x, y))
            d.text((x + cell // 2, y + cell + 5), caption,
                   font=rf.F(12), fill=rf.INK + (255,), anchor="ma")
            x += cell + gap
        y += rowh

    d.line([(pad, y + 4), (width - pad, y + 4)], fill=rf.GRID + (255,))
    d.text((pad, y + 12),
           f"静态 {STATIC_COUNT} + 动效 {DYNAMIC_COUNT} = {TOTAL} tile · tile {TILE}×{TILE} · "
           "key 全小写、| 分隔（见 assets/flow_atlas.json）",
           font=rf.F(12), fill=rf.INK2 + (255,))
    d.text((pad, y + 30), "射程点不进图集：运行时用 3 个 1×1 白点 Image 着色叠加。",
           font=rf.F(12), fill=rf.INK2 + (255,))
    im.convert("RGB").save(SAMPLE, format="PNG")


# ---------------------------------------------------------------- main
def sha256_of(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    assert TOTAL == 4896, TOTAL
    print(f"keys static={STATIC_COUNT} dynamic={DYNAMIC_COUNT} total={TOTAL} "
          f"layout={PER_ROW * TILE}x{ROWS * TILE}")

    atlas, index = build_atlas()

    ASSETS.mkdir(parents=True, exist_ok=True)
    atlas.save(ATLAS_PNG, format="PNG")
    payload = {"tiles": index}
    ATLAS_JSON.write_bytes(
        json.dumps(payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8"))
    SAMPLE.parent.mkdir(parents=True, exist_ok=True)
    make_sample(atlas, index)

    png_size = ATLAS_PNG.stat().st_size
    json_size = ATLAS_JSON.stat().st_size
    sample_size = SAMPLE.stat().st_size
    print(f"atlas  {atlas.width}x{atlas.height} {atlas.mode} tiles={len(index)}")
    print(f"png    assets/flow_atlas.png  {png_size} bytes  sha256={sha256_of(ATLAS_PNG)}")
    print(f"json   assets/flow_atlas.json {json_size} bytes  sha256={sha256_of(ATLAS_JSON)}")
    print(f"sample docs/flow-visual/final/png/runtime-atlas-sample.png  {sample_size} bytes  "
          f"sha256={sha256_of(SAMPLE)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
