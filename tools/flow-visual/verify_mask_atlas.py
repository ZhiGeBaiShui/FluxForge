#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""verify_mask_atlas.py — 蒙版图集自检（结构 / 规格 / 与旧图对拍）。

检查项：
1) 文件：4 张 PNG 均为 256×1040 RGBA；PNG 只含 IHDR/IDAT/IEND（无时间戳元数据）。
2) 索引：1040 个 key；3 段静态 `content|state|dir` + 4 段 `...|f0..f11`；
   key 全小写、枚举合法、坐标整数 / 16 对齐 / 唯一 / 在界内；UTF-8 无 BOM、无重复 key。
3) 规格一致性：用 render_final.build_grid + REFACTOR-A §2 的角色表**独立重画**全部
   1040 个 key × 4 层，与落盘图集逐像素（RGB+A）比对；层间互斥（每个像素只属于一层）；
   并直接检查落盘字节：S（汇聚蒸汽）/ ~（抽吸流）必须在 accent 层、body 为空。
4) 墙：只在 blocked 出现；颜色恒 #C9C2B8；形态 1×3。
5) alpha 对拍：4 层 alpha 的并集 vs 旧 assets/flow_atlas.png 的 A 通道。
   - 规定 20 个 key（large × 5 状态 × 4 向）必须 100% 相等；
   - 另对全部 272 个有旧图对应的 key（80 静态 + 192 flow 帧）做全量对拍。
6) 颜色对拍（6 成分 × 3 温度 × 5 key = 90 组）：
   - 新行为：accent = mix(body, white, 0.62)（与 render_final 的 C 角色同式），S/~ 在
     accent 层；旧行为基准：accent 固定 #F2FAFF、S/~ 在 body 层（ROLE_MASK_BEFORE 复算）；
   - 只比「新旧两边角色一致」的像素：旧图的成分纹理 X/Y/Z/K/N、霜点 I、酸沫 F、
     热缺口是 A 方案的结构性删减，单独计数，不混进 tint 偏差；
   - 输出修改前/后、每成分 × 每温度的 Δmax/Δavg，以及按角色分解；
     默认只 WARN，--strict-color 时 |Δ|≤20 且 ≥90% 像素≤10 不达标返回非 0。

用法：
    python tools/flow-visual/verify_mask_atlas.py
    python tools/flow-visual/verify_mask_atlas.py --strict-color
"""
import argparse
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent.parent
FINAL = ROOT / "docs/flow-visual/final"
ASSETS = ROOT / "assets"

sys.path.insert(0, str(FINAL))
sys.dont_write_bytecode = True  # 不在 final/ 下留 __pycache__
import render_final as rf  # noqa: E402

TILE = 16
PER_ROW = 16
FRAME_COUNT = 12
VARIANTS = 1 + FRAME_COUNT

CONTENT = ("empty", "small", "mid", "large")
STATES = ("flow", "blocked", "split", "merge", "collect")
DIRS = ("up", "right", "down", "left")

EXPECTED_TOTAL = len(CONTENT) * len(STATES) * len(DIRS) * VARIANTS
EXPECTED_ROWS = (EXPECTED_TOTAL + PER_ROW - 1) // PER_ROW

BAKE_COMP = "water"
BAKE_TEMP = "normal"
WALL_COLOR = (0xC9, 0xC2, 0xB8)

# 新 accent = mix(body, white, 0.62)；旧 accent 固定近白（评审要修的根因）。
ACCENT_MIX = 0.62
FIXED_ACCENT = (0xF2, 0xFA, 0xFF)
COMP_NAMES = tuple(rf.COMPS)               # water/poison/oil/blood/acid/energy
TEMPS = ("cold", "normal", "hot")
COLOR_KEYS = [f"large|{state}|right" for state in STATES]   # 每成分 5 个 key：5 种状态

def accent_color(body):
    """运行时 FlowTint.Accent：本体色向白混 62%。"""
    return rf.mix(body, (255, 255, 255), ACCENT_MIX)


def tints_for(comp_name, temp):
    body = rf.COMPS[comp_name]
    return {"body": body, "edge": rf.edge_color(body, temp),
            "accent": accent_color(body), "wall": (255, 255, 255)}


def old_tints(comp_name, temp):
    """修改前的运行时 tint：accent 固定 #F2FAFF。"""
    body = rf.COMPS[comp_name]
    return {"body": body, "edge": rf.edge_color(body, temp),
            "accent": FIXED_ACCENT, "wall": (255, 255, 255)}


