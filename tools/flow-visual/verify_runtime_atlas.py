#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""verify_runtime_atlas.py — 运行时图集自检（索引契约 + 与渲染器逐像素对拍）。

1) 索引：
   - 条数 4896（静态 1440 + 动效 3456）；
   - key 唯一（按原始 JSON 检测重复键）、全小写、| 分隔、枚举值合法；
   - key 集合与 IMPL-PLAN-A §1 契约完全一致（无缺、无多）；
   - 坐标是整数、16 对齐、在界内，且 4896 个槽位互不重叠。
2) 像素：
   - 固定 seed 随机抽 20 个 key，从图集裁出 tile；
   - 用 render_final.build_grid + role_rgba 独立重画同一 tile（不复用构建脚本的代码），
     逐像素（含 alpha）比较，输出 MATCH 20/20。
3) 附加：动效段确实不是静态复用（12 帧至少 6 种不同 tile）。

枚举表按 IMPL-PLAN-A §1 独立抄写，不 import 构建脚本，避免两边一起错。

用法：
    python tools/flow-visual/verify_runtime_atlas.py
    python tools/flow-visual/verify_runtime_atlas.py --seed 20261020 --samples 20
"""
import argparse
import json
import random
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent.parent
FINAL = ROOT / "docs/flow-visual/final"
ATLAS_PNG = ROOT / "assets/flow_atlas.png"
ATLAS_JSON = ROOT / "assets/flow_atlas.json"

sys.path.insert(0, str(FINAL))
sys.dont_write_bytecode = True  # 不在 final/ 下留 __pycache__
import render_final as rf  # noqa: E402

TILE = 16
PER_ROW = 16

COMPS = ("water", "poison", "oil", "blood", "acid", "energy")
TEMPS = ("cold", "normal", "hot")
CONTENT = ("empty", "small", "mid", "large")
STATES = ("flow", "blocked", "split", "merge", "collect")
DIRS = ("up", "right", "down", "left")
FRAME_COUNT = 12

EXPECTED_STATIC = len(COMPS) * len(TEMPS) * len(CONTENT) * len(STATES) * len(DIRS)
EXPECTED_DYNAMIC = len(COMPS) * len(TEMPS) * len(CONTENT) * len(DIRS) * FRAME_COUNT
EXPECTED_TOTAL = EXPECTED_STATIC + EXPECTED_DYNAMIC
EXPECTED_ROWS = (EXPECTED_TOTAL + PER_ROW - 1) // PER_ROW


def expected_keys():
    """按契约重建全部 key，用来做集合比对。"""
    keys = set()
    for comp in COMPS:
        for temp in TEMPS:
            for content in CONTENT:
                for state in STATES:
                    for dirn in DIRS:
                        keys.add(f"{comp}|{temp}|{content}|{state}|{dirn}")
    for comp in COMPS:
        for temp in TEMPS:
            for content in CONTENT:
                for dirn in DIRS:
                    for frame in range(FRAME_COUNT):
                        keys.add(f"{comp}|{temp}|{content}|flow|{dirn}|f{frame}")
    return keys


def render_tile(key):
    """独立重画：与构建脚本同契约，但代码路径分开，能抓构建脚本的错。"""
    parts = key.split("|")
    if len(parts) == 5:
        comp, temp, content, state, dirn = parts
        frame = None
    else:
        comp, temp, content, _flow, dirn, fpart = parts
        state = "flow"
        frame = int(fpart[1:])
    g = rf.build_grid(comp, content, state, dirn, temp, frame)
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


def pixel_diff(a, b):
    """返回 (是否逐像素相同, 不同像素数)。"""
    d = np.abs(np.asarray(a, dtype=np.int16) - np.asarray(b, dtype=np.int16))
    bad = int(np.count_nonzero(d.any(axis=2)))
    return bad == 0, bad


def load_index():
    """读 json，同时抓重复 key（json 默认会静默覆盖）。"""
    raw = ATLAS_JSON.read_bytes()
    problems = []
    if raw.startswith(b"\xef\xbb\xbf"):
        problems.append("json 带 UTF-8 BOM")
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError as e:
        return None, [f"json 不是 UTF-8：{e}"]

    dups = []

    def hook(pairs):
        seen = set()
        out = {}
        for k, v in pairs:
            if k in seen:
                dups.append(k)
            seen.add(k)
            out[k] = v
        return out

    try:
        obj = json.loads(text, object_pairs_hook=hook)
    except json.JSONDecodeError as e:
        return None, [f"json 解析失败：{e}"]
    if not isinstance(obj, dict) or "tiles" not in obj:
        return None, ["json 顶层缺少 tiles"]

    for k in dups:
        problems.append(f"重复 key：{k}")
    return obj["tiles"], problems


def check_index(index):
    """返回 (fails, 统计信息)。"""
    fails = []
    keys = list(index.keys())
    if len(keys) != EXPECTED_TOTAL:
        fails.append(f"tile 条数 {len(keys)} != {EXPECTED_TOTAL}")

    exp = expected_keys()
    missing = exp - set(keys)
    extra = set(keys) - exp
    if missing:
        fails.append(f"缺 {len(missing)} 个契约 key，例如 {sorted(missing)[:3]}")
    if extra:
        fails.append(f"多 {len(extra)} 个非法 key，例如 {sorted(extra)[:3]}")

    slots = set()
    for key, pos in index.items():
        if key != key.lower():
            fails.append(f"key 不是全小写：{key}")
        parts = key.split("|")
        if len(parts) not in (5, 6):
            fails.append(f"key 段数 {len(parts)}：{key}")
        elif len(parts) == 5:
            comp, temp, content, state, dirn = parts
            if (comp not in COMPS or temp not in TEMPS or content not in CONTENT
                    or state not in STATES or dirn not in DIRS):
                fails.append(f"静态 key 枚举非法：{key}")
        else:
            comp, temp, content, flow, dirn, fpart = parts
            if (comp not in COMPS or temp not in TEMPS or content not in CONTENT
                    or flow != "flow" or dirn not in DIRS):
                fails.append(f"动效 key 枚举非法：{key}")
            elif not (fpart.startswith("f") and fpart[1:].isdigit()
                      and 0 <= int(fpart[1:]) < FRAME_COUNT):
                fails.append(f"动效 key 帧号非法：{key}")
        if not isinstance(pos, list) or len(pos) != 2:
            fails.append(f"坐标格式非法：{key} -> {pos!r}")
            continue
        x, y = pos
        if not (isinstance(x, int) and isinstance(y, int)):
            fails.append(f"坐标不是整数：{key} -> {pos!r}")
            continue
        if x < 0 or y < 0 or x % TILE or y % TILE:
            fails.append(f"坐标越界/未对齐：{key} -> {pos}")
            continue
        if x + TILE > PER_ROW * TILE or y + TILE > EXPECTED_ROWS * TILE:
            fails.append(f"坐标越界：{key} -> {pos}")
            continue
        if (x, y) in slots:
            fails.append(f"槽位重叠：{key} -> {pos}")
        slots.add((x, y))

    stats = {
        "count": len(keys),
        "unique_slots": len(slots),
        "static": sum(1 for k in keys if len(k.split("|")) == 5),
        "dynamic": sum(1 for k in keys if len(k.split("|")) == 6),
    }
    if stats["unique_slots"] != EXPECTED_TOTAL:
        fails.append(f"唯一槽位 {stats['unique_slots']} != {EXPECTED_TOTAL}")
    return fails, stats


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seed", type=int, default=20261020, help="抽样随机种子（固定才有可比性）")
    ap.add_argument("--samples", type=int, default=20, help="抽样 tile 数")
    args = ap.parse_args()

    fails = []
    print(f"atlas  {ATLAS_PNG.relative_to(ROOT).as_posix()}")
    if not ATLAS_PNG.exists():
        print("FAIL 图集不存在，先跑 build_runtime_atlas.py")
        return 1
    atlas = Image.open(ATLAS_PNG)
    atlas.load()
    print(f"       {atlas.width}x{atlas.height} {atlas.mode}")
    if atlas.mode != "RGBA":
        fails.append(f"图集不是 RGBA：{atlas.mode}")
    if (atlas.width, atlas.height) != (PER_ROW * TILE, EXPECTED_ROWS * TILE):
        fails.append(f"图集尺寸 {atlas.width}x{atlas.height} != "
                     f"{PER_ROW * TILE}x{EXPECTED_ROWS * TILE}")

    index, json_problems = load_index()
    fails.extend(json_problems)
    if index is None:
        print("FAIL " + "；".join(fails))
        return 1

    idx_fails, stats = check_index(index)
    fails.extend(idx_fails)
    print(f"index  {stats['count']} tiles (static={stats['static']} dynamic={stats['dynamic']}) "
          f"slots={stats['unique_slots']}  "
          f"{'OK' if not idx_fails else 'FAIL'}")

    keys = sorted(index)
    rng = random.Random(args.seed)
    picked = rng.sample(keys, min(args.samples, len(keys)))
    print(f"seed={args.seed} samples={len(picked)}  "
          f"(static={sum(1 for k in picked if len(k.split('|')) == 5)} "
          f"dynamic={sum(1 for k in picked if len(k.split('|')) == 6)})")

    matched = 0
    for key in picked:
        x, y = index[key]
        crop = atlas.crop((x, y, x + TILE, y + TILE))
        exp = render_tile(key)
        same, bad = pixel_diff(crop, exp)
        if same:
            matched += 1
            print(f"  MATCH {key}")
        else:
            fails.append(f"像素不一致 {key} @ ({x},{y})：{bad} px")
            print(f"  DIFF  {key} @ ({x},{y})：{bad} px")
    print(f"MATCH {matched}/{len(picked)}")

    # 动效段自检：同一 comp/temp/content/dir 的 12 帧不能是同一张图。
    def crop_at(key):
        x, y = index[key]
        return atlas.crop((x, y, x + TILE, y + TILE))

    frame_tiles = [np.asarray(crop_at(f"water|normal|large|flow|right|f{i}")).tobytes()
                   for i in range(FRAME_COUNT)]
    distinct = len(set(frame_tiles))
    if distinct < 6:
        fails.append(f"动效帧几乎不变：12 帧只有 {distinct} 种")
    print(f"frames     water|normal|large|flow|right distinct tiles={distinct}/{FRAME_COUNT} "
          f"{'OK' if distinct >= 6 else 'FAIL'}")

    if fails:
        print("FAIL")
        for msg in fails:
            print("  - " + msg)
        return 1
    print("OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