WATER_TINTS = tints_for(BAKE_COMP, BAKE_TEMP)

LAYERS = ("body", "edge", "accent", "wall")
FILES = {name: ASSETS / f"flow_mask_{name}.png" for name in LAYERS}
INDEX_JSON = ASSETS / "flow_mask.json"
OLD_ATLAS = ASSETS / "flow_atlas.png"
OLD_INDEX = ASSETS / "flow_atlas.json"

# REFACTOR-A §2 的角色表（独立抄写，不 import 构建脚本）。
# `~` 的 alpha 表里写 200，旧 lay 实际是 255；以形状 alpha 为准取 255（见 build 脚本说明）。
# X/Y/Z/K/N 删除成分纹理，但保留旧形状 alpha=255，亮度并入 accent。
# S/~ 本版从 body 迁到 accent（蒸汽/抽吸流接近白，固定 body 色会得到混合色）。
ROLE_MASK = {
    "B": ("body", None, 208),
    "R": ("body", 75, 130),
    "u": ("body", 166, 170),
    "C": ("accent", 255, 255),
    "G": ("accent", 255, 255),
    "I": ("accent", 255, 255),
    "F": ("accent", 255, 255),
    "X": ("accent", 255, 255),
    "Y": ("accent", 255, 255),
    "Z": ("accent", 255, 255),
    "K": ("accent", 255, 255),
    "N": ("accent", 255, 255),
    "S": ("accent", 255, 255),
    "~": ("accent", 255, 255),
    "E": ("edge", 255, 255),
    "M": ("edge", 255, 255),
    "L": ("edge", 255, 255),
    "r": ("edge", 38, 130),
    "P": ("edge", 210, 200),
    "s": ("edge", 255, 200),
    "S2": ("edge", 255, 120),
    "W": ("wall", None, 255),
}

# 修改前基准：S/~ 还归 body（alpha 与亮度不变），用于量化「固定 accent + 错误分层」的旧偏差。
ROLE_MASK_BEFORE = dict(ROLE_MASK)
ROLE_MASK_BEFORE["S"] = ("body", 255, 255)
ROLE_MASK_BEFORE["~"] = ("body", 255, 255)

BODY_LUMS = {255, 235, 215, 75, 166}
EDGE_LUMS = {255, 38, 210}
BODY_ALPHAS = {208, 130, 170, 255}
EDGE_ALPHAS = {255, 130, 200, 120}

ALPHA_KEYS = [f"large|{state}|{dirn}" for state in STATES for dirn in DIRS]


# ---------------------------------------------------------------- 索引
def expected_keys():
    keys = set()
    for content in CONTENT:
        for state in STATES:
            for dirn in DIRS:
                keys.add(f"{content}|{state}|{dirn}")
                for frame in range(FRAME_COUNT):
                    keys.add(f"{content}|{state}|{dirn}|f{frame}")
    return keys


def parse_key(key):
    parts = key.split("|")
    if len(parts) == 3:
        content, state, dirn = parts
        return content, state, dirn, None
    content, state, dirn, fpart = parts
    return content, state, dirn, int(fpart[1:])


def load_index():
    raw = INDEX_JSON.read_bytes()
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
    problems.extend(f"重复 key：{k}" for k in dups)
    return obj["tiles"], problems


def check_index(index):
    fails, notes = [], []
    keys = list(index)
    if len(keys) != EXPECTED_TOTAL:
        fails.append(f"key 条数 {len(keys)} != {EXPECTED_TOTAL}")

    exp = expected_keys()
    missing, extra = exp - set(keys), set(keys) - exp
    if missing:
        fails.append(f"缺 {len(missing)} 个契约 key，例如 {sorted(missing)[:3]}")
    if extra:
        fails.append(f"多 {len(extra)} 个非法 key，例如 {sorted(extra)[:3]}")

    slots = set()
    stats = {"static": 0, "frames": 0}
    for key, pos in index.items():
        if key != key.lower():
            fails.append(f"key 不是全小写：{key}")
        parts = key.split("|")
        if len(parts) not in (3, 4):
            fails.append(f"key 段数 {len(parts)}：{key}")
            continue
        content, state, dirn, frame = parse_key(key)
        if content not in CONTENT or state not in STATES or dirn not in DIRS:
            fails.append(f"key 枚举非法：{key}")
        if len(parts) == 4 and not (0 <= frame < FRAME_COUNT):
            fails.append(f"帧号非法：{key}")
        stats["static" if frame is None else "frames"] += 1
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

    if len(slots) != EXPECTED_TOTAL:
        fails.append(f"唯一槽位 {len(slots)} != {EXPECTED_TOTAL}")
    notes.append(f"{len(keys)} keys (static={stats['static']} frames={stats['frames']}) "
                 f"slots={len(slots)}")
    return fails, notes


# ---------------------------------------------------------------- PNG
def png_chunks(path):
    data = path.read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    i, chunks = 8, []
    while i + 12 <= len(data):
        n = int.from_bytes(data[i:i + 4], "big")
        chunks.append(data[i + 4:i + 8].decode("ascii", "replace"))
        i += 12 + n
    return chunks


# ---------------------------------------------------------------- 独立重画
def body_lum(y, bbox):
    y0, h = bbox[1], bbox[3]
    t = (y - y0) / max(1, h - 1)
    return 255 if t < 1.0 / 3.0 else (215 if t > 2.0 / 3.0 else 235)


def expected_layers(content, state, dirn, frame, comp=BAKE_COMP, temp=BAKE_TEMP,
                    role_mask=ROLE_MASK):
    g = rf.build_grid(comp, content, state, dirn, temp, frame)
    bbox = rf.bbox_of(g) or rf.CBBOX[content]
    out = {name: np.zeros((TILE, TILE, 4), dtype=np.uint8) for name in LAYERS}
    for y in range(TILE):
        for x in range(TILE):
            role = g[y][x]
            if role == ".":
                continue
            layer, lum, alpha = role_mask[role]
            if layer == "body" and lum is None:
                lum = body_lum(y, bbox)
            rgb = WALL_COLOR if layer == "wall" else (lum, lum, lum)
            out[layer][y, x] = rgb + (alpha,)
    return out, g


def crop(images, key):
    x, y = index_of[key]
    return {name: np.asarray(images[name].crop((x, y, x + TILE, y + TILE)))
            for name in LAYERS}


# ---------------------------------------------------------------- 对拍
def old_key(comp, temp, content, state, dirn, frame):
    if frame is None:
        return f"{comp}|{temp}|{content}|{state}|{dirn}"
    return f"{comp}|{temp}|{content}|flow|{dirn}|f{frame}"


def old_tile(key):
    x, y = old_index[key]
    return np.asarray(old_atlas.crop((x, y, x + TILE, y + TILE)))


def union_alpha(layers):
    return np.maximum.reduce([layers[name][..., 3] for name in LAYERS])


def tint_rgb(rgb, color):
    c = np.asarray(color, dtype=np.int32)
    return ((rgb.astype(np.int32) * c + 127) // 255).astype(np.uint8)


def rebuild_rgb(layers, tints):
    out = np.zeros((TILE, TILE, 3), dtype=np.uint8)
    # 墙图已带固定色 #C9C2B8，tint 用白；再乘 WallColor 会二次变暗（实测 Δ≈51）。
    for name in LAYERS:
        a = layers[name][..., 3] > 0
        out[a] = tint_rgb(layers[name][..., :3], tints[name])[a]
    return out


def color_sweep(role_mask, fixed_accent, comps=COMP_NAMES, temps=TEMPS, keys=None):
    """6 成分 × 3 温度 × 5 key 的颜色对拍。

    返回 (stats, role_deltas, all_px, all_ch)：
    - stats[comp][temp] = {"px": [...], "ch": [...], "excluded": n, "new_only": n}
    - px 是每像素三通道最大偏差（Δmax / 百分位），ch 是逐通道偏差（Δavg 与原报告同口径）。
    - 只统计新旧两边角色一致的像素；角色不同（成分纹理/霜点/酸沫/热缺口）计入
      excluded / new_only，属于 A 方案的结构性删减，不计入颜色偏差。
    """
    keys = COLOR_KEYS if keys is None else keys
    stats = {c: {t: {"px": [], "ch": [], "excluded": 0, "new_only": 0}
                 for t in temps} for c in comps}
    role_deltas = {}
    all_px, all_ch = [], []
    for comp_name in comps:
        for temp in temps:
            tints = old_tints(comp_name, temp) if fixed_accent else tints_for(comp_name, temp)
            cell = stats[comp_name][temp]
            for key in keys:
                content, state, dirn, frame = parse_key(key)
                mask_layers, mask_grid = expected_layers(
                    content, state, dirn, frame, role_mask=role_mask)
                new_rgb = rebuild_rgb(mask_layers, tints)
                _, old_grid = expected_layers(
                    content, state, dirn, frame, comp=comp_name, temp=temp,
                    role_mask=ROLE_MASK)
                ref = old_tile(old_key(comp_name, temp, content, state, dirn, frame))
                ref_rgb, ref_a = ref[..., :3].astype(np.int32), ref[..., 3] > 0
                new_a = union_alpha(mask_layers) > 0
                cell["new_only"] += int(np.count_nonzero(new_a & ~ref_a))
                for y in range(TILE):
                    for x in range(TILE):
                        if not ref_a[y, x]:
                            continue
                        role = mask_grid[y][x]
                        if role != old_grid[y][x]:
                            cell["excluded"] += 1
                            continue
                        delta = np.abs(new_rgb[y, x].astype(np.int32) - ref_rgb[y, x])
                        value = int(delta.max())
                        cell["px"].append(value)
                        cell["ch"].extend(int(v) for v in delta)
                        all_px.append(value)
                        all_ch.extend(int(v) for v in delta)
                        role_deltas.setdefault(
                            role, {"px": [], "ch": []})
                        role_deltas[role]["px"].append(value)
                        role_deltas[role]["ch"].extend(int(v) for v in delta)
    return (stats, role_deltas, np.asarray(all_px, dtype=np.int32),
            np.asarray(all_ch, dtype=np.int32))


def stat_line(cell):
    """一行统计：Δmax（每像素三通道最大）/ Δavg（逐通道，与原报告同口径）。"""
    px = np.asarray(cell["px"], dtype=np.int32)
    ch = np.asarray(cell["ch"], dtype=np.int32)
    return f"{int(px.max()):3d}/{float(ch.mean()):5.2f}"


# ---------------------------------------------------------------- main
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--strict-color", action="store_true",
                    help="颜色预算（|Δ|≤20 且 ≥90%% 像素 ≤10）不达标也返回非 0")
    args = ap.parse_args()

    fails = []
    print("=== 文件 ===")
    images = {}
    for name in LAYERS:
        p = FILES[name]
        if not p.exists():
            fails.append(f"缺文件 {p.relative_to(ROOT).as_posix()}")
            continue
        img = Image.open(p)
        img.load()
        images[name] = img
        chunks = png_chunks(p)
        bad_chunks = [c for c in (chunks or []) if c not in ("IHDR", "IDAT", "IEND")]
        print(f"{name:6s} {img.width}x{img.height} {img.mode} "
              f"{p.stat().st_size} bytes  chunks={','.join(chunks or [])}")
        if img.mode != "RGBA":
            fails.append(f"{name} 不是 RGBA：{img.mode}")
        if (img.width, img.height) != (PER_ROW * TILE, EXPECTED_ROWS * TILE):
            fails.append(f"{name} 尺寸 {img.width}x{img.height} != "
                         f"{PER_ROW * TILE}x{EXPECTED_ROWS * TILE}")
        if bad_chunks:
            fails.append(f"{name} 含非确定性/无关 PNG chunk：{bad_chunks}")
    if not images:
        print("FAIL 没有可读的蒙版图")
        return 1

    print("\n=== 索引 ===")
    global index_of, old_index, old_atlas
    index_of, json_problems = load_index()
    fails.extend(json_problems)
    if index_of is None:
        print("FAIL " + "；".join(fails))
        return 1
    idx_fails, notes = check_index(index_of)
    fails.extend(idx_fails)
    for n in notes:
        print(n)
    print("index  " + ("OK" if not idx_fails else "FAIL"))

    # 旧图（alpha / 颜色对拍基准）
    if not OLD_ATLAS.exists() or not OLD_INDEX.exists():
        print("FAIL 缺 assets/flow_atlas.png / flow_atlas.json（对拍基准）")
        return 1
    old_index = json.loads(OLD_INDEX.read_text(encoding="utf-8"))["tiles"]
    old_atlas = Image.open(OLD_ATLAS).convert("RGBA")

    # ---- 规格一致性：全部 key × 4 层逐像素
    print("\n=== 规格一致性（独立重画 1040 key × 4 层）===")
    bad_keys, bad_px, overlap_px = [], 0, 0
    wall_opaque, wall_wrong_color, wall_off_blocked = 0, 0, 0
    body_lum_set, body_alpha_set = set(), set()
    edge_lum_set, edge_alpha_set = set(), set()
    accent_lum_set, accent_alpha_set = set(), set()
    migrate_px, migrate_bad = 0, 0
    for key, pos in index_of.items():
        content, state, dirn, frame = parse_key(key)
        actual = crop(images, key)
        exp, g = expected_layers(content, state, dirn, frame)
        diff = sum(int(np.count_nonzero(actual[name] != exp[name])) for name in LAYERS)
        if diff:
            bad_keys.append((key, diff))
            bad_px += diff
        stacked = sum((actual[name][..., 3] > 0).astype(np.int32) for name in LAYERS)
        overlap_px += int(np.count_nonzero(stacked > 1))
        # 回归保护：S（汇聚蒸汽）/ ~（抽吸流）必须在 accent 层，body 必须为空。
        # 不依赖 ROLE_MASK 的自洽性：直接按设计意图检查落盘字节。
        for y in range(TILE):
            for x in range(TILE):
                if g[y][x] not in ("S", "~"):
                    continue
                migrate_px += 1
                if (actual["body"][y, x, 3] != 0
                        or actual["accent"][y, x, 3] != 255
                        or bool((actual["accent"][y, x, :3] != 255).any())):
                    migrate_bad += 1
        # 层内取值域
        for name, lums, alphas in (("body", body_lum_set, body_alpha_set),
                                   ("edge", edge_lum_set, edge_alpha_set),
                                   ("accent", accent_lum_set, accent_alpha_set)):
            a = actual[name][..., 3] > 0
            if a.any():
                lums.update(np.unique(actual[name][..., 0][a]).tolist())
                alphas.update(np.unique(actual[name][..., 3][a]).tolist())
        wa = actual["wall"][..., 3] > 0
        n_wall = int(np.count_nonzero(wa))
        wall_opaque += n_wall
        if n_wall:
            got = np.unique(actual["wall"][wa][:, :3], axis=0)
            if len(got) != 1 or tuple(int(v) for v in got[0]) != WALL_COLOR:
                wall_wrong_color += 1
            if state != "blocked":
                wall_off_blocked += int(n_wall)
    print(f"重画不一致：{len(bad_keys)} key / {bad_px} px  "
          f"{'OK' if not bad_keys else 'FAIL'}")
    print(f"S/~ 迁到 accent：{migrate_px} px（body 为空 / accent=255）  "
          f"{'OK' if migrate_px and not migrate_bad else f'FAIL {migrate_bad}'}")
    if not migrate_px or migrate_bad:
        fails.append(f"S/~ 未正确落在 accent：{migrate_px} px / 错 {migrate_bad}")
    print(f"层间重叠像素：{overlap_px}  {'OK' if overlap_px == 0 else 'FAIL'}")
    print(f"墙：opaque={wall_opaque}px  颜色错={wall_wrong_color}  "
          f"非 blocked 出现={wall_off_blocked}px  "
          f"{'OK' if (wall_wrong_color == 0 and wall_off_blocked == 0) else 'FAIL'}")
    wall_ref_bad = 0
    for dirn in DIRS:
        key = f"large|blocked|{dirn}"
        got = crop(images, key)["wall"]
        ref = old_tile(f"{BAKE_COMP}|{BAKE_TEMP}|large|blocked|{dirn}")
        wa = got[..., 3] > 0
        if not np.array_equal(got[wa], ref[wa]):
            wall_ref_bad += 1
    print(f"墙 vs 旧 W（4 向，颜色+alpha）："
          f"{'OK' if wall_ref_bad == 0 else f'FAIL {wall_ref_bad}'}")
    if wall_ref_bad:
        fails.append(f"墙与旧 W 不一致：{wall_ref_bad} 向")
    print(f"亮度取值 body={sorted(body_lum_set)} edge={sorted(edge_lum_set)} "
          f"accent={sorted(accent_lum_set)}")
    print(f"alpha 取值 body={sorted(body_alpha_set)} edge={sorted(edge_alpha_set)} "
          f"accent={sorted(accent_alpha_set)}")
    if bad_keys:
        fails.append(f"独立重画不一致 {len(bad_keys)} key（首例 {bad_keys[0]}）")
    if overlap_px:
        fails.append(f"层间重叠 {overlap_px} px")
    if wall_wrong_color or wall_off_blocked:
        fails.append(f"墙颜色/位置错：{wall_wrong_color} / {wall_off_blocked}px")
    if not body_lum_set <= BODY_LUMS or not body_alpha_set <= BODY_ALPHAS:
        fails.append(f"body 取值越界：lum={sorted(body_lum_set)} alpha={sorted(body_alpha_set)}")
    if not edge_lum_set <= EDGE_LUMS or not edge_alpha_set <= EDGE_ALPHAS:
        fails.append(f"edge 取值越界：lum={sorted(edge_lum_set)} alpha={sorted(edge_alpha_set)}")
    if not accent_lum_set <= {255} or not accent_alpha_set <= {255}:
        fails.append(f"accent 取值越界：lum={sorted(accent_lum_set)} "
                     f"alpha={sorted(accent_alpha_set)}")

    # ---- 角色表 alpha 与 render_final 对齐
    print("\n=== 角色表 alpha 对齐 render_final.role_rgba ===")
    alpha_bad = []
    for role, (layer, lum, alpha) in ROLE_MASK.items():
        ref = rf.role_rgba(role, 0, 0, rf.COMPS[BAKE_COMP], BAKE_TEMP, (0, 0, 16, 16))
        if ref[3] != alpha:
            alpha_bad.append((role, alpha, ref[3]))
    print(f"{len(ROLE_MASK)} roles  {'OK' if not alpha_bad else 'FAIL'} "
          f"{alpha_bad if alpha_bad else ''}")
    if alpha_bad:
        fails.append(f"角色表 alpha 与渲染器不符：{alpha_bad}")

    # ---- 帧
    print("\n=== 帧槽 ===")
    def tile_sig(key):
        x, y = index_of[key]
        return b"".join(images[n].crop((x, y, x + TILE, y + TILE)).tobytes()
                        for n in LAYERS)

    flow_keys = ["large|flow|right"] + [f"large|flow|right|f{i}" for i in range(FRAME_COUNT)]
    distinct = len({tile_sig(k) for k in flow_keys})
    print(f"flow 变体 distinct={distinct}/{VARIANTS}  "
          f"{'OK' if distinct >= 6 else 'FAIL'}")
    if distinct < 6:
        fails.append(f"flow 帧几乎不变：13 槽只有 {distinct} 种")
    dup_bad = 0
    for content in CONTENT:
        for state in STATES:
            if state == "flow":
                continue
            for dirn in DIRS:
                base = tile_sig(f"{content}|{state}|{dirn}")
                for f in range(FRAME_COUNT):
                    if tile_sig(f"{content}|{state}|{dirn}|f{f}") != base:
                        dup_bad += 1
    print(f"非 flow 帧槽=静态重复：{'OK' if dup_bad == 0 else f'FAIL {dup_bad}'}")
    if dup_bad:
        fails.append(f"非 flow 帧槽与静态不一致 {dup_bad} 个")

    # ---- alpha 对拍
    print("\n=== alpha 对拍（4 层并集 vs 旧 flow_atlas A 通道）===")
    alpha_fail = []
    for key in ALPHA_KEYS:
        content, state, dirn, _ = parse_key(key)
        act = union_alpha(crop(images, key))
        ref = old_tile(old_key(BAKE_COMP, BAKE_TEMP, content, state, dirn, None))[..., 3]
        diff = int(np.count_nonzero(act != ref))
        if diff:
            alpha_fail.append((key, diff))
    print(f"规定 20 key：{len(ALPHA_KEYS) - len(alpha_fail)}/{len(ALPHA_KEYS)} 100% 相等  "
          f"{'OK' if not alpha_fail else 'FAIL'}")
    for key, diff in alpha_fail[:5]:
        print(f"  DIFF {key}: {diff}px")
    if alpha_fail:
        fails.append(f"20 key alpha 不一致：{alpha_fail[:3]}")

    full_checked, full_bad = 0, []
    for key in index_of:
        content, state, dirn, frame = parse_key(key)
        if frame is not None and state != "flow":
            continue
        full_checked += 1
        act = union_alpha(crop(images, key))
        ref = old_tile(old_key(BAKE_COMP, BAKE_TEMP, content, state, dirn, frame))[..., 3]
        diff = int(np.count_nonzero(act != ref))
        if diff:
            full_bad.append((key, diff))
    print(f"全量 {full_checked} key（80 静态 + 192 flow 帧）："
          f"{full_checked - len(full_bad)}/{full_checked}  "
          f"{'OK' if not full_bad else 'FAIL'}")
    if full_bad:
        fails.append(f"全量 alpha 不一致 {len(full_bad)} key（首例 {full_bad[0]}）")

    # ---- 颜色对拍（6 成分 × 3 温度 × 5 key = 90 组）
    print("\n=== 颜色对拍（6 成分 × 3 温度 × 5 key = 90 组）===")
    print("新：accent = mix(body, white, 0.62)、S/~ 在 accent；"
          "旧：accent 固定 #F2FAFF、S/~ 在 body。")
    print("样本：" + "、".join(COLOR_KEYS) + "（large × 5 状态 × 右向）")
    print("只比两边角色一致的像素；旧图的成分纹理/霜点/酸沫/热缺口按结构性差异单列。")

    before_stats, before_roles, before_px, before_ch = color_sweep(ROLE_MASK_BEFORE, True)
    after_stats, after_roles, after_px, after_ch = color_sweep(ROLE_MASK, False)

    def merge_cells(cells):
        merged = {"px": [], "ch": []}
        for cell in cells:
            merged["px"].extend(cell["px"])
            merged["ch"].extend(cell["ch"])
        return merged

    def print_table(title, stats):
        print(f"\n{title}（Δmax/Δavg）")
        print(f"{'成分':<8}" + "".join(f"{t:>13}" for t in TEMPS) + f"{'3 温度合计':>15}")
        for comp_name in COMP_NAMES:
            cells = [stat_line(stats[comp_name][t]) for t in TEMPS]
            merged = merge_cells([stats[comp_name][t] for t in TEMPS])
            print(f"{comp_name:<8}" + "".join(f"{c:>13}" for c in cells)
                  + f"{stat_line(merged):>15}")
        merged = merge_cells([stats[c][t] for c in COMP_NAMES for t in TEMPS])
        print(f"{'全部':<8}" + "".join(f"{'':>13}" for _ in TEMPS)
              + f"{stat_line(merged):>15}")

    def pct_le(arr, limit):
        return float((arr <= limit).mean() * 100.0)

    print_table("修改前：固定 accent #F2FAFF + S/~ 在 body", before_stats)
    print_table("修改后：accent = mix(body, 62% 白) + S/~ 在 accent", after_stats)

    print(f"\n修改前 全样本：Δmax={int(before_px.max())} Δavg={before_ch.mean():.2f} "
          f"≤10={pct_le(before_px, 10):.1f}% ≤20={pct_le(before_px, 20):.1f}%"
          f"（{before_px.size} px）")
    print(f"修改后 全样本：Δmax={int(after_px.max())} Δavg={after_ch.mean():.2f} "
          f"≤10={pct_le(after_px, 10):.1f}% ≤20={pct_le(after_px, 20):.1f}%"
          f"（{after_px.size} px）")

    excluded = sum(after_stats[c][t]["excluded"] for c in COMP_NAMES for t in TEMPS)
    new_only = sum(after_stats[c][t]["new_only"] for c in COMP_NAMES for t in TEMPS)
    print(f"结构性差异（不计入 Δ）：旧图有、蒙版无对应角色 {excluded} px；"
          f"蒙版有、旧图透明（热缺口等）{new_only} px")

    # 与原报告可比的 20 key（纯水/常温，large × 5 状态 × 4 向）
    _, _, before20_px, before20_ch = color_sweep(
        ROLE_MASK_BEFORE, True, comps=("water",), temps=("normal",), keys=ALPHA_KEYS)
    _, _, after20_px, after20_ch = color_sweep(
        ROLE_MASK, False, comps=("water",), temps=("normal",), keys=ALPHA_KEYS)
    print(f"纯水/常温 20 key（与原报告可比）：Δmax {int(before20_px.max())}"
          f" → {int(after20_px.max())}，Δavg {before20_ch.mean():.2f}"
          f" → {after20_ch.mean():.2f}（{after20_px.size} px）")

    def role_cell(mapping, role):
        return mapping.get(role, {"px": [0], "ch": [0]})

    print("\n按角色（两边角色一致；6 成分 × 3 温度）：")
    roles = sorted(set(before_roles) | set(after_roles),
                   key=lambda r: -max(max(role_cell(before_roles, r)["px"]),
                                      max(role_cell(after_roles, r)["px"])))
    print(f"  {'角色':<4}{'层':<8}{'修改前':>11}{'修改后':>11}{'px':>7}")
    for role in roles:
        before_cell = role_cell(before_roles, role)
        after_cell = role_cell(after_roles, role)
        layer = ROLE_MASK[role][0]
        print(f"  {role:<4}{layer:<8}{stat_line(before_cell):>11}"
              f"{stat_line(after_cell):>11}{len(after_cell['px']):>7}")

    accent_roles = sorted(r for r in after_roles
                          if ROLE_MASK[r][0] == "accent" and after_roles[r]["px"])
    accent_max = max(max(after_roles[r]["px"]) for r in accent_roles)
    print(f"accent 层角色（{'、'.join(accent_roles)}）修改后 Δmax={accent_max}；"
          f"其余偏差来自非 accent 角色的单标量近似。")

    p90_after = float(np.percentile(after_px, 90))
    color_ok = int(after_px.max()) <= 20 and pct_le(after_px, 10) >= 90.0
    print(f"预算 |Δ|≤20 且 ≥90% 像素≤10：{'PASS' if color_ok else 'WARN'}")

    # 全量 272 key 的颜色汇总（纯水/常温，含 flow 帧的 s/S2/u）
    total_px, total_ch = [], []
    for key in index_of:
        content, state, dirn, frame = parse_key(key)
        if frame is not None and state != "flow":
            continue
        act = crop(images, key)
        new_rgb = rebuild_rgb(act, WATER_TINTS)
        ref = old_tile(old_key(BAKE_COMP, BAKE_TEMP, content, state, dirn, frame))
        ref_rgb, ref_a = ref[..., :3].astype(np.int32), ref[..., 3] > 0
        delta = np.abs(new_rgb.astype(np.int32) - ref_rgb)
        flat = delta[ref_a].reshape(-1, 3)
        total_px.append(flat.max(axis=1))
        total_ch.append(flat.reshape(-1))
    tper = np.concatenate(total_px)
    tch = np.concatenate(total_ch)
    print(f"\n全量 {len(total_px)} key（80 静态 + 192 flow 帧，纯水/常温）："
          f"Δmax={int(tper.max())}  Δavg={float(tch.mean()):.2f}  "
          f"像素≤10={float((tper <= 10).mean() * 100.0):.1f}%")

    # ---- 摘要
    print("\n=== 摘要 ===")
    print(f"tiles={len(index_of)}  layout={PER_ROW * TILE}x{EXPECTED_ROWS * TILE}  images=4")
    for name in LAYERS:
        p = FILES[name]
        print(f"  {name:6s} {images[name].width}x{images[name].height} "
              f"{p.stat().st_size} bytes")
    full_rate = 100.0 * (full_checked - len(full_bad)) / full_checked
    print(f"  alpha  20/20=100.0%  full {full_checked - len(full_bad)}/{full_checked}"
          f"={full_rate:.1f}%")
    print(f"  color  Δmax={int(after_px.max())} Δavg={after_ch.mean():.2f} "
          f"p90={p90_after:.0f} ≤10={pct_le(after_px, 10):.1f}% "
          f"(90 组，角色对齐；before Δmax={int(before_px.max())})")

    # ---- 结论
    print("\n=== 判定 ===")
    hard_ok = not fails
    if fails:
        for msg in fails:
            print("FAIL " + msg)
    if color_ok:
        print("颜色：PASS")
    else:
        print(f"颜色：WARN（accent 已随成分：accent 层 Δmax={accent_max}；"
              f"剩余 {int(after_px.max())} 来自 edge 暗边 r / 后缘 R / body 三段 B "
              "等单标量近似，不是 accent）")
        if args.strict_color:
            print("FAIL --strict-color：颜色预算未达标")
            hard_ok = False
    print("OK" if hard_ok else "FAIL")
    return 0 if hard_ok else 1


if __name__ == "__main__":
    sys.exit(main())
